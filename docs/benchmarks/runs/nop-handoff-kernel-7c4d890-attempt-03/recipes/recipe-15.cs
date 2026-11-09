// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using NPS.NOP.Frames;
using NPS.NOP.Orchestration;

namespace NPS.NOP.Runtime;

/// <summary>
/// Explicit experimental host selection of bounded NOP execution. The caller is derived
/// from current authenticated host admission, never task/model text. One binding belongs
/// to one caller; the journal and worker transport must enforce their documented trust
/// contracts. Selecting this API does not advertise full production_runtime support.
/// </summary>
public sealed record NopRuntimeBinding(string CallerNid, INopRuntimeJournal Journal,
    INopAuthenticatedWorkerClient Worker);

/// <summary>
/// Transport boundary which independently authenticates current worker channel/NIP
/// identity and authorization before emitting any frame. Implementations must bound
/// response sizes and must never attest identity from SenderNid/forwarded headers.
/// Cancellation requests cessation, not rollback; dispatch failures may be unknown.
/// </summary>
public interface INopAuthenticatedWorkerClient
{
    /// <summary>Dispatch one stable operation; report the actual authenticated peer with each frame.</summary>
    IAsyncEnumerable<NopAuthenticatedFrame> DelegateAsync(DelegateFrame frame, CancellationToken ct);
}

/// <summary>Transport-attested peer identity, distinct from the untrusted frame's sender claim.</summary>
public sealed record NopAuthenticatedFrame(string PeerNid, AlignStreamFrame Frame);

/// <summary>Stable identities persisted atomically with task admission before any dispatch.</summary>
public sealed record NopOperationIdentity(string SubtaskId, string IdempotencyKey);

/// <summary>
/// Immutable admission request. Digest is a value compared under the unique
/// (CallerNid, TaskId) key; it is not a third key component. No request contains secrets.
/// </summary>
public sealed record NopRuntimeRequest(string CallerNid, TaskFrame Task, string Digest,
    DateTimeOffset StartedAt, DateTimeOffset DeadlineAt, IReadOnlyDictionary<string, NopOperationIdentity> Operations);

/// <summary>
/// Atomic journal receipt. OwnerToken is returned ONLY to the newly admitted executor.
/// Existing pending requests return no token/result; they must not start work again.
/// </summary>
public sealed record NopRuntimeAdmission(string? OwnerToken, NopTaskResult? Result);

/// <summary>
/// Host-owned atomic replay/terminal journal, not the legacy in-memory task store.
/// Admit must compare digest, enforce capacity/retention, persist all operation IDs before
/// returning ownership, and return recorded running/terminal outcomes to duplicates.
/// Commit must authenticate the owner token and compare-and-set exactly one terminal;
/// subsequent/late commits return the first result. All reads are caller-scoped. Storage
/// failures throw and forbid fallback/dispatch. Durable implementations must document
/// restart/replica ownership; replay caches alone do not prove durable effect deduplication.
/// This narrow reference rejects callbacks/compensation before admission.
/// </summary>
public interface INopRuntimeJournal
{
    /// <summary>Atomically compare an existing replay without admitting invalid work; absent returns null.</summary>
    NopRuntimeAdmission? ReadReplay(string callerNid, string taskId, string digest);
    /// <summary>Atomically admit or retrieve; conflicting immutable requests throw NOP-REPLAY-CONFLICT.</summary>
    NopRuntimeAdmission Admit(NopRuntimeRequest request);
    /// <summary>Commit one immutable terminal record with the original admission ownership token.</summary>
    NopTaskResult Commit(string callerNid, string taskId, string ownerToken, NopTaskResult result);
    /// <summary>Read a caller-owned record; pending has no terminal result, absent returns null.</summary>
    NopTaskRecord? GetStatus(string callerNid, string taskId);
}

/// <summary>Known fail-closed admission/journal rejection; messages must contain no task data.</summary>
public sealed class NopRuntimeException(string code) : Exception(code)
{
    /// <summary>Stable protocol error code.</summary>
    public string Code { get; } = code;
}

/// <summary>Execution certainty, separate from completed/failed/cancelled task state.</summary>
public static class NopExecutionOutcome
{
    /// <summary>Authoritative evidence that the operation was not dispatched.</summary>
    public const string NotDispatched = "not_dispatched";
    /// <summary>Authoritative terminal evidence exists; this does not promise rollback.</summary>
    public const string KnownTerminal = "known_terminal";
    /// <summary>Effects are unresolved; do not automatically replay or compensate.</summary>
    public const string Indeterminate = "indeterminate";
}
