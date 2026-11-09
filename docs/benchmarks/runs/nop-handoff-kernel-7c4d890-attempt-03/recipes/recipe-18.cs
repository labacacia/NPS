// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NPS.NOP.Frames;
using NPS.NOP.Models;
using NPS.NOP.Validation;
using CompensationPolicy = NPS.NOP.Models.CompensationPolicy;

namespace NPS.NOP.Orchestration;

/// <summary>
/// Core NOP Orchestrator: accepts a <see cref="TaskFrame"/>, runs its DAG by
/// dispatching <see cref="DelegateFrame"/>s to Worker Agents, handles retries,
/// condition-based skipping, and result aggregation (NPS-5 §3, §5).
/// </summary>
public sealed class NopOrchestrator : INopOrchestrator
{
    private readonly NPS.NOP.Runtime.NopRuntimeExecutor? _runtime;
    private readonly INopWorkerClient        _worker;
    private readonly INopTaskStore           _store;
    private readonly NopOrchestratorOptions  _opts;
    private readonly ILogger<NopOrchestrator> _log;
    private readonly IHttpClientFactory?     _httpFactory;

    // Cancellation handles keyed by task_id — allows external CancelAsync().
    private readonly Dictionary<string, CancellationTokenSource> _ctsSources = new();
    private readonly SemaphoreSlim _ctsLock = new(1, 1);

    public NopOrchestrator(
        INopWorkerClient          worker,
        INopTaskStore             store,
        NopOrchestratorOptions?   opts        = null,
        ILogger<NopOrchestrator>? log         = null,
        IHttpClientFactory?       httpFactory = null)
    {
        _runtime = opts?.RuntimeBinding is { } binding ? new(binding) : null;
        _worker      = worker;
        _store       = store;
        _opts        = opts ?? new NopOrchestratorOptions();
        _log         = log  ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<NopOrchestrator>.Instance;
        _httpFactory = httpFactory;
    }

    // ── INopOrchestrator ──────────────────────────────────────────────────────

    /// <inheritdoc/>
    public async Task<NopTaskResult> ExecuteAsync(TaskFrame task, CancellationToken ct = default)
    {
        if (_runtime is not null) return await _runtime.ExecuteAsync(task, ct);
        using var activity = NopTelemetry.Source.StartActivity(
            "nps.nop.task.execute", ActivityKind.Server, ExtractTaskContext(task.Context));
        activity?.SetTag("nps.task.id",         task.TaskId);
        activity?.SetTag("nps.dag.node_count",  task.Dag.Nodes.Count);
        activity?.SetTag("nps.task.priority",   task.Priority);
        activity?.SetTag("nps.task.preflight",  task.Preflight);

        var sw = Stopwatch.StartNew();

        // 1a. Validate delegation chain depth
        if (task.DelegateDepth >= NopConstants.MaxDelegateChainDepth)
        {
            _log.LogWarning("Task {TaskId} rejected: delegation chain depth {Depth} ≥ max {Max}",
                task.TaskId, task.DelegateDepth, NopConstants.MaxDelegateChainDepth);
            activity?.SetStatus(ActivityStatusCode.Error, NopErrorCodes.DelegateChainTooDeep);
            NopTelemetry.TasksFailed.Add(1);
            return NopTaskResult.Failure(task.TaskId, NopErrorCodes.DelegateChainTooDeep,
                $"Delegation chain depth {task.DelegateDepth} exceeds the maximum of {NopConstants.MaxDelegateChainDepth}.");
        }

        // 1b. Validate callback_url (MUST https://, SHOULD not be private IP)
        if (!string.IsNullOrEmpty(task.CallbackUrl))
        {
            var urlError = NopCallbackValidator.ValidateCallbackUrl(task.CallbackUrl);
            if (urlError is not null)
            {
                _log.LogWarning("Task {TaskId} rejected: invalid callback_url — {Error}", task.TaskId, urlError);
                return NopTaskResult.Failure(task.TaskId, NopErrorCodes.TaskDagInvalid, urlError);
            }
        }

        // 1c. Validate DAG
        var validation = DagValidator.Validate(task.Dag);
        if (!validation.IsValid)
        {
            _log.LogWarning("DAG validation failed for task {TaskId}: {Error}", task.TaskId, validation.ErrorMessage);
            return NopTaskResult.Failure(task.TaskId, validation.ErrorCode!, validation.ErrorMessage!);
        }

        // 2. Reject already-known tasks
        if (await _store.GetAsync(task.TaskId, ct) is not null)
            return NopTaskResult.Failure(task.TaskId, NopErrorCodes.TaskAlreadyCompleted, $"Task '{task.TaskId}' already exists.");

        // 3. Persist initial record
        var record = new NopTaskRecord
        {
            TaskId    = task.TaskId,
            Frame     = task,
            State     = TaskState.Pending,
            StartedAt = DateTime.UtcNow,
        };
        await _store.SaveAsync(record, ct);

        // 4. Register a linked CTS for external cancellation
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var timeoutMs = (int)Math.Min(task.TimeoutMs, NopConstants.MaxTimeoutMs);
        cts.CancelAfter(timeoutMs);

        await _ctsLock.WaitAsync(ct);
        try { _ctsSources[task.TaskId] = cts; } finally { _ctsLock.Release(); }

        try
        {
            // 5. Optional preflight
            if (task.Preflight)
            {
                await _store.UpdateStateAsync(task.TaskId, TaskState.Preflight, ct);
                var preflightFail = await RunPreflightAsync(task, cts.Token);
                if (preflightFail is not null)
                {
                    _log.LogWarning("Preflight failed for task {TaskId}: {Error}", task.TaskId, preflightFail);
                    await _store.UpdateStateAsync(task.TaskId, TaskState.Failed, ct);
                    return NopTaskResult.Failure(task.TaskId, NopErrorCodes.ResourceInsufficient, preflightFail);
                }
            }

            await _store.UpdateStateAsync(task.TaskId, TaskState.Running, ct);

            // 6. Execute DAG
            var result = await RunDagAsync(task, record, validation.TopologicalOrder!, cts.Token);

            // 7. Finalise state in store
            var finalState = result.FinalState;
            record.CompletedAt = DateTime.UtcNow;
            await _store.UpdateStateAsync(task.TaskId, finalState, ct);

            // 8. Fire callback (fire-and-forget)
            if (_opts.EnableCallback && !string.IsNullOrEmpty(task.CallbackUrl))
                _ = FireCallbackAsync(task.CallbackUrl, task.CallbackSecret, result);

            sw.Stop();
            _log.LogInformation("Task {TaskId} finished as {State}", task.TaskId, finalState);

            bool succeeded = result.FinalState == TaskState.Completed;
            if (succeeded)
            {
                activity?.SetStatus(ActivityStatusCode.Ok);
                NopTelemetry.TasksCompleted.Add(1);
            }
            else
            {
                activity?.SetStatus(ActivityStatusCode.Error, result.ErrorCode);
                NopTelemetry.TasksFailed.Add(1);
            }
            NopTelemetry.TaskDurationMs.Record(sw.Elapsed.TotalMilliseconds,
                new TagList { { "outcome", succeeded ? "success" : "failure" } });

            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Our own timeout fired
            _log.LogWarning("Task {TaskId} exceeded timeout of {Timeout}ms", task.TaskId, timeoutMs);
            await _store.UpdateStateAsync(task.TaskId, TaskState.Failed, ct);
            sw.Stop();
            activity?.SetStatus(ActivityStatusCode.Error, NopErrorCodes.TaskTimeout);
            NopTelemetry.TasksFailed.Add(1);
            NopTelemetry.TaskDurationMs.Record(sw.Elapsed.TotalMilliseconds, new TagList { { "outcome", "timeout" } });
            return NopTaskResult.Failure(task.TaskId, NopErrorCodes.TaskTimeout,
                $"Task exceeded timeout of {timeoutMs}ms.");
        }
        finally
        {
            await _ctsLock.WaitAsync(CancellationToken.None);
            try { _ctsSources.Remove(task.TaskId); } finally { _ctsLock.Release(); }
        }
    }

    /// <inheritdoc/>
    public async Task CancelAsync(string taskId, CancellationToken ct = default)
    {
        if (_runtime is not null) { await _runtime.CancelAsync(taskId, ct); return; }
        await _ctsLock.WaitAsync(ct);
        try
        {
            if (_ctsSources.TryGetValue(taskId, out var cts))
                await cts.CancelAsync();
        }
        finally { _ctsLock.Release(); }

        await _store.UpdateStateAsync(taskId, TaskState.Cancelled, ct);
    }

    /// <inheritdoc/>
    public Task<NopTaskRecord?> GetStatusAsync(string taskId, CancellationToken ct = default)
        => _runtime is not null ? Task.FromResult(_runtime.GetStatus(taskId)) : _store.GetAsync(taskId, ct);

    // ── DAG execution ─────────────────────────────────────────────────────────

    private async Task<NopTaskResult> RunDagAsync(
        TaskFrame           task,
        NopTaskRecord       record,
        IReadOnlyList<string> topoOrder,
        CancellationToken   ct)
    {
        var allNodes     = task.Dag.Nodes.ToDictionary(n => n.Id);
        var nodeResults  = new Dictionary<string, JsonElement>();     // nodeId → result (completed only)
        var nodeStates   = new Dictionary<string, TaskState>();       // nodeId → terminal state
        var inFlight     = new Dictionary<string, Task<NodeOutcome>>(); // nodeId → running task

        // Identify end nodes (no outgoing edges)
        var hasOutgoing  = new HashSet<string>(task.Dag.Edges.Select(e => e.From));
        var endNodeIds   = allNodes.Keys.Where(id => !hasOutgoing.Contains(id)).ToList();

        while (nodeStates.Count < allNodes.Count)
        {
            ct.ThrowIfCancellationRequested();

            // Find nodes whose deps are all done (completed or skipped) and not yet started
            var readyNodes = allNodes.Values
                .Where(n => !nodeStates.ContainsKey(n.Id) && !inFlight.ContainsKey(n.Id))
                .Where(n => AreDepsDone(n, nodeStates))
                .ToList();

            // K-of-N check: for each ready node, verify enough deps succeeded.
            foreach (var n in readyNodes.ToList())
            {
                if (n.InputFrom is not { Count: > 0 }) continue; // no deps, always ok

                int total   = n.InputFrom.Count;
                int k       = n.MinRequired > 0 ? (int)n.MinRequired : total;
                int success = n.InputFrom.Count(d => nodeStates.TryGetValue(d, out var s) && s is TaskState.Completed or TaskState.Skipped);
                int failed  = n.InputFrom.Count(d => nodeStates.TryGetValue(d, out var s) && s == TaskState.Failed);

                if (success < k)
                {
                    // K can never be satisfied — fail the node immediately
                    _log.LogDebug("Node {NodeId} cannot satisfy K-of-N ({K}/{N}): {F} dep(s) failed", n.Id, k, total, failed);
                    nodeStates[n.Id] = TaskState.Failed;
                    await _store.UpdateSubtaskAsync(task.TaskId, n.Id, Guid.NewGuid().ToString("D"),
                        TaskState.Failed, errorCode: NopErrorCodes.SyncDependencyFailed,
                        errorMsg: $"Only {success}/{k} required dependencies succeeded.", ct: ct);
                    readyNodes.Remove(n);
                }
            }

            // Launch ready nodes up to MaxConcurrentNodes
            foreach (var node in readyNodes)
            {
                if (inFlight.Count >= _opts.MaxConcurrentNodes) break;
                _log.LogDebug("Launching node {NodeId}", node.Id);
                inFlight[node.Id] = ExecuteNodeWithRetryAsync(task, node, nodeResults, ct);
            }

            if (inFlight.Count == 0) break; // stuck or finished

            // Wait for the next completion
            var finishedTask = await Task.WhenAny(inFlight.Values);
            var finishedNodeId = inFlight.First(kv => kv.Value == finishedTask).Key;
            inFlight.Remove(finishedNodeId);

            NodeOutcome outcome;
            try
            {
                outcome = await finishedTask;
            }
            catch (OperationCanceledException)
            {
                throw; // propagate timeout / external cancel
            }

            nodeStates[finishedNodeId] = outcome.State;
            if (outcome.Result.HasValue && outcome.State == TaskState.Completed)
                nodeResults[finishedNodeId] = outcome.Result.Value;

            // If this was a failure, check whether any end node is now unrecoverable.
            // K-of-N: even if the failed node can reach an end node, abort only when
            // the end node's K cannot be satisfied by remaining in-flight/not-started nodes.
            if (outcome.State == TaskState.Failed)
            {
                bool mustAbort = endNodeIds.Any(e =>
                    CanReachEndNode(e, finishedNodeId, allNodes, task.Dag.Edges)
                    && !CanEndNodeStillSucceed(e, allNodes, nodeStates, inFlight));

                if (mustAbort)
                {
                    _log.LogWarning("Node {NodeId} failed; end node(s) cannot recover — aborting task {TaskId}",
                        finishedNodeId, task.TaskId);
                    await WaitAndAbortInFlightAsync(inFlight);
                    var compensation = CompensationPolicy.RunsOnFailure(task.CompensationPolicy)
                        ? await RunSagaCompensationAsync(task, allNodes, topoOrder, nodeResults, nodeStates, ct)
                        : null;
                    var errorCode = CompensationFailureErrorCode(task, compensation) ?? NopErrorCodes.SyncDependencyFailed;
                    return NopTaskResult.Failure(
                        task.TaskId,
                        errorCode,
                        $"Node '{finishedNodeId}' failed: {outcome.ErrorCode}",
                        compensation);
                }
            }
        }

        // All nodes done — check for any failures
        var failedNodes = nodeStates.Where(kv => kv.Value == TaskState.Failed).ToList();
        if (failedNodes.Count > 0 && endNodeIds.Any(e => nodeStates.GetValueOrDefault(e) == TaskState.Failed))
        {
            var compensation = CompensationPolicy.RunsOnFailure(task.CompensationPolicy)
                ? await RunSagaCompensationAsync(task, allNodes, topoOrder, nodeResults, nodeStates, ct)
                : null;
            var errorCode = CompensationFailureErrorCode(task, compensation) ?? NopErrorCodes.SyncDependencyFailed;
            return NopTaskResult.Failure(task.TaskId, errorCode,
                $"End node(s) failed: {string.Join(", ", failedNodes.Select(kv => kv.Key))}",
                compensation);
        }

        // Aggregate end-node results
        var aggregated = NopResultAggregator.AggregateEndNodes(
            endNodeIds, nodeResults, _opts.DefaultAggregateStrategy);

        var successCompensation = CompensationPolicy.RunsOnSuccess(task.CompensationPolicy)
            ? await RunSagaCompensationAsync(task, allNodes, topoOrder, nodeResults, nodeStates, ct)
            : null;

        return NopTaskResult.Success(task.TaskId, aggregated, nodeResults, successCompensation);
    }

    // ── Node execution + retry ────────────────────────────────────────────────

    private async Task<NodeOutcome> ExecuteNodeWithRetryAsync(
        TaskFrame task,
        DagNode   node,
        IReadOnlyDictionary<string, JsonElement> context,
        CancellationToken ct)
    {
        var subtaskId      = Guid.NewGuid().ToString("D");
        var idempotencyKey = Guid.NewGuid().ToString("D"); // same across retries
        int maxRetries     = node.RetryPolicy?.MaxRetries ?? task.MaxRetries;

        using var nodeActivity = NopTelemetry.Source.StartActivity("nps.nop.node.execute", ActivityKind.Client);
        nodeActivity?.SetTag("nps.node.id",         node.Id);
        nodeActivity?.SetTag("nps.node.agent_nid",  node.Agent);
        nodeActivity?.SetTag("nps.node.action",     node.Action);
        nodeActivity?.SetTag("nps.node.max_retries", maxRetries);

        var nodeSw = Stopwatch.StartNew();

        for (int attempt = 1; attempt <= maxRetries + 1; attempt++)
        {
            ct.ThrowIfCancellationRequested();

            // Evaluate condition before first attempt (and after each retry? — evaluate once)
            if (attempt == 1 && !string.IsNullOrEmpty(node.Condition))
            {
                try
                {
                    if (!NopConditionEvaluator.Evaluate(node.Condition, context))
                    {
                        _log.LogDebug("Node {NodeId} skipped (condition=false)", node.Id);
                        await _store.UpdateSubtaskAsync(task.TaskId, node.Id, subtaskId, TaskState.Skipped, ct: ct);
                        return new NodeOutcome(TaskState.Skipped, null, null);
                    }
                }
                catch (NopConditionException ex)
                {
                    _log.LogError(ex, "Condition evaluation error for node {NodeId}", node.Id);
                    await _store.UpdateSubtaskAsync(task.TaskId, node.Id, subtaskId,
                        TaskState.Failed, errorCode: NopErrorCodes.ConditionEvalError,
                        errorMsg: ex.Message, attempt: attempt, ct: ct);
                    return new NodeOutcome(TaskState.Failed, null, NopErrorCodes.ConditionEvalError);
                }
            }

            await _store.UpdateSubtaskAsync(task.TaskId, node.Id, subtaskId, TaskState.Running,
                attempt: attempt, ct: ct);

            var outcome = await ExecuteNodeOnceAsync(task, node, subtaskId, idempotencyKey, context, ct);

            if (outcome.State == TaskState.Completed)
            {
                await _store.UpdateSubtaskAsync(task.TaskId, node.Id, subtaskId,
                    TaskState.Completed, result: outcome.Result, attempt: attempt, ct: ct);
                nodeSw.Stop();
                nodeActivity?.SetTag("nps.node.attempts", attempt);
                nodeActivity?.SetStatus(ActivityStatusCode.Ok);
                NopTelemetry.NodeDurationMs.Record(nodeSw.Elapsed.TotalMilliseconds, new TagList { { "outcome", "success" } });
                return outcome;
            }

            // Failed — check if retryable
            bool retriable = ShouldRetry(node.RetryPolicy, outcome.ErrorCode, attempt, maxRetries);
            if (!retriable)
            {
                _log.LogWarning("Node {NodeId} failed after {Attempts} attempt(s): {ErrorCode}",
                    node.Id, attempt, outcome.ErrorCode);
                await _store.UpdateSubtaskAsync(task.TaskId, node.Id, subtaskId,
                    TaskState.Failed, errorCode: outcome.ErrorCode, errorMsg: outcome.ErrorMessage,
                    attempt: attempt, ct: ct);
                nodeSw.Stop();
                nodeActivity?.SetTag("nps.node.attempts", attempt);
                nodeActivity?.SetStatus(ActivityStatusCode.Error, outcome.ErrorCode);
                NopTelemetry.NodeDurationMs.Record(nodeSw.Elapsed.TotalMilliseconds, new TagList { { "outcome", "failure" } });
                return outcome;
            }

            NopTelemetry.NodeRetries.Add(1);
            var delayMs = (int)(node.RetryPolicy?.ComputeDelayMs(attempt - 1) ?? 1000);
            _log.LogDebug("Node {NodeId} retrying in {Delay}ms (attempt {A}/{Max})",
                node.Id, delayMs, attempt, maxRetries + 1);
            await Task.Delay(delayMs, ct);
        }

        // Exhausted retries
        await _store.UpdateSubtaskAsync(task.TaskId, node.Id, subtaskId,
            TaskState.Failed, errorCode: NopErrorCodes.DelegateTimeout,
            errorMsg: $"Node '{node.Id}' exhausted {maxRetries} retries.", ct: ct);
        nodeSw.Stop();
        nodeActivity?.SetStatus(ActivityStatusCode.Error, NopErrorCodes.DelegateTimeout);
        NopTelemetry.NodeDurationMs.Record(nodeSw.Elapsed.TotalMilliseconds, new TagList { { "outcome", "exhausted" } });
        return new NodeOutcome(TaskState.Failed, null, NopErrorCodes.DelegateTimeout);
    }

    private async Task<NodeOutcome> ExecuteNodeOnceAsync(
        TaskFrame task,
        DagNode   node,
        string    subtaskId,
        string    idempotencyKey,
        IReadOnlyDictionary<string, JsonElement> context,
        CancellationToken ct)
    {
        // Resolve input_mapping → params
        JsonElement? resolvedParams = null;
        try
        {
            resolvedParams = NopInputMapper.BuildParams(
                node.InputMapping?.ToDictionary(kv => kv.Key, kv => kv.Value),
                context);
        }
        catch (NopMappingException ex)
        {
            return new NodeOutcome(TaskState.Failed, null, ex.ErrorCode, ex.Message);
        }

        var nodeTimeoutMs = node.TimeoutMs ?? task.TimeoutMs;
        using var nodeCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        nodeCts.CancelAfter((int)Math.Min(nodeTimeoutMs, NopConstants.MaxTimeoutMs));

        var deadline = DateTime.UtcNow.AddMilliseconds(nodeTimeoutMs);
        var scope    = JsonDocument.Parse("{}").RootElement; // scope handled by NIP layer

        // Propagate current span into DelegateFrame so the worker agent can link its spans.
        var delegateCtx = task.Context;
        if (Activity.Current is { } currentActivity)
        {
            delegateCtx = (delegateCtx ?? new TaskContext()) with
            {
                TraceId    = currentActivity.TraceId.ToString(),
                SpanId     = currentActivity.SpanId.ToString(),
                TraceFlags = (byte)currentActivity.ActivityTraceFlags,
            };
        }

        var delegateFrame = new DelegateFrame
        {
            ParentTaskId   = task.TaskId,
            SubtaskId      = subtaskId,
            NodeId         = node.Id,
            TargetAgentNid = node.Agent,
            Action         = node.Action,
            Params         = resolvedParams,
            DelegatedScope = scope,
            DeadlineAt     = deadline.ToString("O"),
            IdempotencyKey = idempotencyKey,
            Priority       = task.Priority,
            Context        = delegateCtx,
            DelegateDepth  = task.DelegateDepth + 1,
        };

        try
        {
            JsonElement? finalResult = null;
            string? errorCode = null;
            string? errorMsg  = null;
            ulong   lastSeq   = 0;
            bool    gotFinal  = false;

            await foreach (var frame in _worker.DelegateAsync(delegateFrame, nodeCts.Token)
                                                .WithCancellation(nodeCts.Token))
            {
                // Sequence gap check
                if (frame.Seq != lastSeq && frame.Seq != 0)
                {
                    if (frame.Seq != lastSeq + 1)
                    {
                        _log.LogWarning("Node {NodeId}: seq gap {A} → {B}", node.Id, lastSeq, frame.Seq);
                        return new NodeOutcome(TaskState.Failed, null, NopErrorCodes.StreamSeqGap);
                    }
                }
                lastSeq = frame.Seq;

                // Sender NID validation
                if (_opts.ValidateSenderNid && frame.SenderNid != node.Agent)
                {
                    _log.LogWarning("Node {NodeId}: sender_nid mismatch (expected {Expected}, got {Got})",
                        node.Id, node.Agent, frame.SenderNid);
                    return new NodeOutcome(TaskState.Failed, null, NopErrorCodes.StreamNidMismatch);
                }

                if (frame.IsFinal)
                {
                    gotFinal = true;
                    if (frame.Error is not null)
                    {
                        errorCode = frame.Error.Code;
                        errorMsg  = frame.Error.Message;
                    }
                    else
                    {
                        finalResult = frame.Data;
                    }
                    break;
                }

                // Intermediate frame — log at trace level
                _log.LogTrace("Node {NodeId} intermediate result seq={Seq}", node.Id, frame.Seq);
            }

            if (!gotFinal)
                return new NodeOutcome(TaskState.Failed, null, NopErrorCodes.DelegateTimeout, "Stream ended without final frame.");

            if (errorCode is not null)
                return new NodeOutcome(TaskState.Failed, null, errorCode, errorMsg);

            return new NodeOutcome(TaskState.Completed, finalResult, null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            // Node-level timeout
            return new NodeOutcome(TaskState.Failed, null, NopErrorCodes.DelegateTimeout,
                $"Node '{node.Id}' timed out after {nodeTimeoutMs}ms.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when a node's dependencies are in a terminal state that
    /// allows it to either proceed (K-of-N satisfied) or be marked failed
    /// (impossible to satisfy K). Supports K-of-N via <see cref="DagNode.MinRequired"/>.
    /// </summary>
    private static bool AreDepsDone(DagNode node, Dictionary<string, TaskState> states)
    {
        if (node.InputFrom is not { Count: > 0 }) return true;

        int total   = node.InputFrom.Count;
        int k       = node.MinRequired > 0 ? (int)node.MinRequired : total; // 0 = all
        int success = node.InputFrom.Count(d => states.TryGetValue(d, out var s) && s is TaskState.Completed or TaskState.Skipped);
        int failed  = node.InputFrom.Count(d => states.TryGetValue(d, out var s) && s == TaskState.Failed);

        if (success >= k)       return true; // K already satisfied
        if (total - failed < k) return true; // Impossible to satisfy K (too many failures)
        return false;                         // Still waiting
    }

    /// <summary>
    /// Runs preflight probes in parallel against all unique (agent, action) pairs in the DAG.
    /// Returns a non-null failure message if any agent is unavailable; null on success (NPS-5 §4).
    /// </summary>
    private async Task<string?> RunPreflightAsync(TaskFrame task, CancellationToken ct)
    {
        // Deduplicate by agent NID (one probe per unique agent)
        var uniqueAgents = task.Dag.Nodes
            .GroupBy(n => n.Agent)
            .Select(g => (Agent: g.Key, Actions: g.Select(n => n.Action).Distinct().ToList()))
            .ToList();

        _log.LogDebug("Running preflight for task {TaskId} against {Count} agent(s)", task.TaskId, uniqueAgents.Count);

        var probes = uniqueAgents.Select(a =>
            _worker.PreflightAsync(a.Agent, a.Actions.First(), ct: ct)).ToList();

        PreflightResult[] results;
        try
        {
            results = await Task.WhenAll(probes);
        }
        catch (Exception ex)
        {
            return $"Preflight probe failed: {ex.Message}";
        }

        var unavailable = results.FirstOrDefault(r => !r.Available);
        if (unavailable is not null)
        {
            return $"Agent '{unavailable.AgentNid}' is unavailable: {unavailable.UnavailableReason ?? "no reason given"}";
        }

        _log.LogDebug("Preflight passed for task {TaskId}", task.TaskId);
        return null; // all clear
    }

    private static bool ShouldRetry(RetryPolicy? policy, string? errorCode, int attempt, int maxRetries)
    {
        if (attempt > maxRetries) return false;
        if (policy?.RetryOn is { Count: > 0 } retryOn && errorCode is not null)
            return retryOn.Contains(errorCode);
        return true;
    }

    /// <summary>
    /// Returns true when end node <paramref name="endNodeId"/> can still complete successfully,
    /// even after a dependency failure, considering K-of-N (<see cref="DagNode.MinRequired"/>).
    /// Uses an optimistic view: in-flight and not-yet-started nodes are assumed to eventually succeed.
    /// </summary>
    private static bool CanEndNodeStillSucceed(
        string                           endNodeId,
        Dictionary<string, DagNode>      allNodes,
        Dictionary<string, TaskState>    nodeStates,
        Dictionary<string, Task<NodeOutcome>> inFlight)
    {
        var node = allNodes[endNodeId];
        if (node.InputFrom is not { Count: > 0 }) return false; // no deps but node reachable → can't recover

        int total   = node.InputFrom.Count;
        int k       = node.MinRequired > 0 ? (int)node.MinRequired : total;
        int failed  = node.InputFrom.Count(d => nodeStates.TryGetValue(d, out var s) && s == TaskState.Failed);
        // Optimistic: remaining (non-failed) deps might all succeed
        int optimistic = total - failed;

        return optimistic >= k;
    }

    private static bool CanReachEndNode(
        string endNodeId,
        string failedNodeId,
        Dictionary<string, DagNode> allNodes,
        IReadOnlyList<DagEdge> edges)
    {
        // BFS/DFS: can we reach endNodeId from failedNodeId following edges?
        var adj     = edges.GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.Select(e => e.To).ToList());
        var visited = new HashSet<string>();
        var queue   = new Queue<string>();
        queue.Enqueue(failedNodeId);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (cur == endNodeId) return true;
            if (!visited.Add(cur)) continue;
            if (adj.TryGetValue(cur, out var neighbors))
                foreach (var n in neighbors) queue.Enqueue(n);
        }
        return false;
    }

    private static async Task WaitAndAbortInFlightAsync(Dictionary<string, Task<NodeOutcome>> inFlight)
    {
        try { await Task.WhenAll(inFlight.Values); }
        catch { /* ignore — already failed */ }
    }

    /// <summary>
    /// Posts the task result to <paramref name="callbackUrl"/> with exponential backoff retry.
    /// Retries up to <see cref="NopConstants.CallbackMaxRetries"/> times (NPS-5 §8.4).
    /// Failures are non-fatal — logged and swallowed.
    /// </summary>
    private async Task FireCallbackAsync(string callbackUrl, string? callbackSecret, NopTaskResult result)
    {
        var payload = JsonSerializer.Serialize(result, s_callbackSerializerOpts);
        var signature = BuildCallbackSignature(callbackSecret, payload);
        if (!string.IsNullOrWhiteSpace(callbackSecret) && signature is null)
        {
            _log.LogWarning("callback_secret is not a valid base64url-encoded 32-byte HMAC key; callback will be sent without X-NPS-Signature.");
        }

        for (int attempt = 1; attempt <= NopConstants.CallbackMaxRetries; attempt++)
        {
            try
            {
                using var http    = _httpFactory?.CreateClient("NopCallback")
                                    ?? new HttpClient { Timeout = TimeSpan.FromMilliseconds(_opts.CallbackTimeoutMs) };
                using var content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");
                using var request = new HttpRequestMessage(HttpMethod.Post, callbackUrl)
                {
                    Content = content,
                };
                if (signature is not null)
                    request.Headers.TryAddWithoutValidation("X-NPS-Signature", signature);

                var response = await http.SendAsync(request);

                if (response.IsSuccessStatusCode)
                {
                    _log.LogInformation("Callback to {Url} succeeded on attempt {Attempt} ({Status})",
                        callbackUrl, attempt, response.StatusCode);
                    return;
                }

                _log.LogWarning("Callback to {Url} returned non-success {Status} (attempt {Attempt}/{Max})",
                    callbackUrl, response.StatusCode, attempt, NopConstants.CallbackMaxRetries);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _log.LogWarning(ex, "Callback to {Url} failed with exception (attempt {Attempt}/{Max})",
                    callbackUrl, attempt, NopConstants.CallbackMaxRetries);
            }

            if (attempt < NopConstants.CallbackMaxRetries && _opts.CallbackRetryBaseDelayMs > 0)
            {
                // Exponential backoff: base × 2^(attempt-1)
                var delayMs = (int)(_opts.CallbackRetryBaseDelayMs * Math.Pow(2, attempt - 1));
                await Task.Delay(delayMs);
            }
        }

        _log.LogWarning("Callback to {Url} gave up after {Max} attempt(s) — non-fatal.",
            callbackUrl, NopConstants.CallbackMaxRetries);
    }

    private static string? BuildCallbackSignature(string? callbackSecret, string payload)
    {
        if (string.IsNullOrWhiteSpace(callbackSecret))
            return null;

        if (!TryDecodeBase64Url(callbackSecret, out var key) || key.Length != 32)
            return null;

        using var hmac = new HMACSHA256(key);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool TryDecodeBase64Url(string value, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        try
        {
            var normalized = value.Trim().Replace('-', '+').Replace('_', '/');
            normalized = normalized.PadRight(normalized.Length + (4 - normalized.Length % 4) % 4, '=');
            bytes = Convert.FromBase64String(normalized);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static readonly JsonSerializerOptions s_callbackSerializerOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    // ── Saga compensation ─────────────────────────────────────────────────────

    /// <summary>
    /// Dispatches compensation actions for all completed nodes that declared a
    /// <c>compensate_action</c>, in reverse topological order (NPS-5 §3.5).
    /// Compensation failures are non-fatal unless <c>compensation_policy</c> is <c>"strict"</c>.
    /// </summary>
    private async Task<SagaCompensationResult> RunSagaCompensationAsync(
        TaskFrame                                task,
        Dictionary<string, DagNode>              allNodes,
        IReadOnlyList<string>                    topoOrder,
        Dictionary<string, JsonElement>          nodeResults,
        Dictionary<string, TaskState>            nodeStates,
        CancellationToken                        ct)
    {
        // Collect completed nodes in reverse topo order.
        var completed = topoOrder
            .Where(id => nodeStates.TryGetValue(id, out var s) && s == TaskState.Completed
                      && allNodes.ContainsKey(id))
            .Reverse()
            .ToList();

        if (CompensationPolicy.IsStrict(task.CompensationPolicy))
        {
            var missing = completed
                .Where(id => string.IsNullOrWhiteSpace(allNodes[id].CompensateAction))
                .ToList();
            if (missing.Count > 0)
            {
                _log.LogWarning(
                    "Strict saga compensation for task {TaskId} cannot proceed; node(s) lack compensate_action: {Nodes}",
                    task.TaskId, string.Join(", ", missing));
                return new SagaCompensationResult(0, 0, missing.Count, missing);
            }
        }

        var toCompensate = completed
            .Where(id => !string.IsNullOrWhiteSpace(allNodes[id].CompensateAction))
            .ToList();

        if (toCompensate.Count == 0)
            return new SagaCompensationResult(0, 0, 0, []);

        _log.LogInformation("Saga compensation: {Count} node(s) to compensate for task {TaskId}",
            toCompensate.Count, task.TaskId);

        await _store.UpdateStateAsync(task.TaskId, TaskState.Compensating, ct);

        int succeeded   = 0;
        var failedIds   = new List<string>();

        foreach (var nodeId in toCompensate)
        {
            var node = allNodes[nodeId];
            // Synthetic node: same agent, compensation action + params
            var compensationNode = node with
            {
                Action       = node.CompensateAction!,
                InputMapping = node.CompensateParamsMapping,
            };

            var outcome = await ExecuteNodeOnceAsync(
                task, compensationNode,
                subtaskId:      Guid.NewGuid().ToString("D"),
                idempotencyKey: Guid.NewGuid().ToString("D"),
                context:        nodeResults,
                ct:             ct);

            if (outcome.State == TaskState.Completed)
            {
                succeeded++;
                _log.LogInformation("Compensation for node {NodeId} succeeded", nodeId);
            }
            else
            {
                failedIds.Add(nodeId);
                _log.LogWarning("Compensation for node {NodeId} failed: {ErrorCode} — {Msg}",
                    nodeId, outcome.ErrorCode, outcome.ErrorMessage);
            }
        }

        int failed = failedIds.Count;
        if (failed == 0)
            _log.LogInformation("Saga compensation for task {TaskId} complete ({Total} succeeded)", task.TaskId, succeeded);
        else
            _log.LogWarning("Saga compensation for task {TaskId}: {Failed}/{Total} failed", task.TaskId, failed, toCompensate.Count);

        return new SagaCompensationResult(toCompensate.Count, succeeded, failed, failedIds);
    }

    private static string? CompensationFailureErrorCode(TaskFrame task, SagaCompensationResult? compensation)
    {
        if (!CompensationPolicy.IsStrict(task.CompensationPolicy) || compensation is not { Failed: > 0 })
            return null;

        return compensation.Attempted == 0
            ? NopErrorCodes.CompensationNotSupported
            : NopErrorCodes.CompensationFailed;
    }

    // ── Telemetry helpers ─────────────────────────────────────────────────────

    private static ActivityContext ExtractTaskContext(TaskContext? ctx)
    {
        if (ctx is not { TraceId: { Length: 32 } traceId, SpanId: { Length: 16 } spanId })
            return default;

        try
        {
            var actTraceId = ActivityTraceId.CreateFromString(traceId.AsSpan());
            var actSpanId  = ActivitySpanId.CreateFromString(spanId.AsSpan());
            var flags      = ctx.TraceFlags.HasValue
                ? (ActivityTraceFlags)ctx.TraceFlags.Value
                : ActivityTraceFlags.None;
            return new ActivityContext(actTraceId, actSpanId, flags, isRemote: true);
        }
        catch (ArgumentException)
        {
            return default;
        }
    }

    // ── Inner types ───────────────────────────────────────────────────────────

    private sealed record NodeOutcome(
        TaskState    State,
        JsonElement? Result,
        string?      ErrorCode,
        string?      ErrorMessage = null);
}
