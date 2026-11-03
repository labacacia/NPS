// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Data.Sqlite;
using NPS.NOP;
using NPS.NOP.Models;
using NPS.NOP.Orchestration;
using NPS.NOP.Runtime;

namespace NPS.Samples.NopDag.Orchestration;

/// <summary>
/// Reference host journal: one exclusively owned local database, FULL synchronous SQLite
/// transactions, caller/task unique identity, immutable digest and terminal CAS. At most
/// 256 permanent task tombstones; capacity rejects admission instead of evicting a promise.
/// No multi-host/shared-network filesystem or durable external-effect exactly-once claim.
/// Opening after exclusive host loss makes every pending task indeterminate without replay.
/// Result TTL hides payloads but never removes replay tombstones. Dispose releases ownership.
/// The host must protect the directory/database as task data; no callback secrets are allowed.
/// </summary>
public sealed class SqliteNopRuntimeJournal : INopRuntimeJournal, IDisposable
{
    private readonly FileStream _ownership;
    private readonly string _connectionString;
    private readonly object _gate = new();
    private readonly TimeProvider _time;
    private readonly Dictionary<(string Caller, string Task), (long Timestamp, long Lifetime)> _terminalClocks = new();
    private long _latestWall;
    private bool _disposed;

    /// <summary>Open an absolute local path; a second live host fails before touching task state.</summary>
    public SqliteNopRuntimeJournal(string path, bool initializeNewFile = false, TimeProvider? timeProvider = null)
    {
        if (!Path.IsPathFullyQualified(path)) throw new ArgumentException("Journal path must be absolute.");
        _time = timeProvider ?? TimeProvider.System;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (initializeNewFile && File.Exists(path)) throw new NopRuntimeException(NopErrorCodes.ClaimConflict);
        _ownership = new FileStream(path + ".owner", initializeNewFile ? FileMode.CreateNew : FileMode.Open,
            FileAccess.ReadWrite, FileShare.None);
        _connectionString = new SqliteConnectionStringBuilder
            { DataSource = path, Mode = SqliteOpenMode.ReadWrite, DefaultTimeout = 1, Pooling = false }.ToString();
        try
        {
            if (initializeNewFile)
            {
                using (File.Open(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None)) { }
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                    File.SetUnixFileMode(path + ".owner", UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
            if (!File.Exists(path)) throw new NopRuntimeException(NopErrorCodes.ClaimConflict);
            using var connection = Open();
            using var schema = connection.CreateCommand();
            schema.CommandText = initializeNewFile ? """
                PRAGMA journal_mode=DELETE;
                CREATE TABLE nop_tasks(
                  caller TEXT NOT NULL, task TEXT NOT NULL, digest TEXT NOT NULL,
                  owner TEXT NOT NULL, request TEXT NOT NULL, result TEXT, completed_at INTEGER,
                  PRIMARY KEY(caller,task));
                """ : "SELECT caller,task,digest,owner,request,result,completed_at FROM nop_tasks LIMIT 0";
            schema.ExecuteNonQuery();
            using var transaction = connection.BeginTransaction(deferred: false);
            using var read = connection.CreateCommand(); read.Transaction = transaction;
            read.CommandText = "SELECT caller,task FROM nop_tasks WHERE result IS NULL";
            var pending = new List<(string Caller, string Task)>();
            using (var reader = read.ExecuteReader())
                while (reader.Read()) pending.Add((reader.GetString(0), reader.GetString(1)));
            foreach (var item in pending)
            {
                var result = new NopTaskResult { TaskId = item.Task, FinalState = TaskState.Failed,
                    ExecutionOutcome = NopExecutionOutcome.Indeterminate, ErrorCode = NopErrorCodes.ExecutionIndeterminate,
                    ErrorMessage = "Previous host stopped without authoritative terminal evidence." };
                using var update = connection.CreateCommand(); update.Transaction = transaction;
                update.CommandText = "UPDATE nop_tasks SET result=$result,completed_at=$at WHERE caller=$caller AND task=$task AND result IS NULL";
                update.Parameters.AddWithValue("$result", JsonSerializer.Serialize(result));
                update.Parameters.AddWithValue("$at", Now());
                update.Parameters.AddWithValue("$caller", item.Caller); update.Parameters.AddWithValue("$task", item.Task);
                update.ExecuteNonQuery();
            }
            transaction.Commit();
        }
        catch { _ownership.Dispose(); throw; }
    }

    /// <inheritdoc/>
    public NopRuntimeAdmission? ReadReplay(string callerNid, string taskId, string digest)
    {
        lock (_gate)
        {
            ThrowDisposed();
            using var connection = Open(); using var read = connection.CreateCommand();
            read.CommandText = "SELECT digest,request,result,completed_at FROM nop_tasks WHERE caller=$caller AND task=$task";
            read.Parameters.AddWithValue("$caller",callerNid); read.Parameters.AddWithValue("$task",taskId);
            using var reader = read.ExecuteReader();
            if (!reader.Read()) return null;
            if (reader.GetString(0) != digest) throw new NopRuntimeException(NopErrorCodes.ReplayConflict);
            return new(null,reader.IsDBNull(2) ? null : Retained(callerNid,taskId,
                JsonSerializer.Deserialize<NopRuntimeRequest>(reader.GetString(1))!,
                JsonSerializer.Deserialize<NopTaskResult>(reader.GetString(2))!,reader.GetInt64(3)));
        }
    }

    /// <inheritdoc/>
    public NopRuntimeAdmission Admit(NopRuntimeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.CallerNid) || request.CallerNid.Length > 512
            || request.Digest.Length != 64 || request.Operations.Count is < 1 or > 64)
            throw new NopRuntimeException(NopErrorCodes.TaskDagInvalid);
        var encoded = JsonSerializer.Serialize(request);
        if (encoded.Length > 262144) throw new NopRuntimeException(NopErrorCodes.ReplayLimit);
        lock (_gate)
        {
            ThrowDisposed();
            using var connection = Open(); using var transaction = connection.BeginTransaction(deferred: false);
            using var query = connection.CreateCommand(); query.Transaction = transaction;
            query.CommandText = "SELECT digest,request,result,completed_at FROM nop_tasks WHERE caller=$caller AND task=$task";
            query.Parameters.AddWithValue("$caller", request.CallerNid); query.Parameters.AddWithValue("$task", request.Task.TaskId);
            using (var reader = query.ExecuteReader())
            {
                if (reader.Read())
                {
                    if (reader.GetString(0) != request.Digest) throw new NopRuntimeException(NopErrorCodes.ReplayConflict);
                    var result = reader.IsDBNull(2) ? null : Retained(request.CallerNid, request.Task.TaskId,
                        JsonSerializer.Deserialize<NopRuntimeRequest>(reader.GetString(1))!,
                        JsonSerializer.Deserialize<NopTaskResult>(reader.GetString(2))!, reader.GetInt64(3));
                    return new(null, result);
                }
            }
            using var count = connection.CreateCommand(); count.Transaction = transaction;
            count.CommandText = "SELECT COUNT(*) FROM nop_tasks";
            if (Convert.ToInt64(count.ExecuteScalar()) >= 256) throw new NopRuntimeException(NopErrorCodes.ReplayLimit);
            var owner = Guid.NewGuid().ToString("D");
            using var insert = connection.CreateCommand(); insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO nop_tasks(caller,task,digest,owner,request) VALUES($caller,$task,$digest,$owner,$request)";
            insert.Parameters.AddWithValue("$caller", request.CallerNid); insert.Parameters.AddWithValue("$task", request.Task.TaskId);
            insert.Parameters.AddWithValue("$digest", request.Digest); insert.Parameters.AddWithValue("$owner", owner);
            insert.Parameters.AddWithValue("$request", encoded); insert.ExecuteNonQuery(); transaction.Commit();
            return new(owner, null);
        }
    }

    /// <inheritdoc/>
    public NopTaskResult Commit(string callerNid, string taskId, string ownerToken, NopTaskResult result)
    {
        if (result.TaskId != taskId || result.FinalState is not (TaskState.Completed or TaskState.Failed or TaskState.Cancelled)
            || result.ExecutionOutcome is not (NopExecutionOutcome.KnownTerminal or NopExecutionOutcome.NotDispatched or NopExecutionOutcome.Indeterminate))
            throw new NopRuntimeException(NopErrorCodes.TaskDagInvalid);
        var encoded = JsonSerializer.Serialize(result);
        if (encoded.Length > 1048576) throw new NopRuntimeException(NopErrorCodes.ReplayLimit);
        lock (_gate)
        {
            ThrowDisposed();
            using var connection = Open(); using var transaction = connection.BeginTransaction(deferred: false);
            using var read = connection.CreateCommand(); read.Transaction = transaction;
            read.CommandText = "SELECT owner,request,result,completed_at FROM nop_tasks WHERE caller=$caller AND task=$task";
            read.Parameters.AddWithValue("$caller", callerNid); read.Parameters.AddWithValue("$task", taskId);
            NopRuntimeRequest request;
            using (var reader = read.ExecuteReader())
            {
                if (!reader.Read() || reader.GetString(0) != ownerToken) throw new NopRuntimeException(NopErrorCodes.ClaimConflict);
                request = JsonSerializer.Deserialize<NopRuntimeRequest>(reader.GetString(1))!;
                if (!reader.IsDBNull(2)) return Retained(callerNid, taskId, request,
                    JsonSerializer.Deserialize<NopTaskResult>(reader.GetString(2))!, reader.GetInt64(3));
            }
            var now = Now();
            using var update = connection.CreateCommand(); update.Transaction = transaction;
            update.CommandText = "UPDATE nop_tasks SET result=$result,completed_at=$at WHERE caller=$caller AND task=$task AND owner=$owner AND result IS NULL";
            update.Parameters.AddWithValue("$result", encoded); update.Parameters.AddWithValue("$at", now);
            update.Parameters.AddWithValue("$caller", callerNid); update.Parameters.AddWithValue("$task", taskId);
            update.Parameters.AddWithValue("$owner", ownerToken);
            if (update.ExecuteNonQuery() != 1) throw new NopRuntimeException(NopErrorCodes.ClaimConflict);
            transaction.Commit();
            _terminalClocks[(callerNid, taskId)] = (_time.GetTimestamp(), request.Task.ResultTtlSeconds);
            return JsonSerializer.Deserialize<NopTaskResult>(encoded)!;
        }
    }

    /// <inheritdoc/>
    public NopTaskRecord? GetStatus(string callerNid, string taskId)
    {
        lock (_gate)
        {
            ThrowDisposed();
            using var connection = Open(); using var read = connection.CreateCommand();
            read.CommandText = "SELECT request,result,completed_at FROM nop_tasks WHERE caller=$caller AND task=$task";
            read.Parameters.AddWithValue("$caller", callerNid); read.Parameters.AddWithValue("$task", taskId);
            using var reader = read.ExecuteReader();
            if (!reader.Read()) return null;
            var request = JsonSerializer.Deserialize<NopRuntimeRequest>(reader.GetString(0))!;
            var result = reader.IsDBNull(1) ? null : Retained(callerNid, taskId, request,
                JsonSerializer.Deserialize<NopTaskResult>(reader.GetString(1))!, reader.GetInt64(2));
            return new() { TaskId = taskId, Frame = request.Task, StartedAt = request.StartedAt.UtcDateTime,
                State = result?.FinalState ?? TaskState.Running,
                CompletedAt = reader.IsDBNull(2) ? null : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(2)).UtcDateTime };
        }
    }

    private NopTaskResult Retained(string caller, string task, NopRuntimeRequest request, NopTaskResult result, long completed)
    {
        if (Now() >= completed + request.Task.ResultTtlSeconds
            || _terminalClocks.TryGetValue((caller, task), out var clock)
                && _time.GetElapsedTime(clock.Timestamp).TotalSeconds >= clock.Lifetime)
            throw new NopRuntimeException(NopErrorCodes.TaskResultExpired);
        return result;
    }

    private long Now() => _latestWall = Math.Max(_latestWall, _time.GetUtcNow().ToUnixTimeSeconds());
    private SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString); connection.Open();
        using var command = connection.CreateCommand(); command.CommandText = "PRAGMA synchronous=FULL; PRAGMA busy_timeout=1000;";
        command.ExecuteNonQuery(); return connection;
    }
    private void ThrowDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(SqliteNopRuntimeJournal)); }
    /// <summary>Release exclusive host ownership after owned execution has stopped.</summary>
    public void Dispose() { lock (_gate) { _disposed = true; _ownership.Dispose(); } }
}
