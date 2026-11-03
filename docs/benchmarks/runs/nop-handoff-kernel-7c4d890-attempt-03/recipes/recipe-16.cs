// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using NPS.NOP.Frames;
using NPS.NOP.Models;
using NPS.NOP.Orchestration;
using NPS.NOP.Validation;

namespace NPS.NOP.Runtime;

// Selected reference path; shares DAG validation, mapping and wire models with legacy NOP.
// Serial execution bounds retained outputs and makes uncertainty stop all dependent work.
internal sealed class NopRuntimeExecutor(NopRuntimeBinding binding)
{
    private readonly object _admissionGate = new();
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _active = new();

    internal async Task<NopTaskResult> ExecuteAsync(TaskFrame input, CancellationToken ct)
    {
        var unresolved = false;
        var knownExecution = false;
        var results = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        var (task, digest) = NopTaskIdentity.Freeze(input);
        var validation = Validate(task);
        if (validation is not null)
        {
            var prior = binding.Journal.ReadReplay(binding.CallerNid, task.TaskId, digest);
            return prior is null ? Terminal(task.TaskId, NopExecutionOutcome.NotDispatched, validation)
                : prior.Result ?? new NopTaskResult { TaskId = task.TaskId, FinalState = TaskState.Running };
        }
        var order = DagValidator.Validate(task.Dag).TopologicalOrder!;
        var start = DateTimeOffset.UtcNow;
        var operations = task.Dag.Nodes.ToDictionary(n => n.Id,
            _ => new NopOperationIdentity(Guid.NewGuid().ToString("D"), Guid.NewGuid().ToString("D")));
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        stop.CancelAfter(TimeSpan.FromMilliseconds(task.TimeoutMs));
        NopRuntimeAdmission admission;
        lock (_admissionGate)
        {
            admission = binding.Journal.Admit(new(binding.CallerNid, task, digest,
                start, start.AddMilliseconds(task.TimeoutMs), operations));
            if (admission.OwnerToken is not null) _active[task.TaskId] = stop;
        }
        if (admission.OwnerToken is null)
            return admission.Result ?? new NopTaskResult { TaskId = task.TaskId, FinalState = TaskState.Running };
        var elapsed = Stopwatch.StartNew();
        var outcome = NopExecutionOutcome.NotDispatched;
        string? error = null;
        try
        {
            foreach (var id in order)
            {
                stop.Token.ThrowIfCancellationRequested();
                var node = task.Dag.Nodes.Single(n => n.Id == id);
                var parameters = NopInputMapper.BuildParams(node.InputMapping, results);
                var identity = operations[id];
                var retry = node.RetryPolicy?.MaxRetries ?? task.MaxRetries;
                var nodeStart = elapsed.Elapsed;
                var nodeBudget = TimeSpan.FromMilliseconds(node.TimeoutMs ?? task.TimeoutMs);
                var nodeDeadline = start.AddMilliseconds(task.TimeoutMs);
                var proposedDeadline = DateTimeOffset.UtcNow + nodeBudget;
                if (nodeDeadline > proposedDeadline) nodeDeadline = proposedDeadline;
                for (var attempt = 0; ; attempt++)
                {
                    stop.Token.ThrowIfCancellationRequested();
                    var remaining = TimeSpan.FromMilliseconds(task.TimeoutMs) - elapsed.Elapsed;
                    if (remaining <= TimeSpan.Zero) throw new OperationCanceledException();
                    var nodeRemaining = nodeBudget - (elapsed.Elapsed - nodeStart);
                    if (remaining > nodeRemaining) remaining = nodeRemaining;
                    if (remaining <= TimeSpan.Zero) throw new OperationCanceledException();
                    using var nodeStop = CancellationTokenSource.CreateLinkedTokenSource(stop.Token);
                    nodeStop.CancelAfter(remaining);
                    var frame = new DelegateFrame
                    {
                        ParentTaskId = task.TaskId, SubtaskId = identity.SubtaskId, NodeId = id,
                        TargetAgentNid = node.Agent, Action = node.Action, Params = parameters,
                        IdempotencyKey = identity.IdempotencyKey, DelegatedScope = JsonSerializer.SerializeToElement(new { }),
                        DeadlineAt = nodeDeadline.ToString("O"),
                        DelegateDepth = task.DelegateDepth + 1, Priority = task.Priority,
                        // Transparent context is not a grant; the transport binds current host authority.
                        Context = task.Context,
                    };
                    unresolved = true;
                    var terminal = await ObserveAsync(frame, nodeStop.Token);
                    unresolved = false;
                    if (terminal.ExecutionOutcome == NopExecutionOutcome.Indeterminate)
                        throw new StopExecution(NopExecutionOutcome.Indeterminate, NopErrorCodes.ExecutionIndeterminate);
                    if (terminal.ExecutionOutcome == NopExecutionOutcome.KnownTerminal) knownExecution = true;
                    if (terminal.Error is null)
                    {
                        if (terminal.Data is { } data) results[id] = data.Clone();
                        if (results.Sum(p => JsonSerializer.SerializeToUtf8Bytes(p.Value).Length) > 262144)
                            throw new StopExecution(NopExecutionOutcome.KnownTerminal, NopErrorCodes.ResourceInsufficient);
                        break;
                    }
                    // An error flag/timeout never proves safe retry. Only positive non-dispatch does.
                    if (terminal.ExecutionOutcome != NopExecutionOutcome.NotDispatched
                        || !terminal.Error.Retryable || attempt >= retry
                        || node.RetryPolicy?.RetryOn is { } selected && !selected.Contains(terminal.Error.Code))
                        throw new StopExecution(knownExecution ? NopExecutionOutcome.KnownTerminal : NopExecutionOutcome.NotDispatched,
                            terminal.Error.Code);
                    var delay = TimeSpan.FromMilliseconds(node.RetryPolicy?.ComputeDelayMs(attempt) ?? 1000);
                    await Task.Delay(delay, stop.Token);
                }
            }
            outcome = knownExecution ? NopExecutionOutcome.KnownTerminal : NopExecutionOutcome.NotDispatched;
        }
        catch (StopExecution stopped)
        {
            outcome = stopped.Certainty; error = stopped.Code;
        }
        catch (OperationCanceledException)
        {
            outcome = unresolved ? NopExecutionOutcome.Indeterminate
                : knownExecution ? NopExecutionOutcome.KnownTerminal : NopExecutionOutcome.NotDispatched;
            error = unresolved ? NopErrorCodes.ExecutionIndeterminate : NopErrorCodes.TaskCancelled;
        }
        catch (NopMappingException)
        {
            outcome = knownExecution ? NopExecutionOutcome.KnownTerminal : NopExecutionOutcome.NotDispatched;
            error = NopErrorCodes.InputMappingError;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            // No task/transport details enter logs or error messages. Journal failures still throw below.
            outcome = unresolved ? NopExecutionOutcome.Indeterminate
                : knownExecution ? NopExecutionOutcome.KnownTerminal : NopExecutionOutcome.NotDispatched;
            error = unresolved ? NopErrorCodes.ExecutionIndeterminate : NopErrorCodes.DelegateRejected;
        }
        finally { Retire(task.TaskId); }
        return Finish(outcome, error);

        NopTaskResult Finish(string certainty, string? code)
        {
            Retire(task.TaskId);
            var result = Terminal(task.TaskId, certainty, code, code is null ? results : null);
            // Synchronous bounded host journal is independent of the cancelled operation token.
            return binding.Journal.Commit(binding.CallerNid, task.TaskId, admission.OwnerToken!, result);
        }
    }

    internal Task CancelAsync(string taskId, CancellationToken ct)
    {
        CancellationTokenSource? active;
        lock (_admissionGate) _active.TryGetValue(taskId, out active);
        if (active is not null)
        {
            try
            {
                // Token cancellation is initiated now; arbitrary transport callbacks run
                // asynchronously outside admission locks and cannot hold up a cancel request.
                var callbacks = active.CancelAsync();
                _ = callbacks.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            }
            catch (ObjectDisposedException) { /* Completion already retired this execution. */ }
        }
        return Task.CompletedTask;
    }

    private void Retire(string taskId)
    {
        lock (_admissionGate) _active.TryRemove(taskId, out _);
    }

    internal NopTaskRecord? GetStatus(string taskId) => binding.Journal.GetStatus(binding.CallerNid, taskId);

    private sealed class StopExecution(string certainty, string code) : Exception
    {
        internal string Certainty { get; } = certainty;
        internal string Code { get; } = code;
    }

    private static string? Validate(TaskFrame task)
    {
        if (task.Dag is null || task.Dag.Nodes is null || task.Dag.Edges is null
            || task.Dag.Nodes.Count is < 1 or > 64
            || task.Dag.Nodes.Any(node => node is null || string.IsNullOrWhiteSpace(node.Id)
                || string.IsNullOrWhiteSpace(node.Agent) || string.IsNullOrWhiteSpace(node.Action)) || task.TimeoutMs is < 1 or > 120000
            || task.ResultTtlSeconds is < 1 or > 900 || task.DelegateDepth is < 0 or >= NopConstants.MaxDelegateChainDepth
            || !Guid.TryParseExact(task.TaskId, "D", out var guid) || guid.Version != 4
            || task.CallbackUrl is not null || task.CallbackSecret is not null || task.Preflight)
            return NopErrorCodes.TaskDagInvalid;
        var valid = DagValidator.Validate(task.Dag);
        if (!valid.IsValid) return valid.ErrorCode;
        foreach (var node in task.Dag.Nodes)
        {
            if (node.Condition is not null || node.MinRequired != 0 || node.CompensateAction is not null
                || node.CompensateParamsMapping is not null || node.TimeoutMs == 0
                || string.IsNullOrEmpty(node.Agent) || !node.Agent.StartsWith("urn:nps:", StringComparison.Ordinal)) return NopErrorCodes.TaskDagInvalid;
            var actual = task.Dag.Edges.Where(e => e.To == node.Id).Select(e => e.From).ToHashSet(StringComparer.Ordinal);
            if (!actual.SetEquals(node.InputFrom ?? [])) return NopErrorCodes.TaskDagInvalid;
        }
        return null;
    }

    private async Task<AlignStreamFrame> ObserveAsync(DelegateFrame request, CancellationToken ct)
    {
        var enumerator = binding.Worker.DelegateAsync(request, ct).GetAsyncEnumerator(ct);
        string? streamId = null;
        ulong expected = 0;
        var bytes = 0;
        try
        {
            while (await enumerator.MoveNextAsync().AsTask().WaitAsync(ct))
            {
                var envelope = enumerator.Current;
                var frame = envelope.Frame;
                bytes += JsonSerializer.SerializeToUtf8Bytes(frame).Length;
                if (expected >= 16 || bytes > 65536 || envelope.PeerNid != request.TargetAgentNid
                    || frame.SenderNid != envelope.PeerNid || frame.TaskId != request.ParentTaskId
                    || frame.SubtaskId != request.SubtaskId || frame.Seq != expected++
                    || !Guid.TryParseExact(frame.StreamId, "D", out var streamGuid) || streamGuid.Version != 4
                    || streamId is not null && streamId != frame.StreamId)
                    throw new InvalidDataException();
                streamId ??= frame.StreamId;
                if (!frame.IsFinal)
                {
                    if (frame.ExecutionOutcome is not null || frame.Error is not null) throw new InvalidDataException();
                    continue;
                }
                if (frame.Error is { } invalidError && (string.IsNullOrWhiteSpace(invalidError.Code)
                        || invalidError.Code.Length > 128 || invalidError.Message is null || invalidError.Message.Length > 2048)
                    || frame.ExecutionOutcome is not (NopExecutionOutcome.NotDispatched or NopExecutionOutcome.KnownTerminal or NopExecutionOutcome.Indeterminate)
                    || frame.Error is not null && frame.Data is not null
                    || frame.ExecutionOutcome == NopExecutionOutcome.NotDispatched && (frame.Error is null || frame.Data is not null)
                    || frame.ExecutionOutcome == NopExecutionOutcome.Indeterminate && (frame.Error?.Code != NopErrorCodes.ExecutionIndeterminate || frame.Error.Retryable || frame.Data is not null)
                    || frame.ExecutionOutcome != NopExecutionOutcome.Indeterminate && frame.Error?.Code == NopErrorCodes.ExecutionIndeterminate)
                    throw new InvalidDataException();
                return frame;
            }
            throw new InvalidDataException(); // EOF is not terminal evidence or a retry authorization.
        }
        finally
        {
            // A transport ignoring cancellation must not extend the task deadline on disposal.
            var dispose = enumerator.DisposeAsync().AsTask();
            if (!dispose.IsCompleted)
                _ = dispose.ContinueWith(t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
            else await dispose;
        }
    }

    internal static NopTaskResult Terminal(string id, string outcome, string? code,
        IReadOnlyDictionary<string, JsonElement>? results = null) => new()
    {
        TaskId = id, ExecutionOutcome = outcome, FinalState = code is null ? TaskState.Completed : TaskState.Failed,
        ErrorCode = code, ErrorMessage = code is null ? null : "Bounded runtime execution did not complete.",
        NodeResults = results ?? new Dictionary<string, JsonElement>(),
        AggregatedResult = results is null ? null : JsonSerializer.SerializeToElement(results),
    };
}
