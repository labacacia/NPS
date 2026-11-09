// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

namespace NPS.NOP.Orchestration;

/// <summary>
/// Configuration options for <see cref="NopOrchestrator"/>.
/// </summary>
public sealed class NopOrchestratorOptions
{
    /// <summary>Explicit host-authenticated bounded runtime path. Null preserves the legacy implementation.</summary>
    public NPS.NOP.Runtime.NopRuntimeBinding? RuntimeBinding { get; set; }

    /// <summary>
    /// Maximum number of DAG nodes that may execute concurrently per task.
    /// Defaults to <see cref="Environment.ProcessorCount"/> × 2.
    /// </summary>
    public int MaxConcurrentNodes { get; set; } = Environment.ProcessorCount * 2;

    /// <summary>
    /// When <c>true</c>, the orchestrator validates <see cref="AlignStreamFrame.SenderNid"/>
    /// against the <see cref="NPS.NOP.Models.DagNode.Agent"/> NID for every received frame.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool ValidateSenderNid { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, the orchestrator POSTs the <see cref="NopTaskResult"/>
    /// to <c>TaskFrame.callback_url</c> on completion (fire-and-forget).
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool EnableCallback { get; set; } = true;

    /// <summary>
    /// HTTP client timeout for callback POST requests (milliseconds).
    /// Defaults to 10 000.
    /// </summary>
    public int CallbackTimeoutMs { get; set; } = 10_000;

    /// <summary>
    /// Base delay in milliseconds for exponential backoff between callback retry attempts.
    /// Delay for attempt <c>n</c> (1-based) = <c>CallbackRetryBaseDelayMs × 2^(n-1)</c>.
    /// Set to 0 in test environments to avoid real delays. Defaults to 1000.
    /// </summary>
    public int CallbackRetryBaseDelayMs { get; set; } = 1000;

    /// <summary>
    /// Default aggregate strategy applied to terminal (end) nodes when
    /// no <c>SyncFrame</c> is present. Defaults to <c>"merge"</c>.
    /// </summary>
    public string DefaultAggregateStrategy { get; set; } = "merge";
}
