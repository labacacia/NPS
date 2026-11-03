// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NPS.Core.Caching;
using NPS.Core.Frames;
using NPS.NWP.ActionNode;
using NPS.NWP.Results;

namespace NPS.Demo.DataWorkflow;

/// <summary>Finite deterministic source; one operator update changes one percent of amounts and adds nullable note.</summary>
internal sealed class OrderSource(int scale)
{
    internal static readonly string[] Reasons = ["delivery", "quality", "duplicate", "other"];
    internal static readonly string[] Fields = ["id", "period", "reason", "refunded", "amount_minor", "note"];
    private int _revision = 1;
    internal int Revision => Volatile.Read(ref _revision);
    internal int Scale { get; } = scale is 100 or 1000 or 100000 ? scale : throw new ArgumentOutOfRangeException(nameof(scale));

    internal int Advance()
    {
        if (Interlocked.CompareExchange(ref _revision, 2, 1) != 1)
            throw new ActionNodeException(409, "NPS-CLIENT-CONFLICT", "DEMO-UPDATE-ALREADY-APPLIED", "Update already applied.");
        return 2;
    }

    internal static void Validate(JsonElement parameters)
    {
        if (parameters.ValueKind != JsonValueKind.Object
            || parameters.EnumerateObject().Any(p => p.Name is not ("source_id" or "ttl_seconds" or "filter"))
            || !parameters.TryGetProperty("source_id", out var source) || source.GetString() != "orders") throw Invalid();
        if (parameters.TryGetProperty("ttl_seconds", out var ttl)
            && (!ttl.TryGetInt32(out var seconds) || seconds is < 1 or > 900)) throw Invalid();
        if (!parameters.TryGetProperty("filter", out var filter)) return;
        if (filter.ValueKind != JsonValueKind.Object || filter.EnumerateObject().Count() is < 1 or > 3) throw Invalid();
        foreach (var property in filter.EnumerateObject())
        {
            var value = property.Value;
            if (property.Name == "refunded" && value.ValueKind == JsonValueKind.True) continue;
            if (property.Name == "period" && value.ValueKind == JsonValueKind.String && value.GetString() is "baseline" or "current") continue;
            if (property.Name == "reason" && value.ValueKind == JsonValueKind.String && Reasons.Contains(value.GetString())) continue;
            throw Invalid();
        }
    }

    internal ValueTask<ResultSnapshot> Capture(ResultPrincipal principal, JsonElement parameters, CancellationToken ct)
    {
        try { return CaptureCore(principal, parameters, ct); }
        catch (Exception error) when (Environment.GetEnvironmentVariable("NPS_DEMO_DIAGNOSTICS") == "1")
        {
            Console.Error.WriteLine("source_capture_exception_type:" + error.GetType().FullName);
            throw;
        }
    }

    private ValueTask<ResultSnapshot> CaptureCore(ResultPrincipal principal, JsonElement parameters, CancellationToken ct)
    {
        Validate(parameters);
        var revision = Revision; // One immutable revision for this complete capture.
        var filter = parameters.TryGetProperty("filter", out var value) ? value : default;
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartArray();
            for (var index = 0; index < Scale; index++)
            {
                ct.ThrowIfCancellationRequested();
                var period = index % 100 < 50 ? "baseline" : "current";
                var reason = Reasons[index % 4];
                var refunded = index % 100 < 10 || period == "current" && reason == "delivery" && index % 100 < 80;
                if (filter.ValueKind == JsonValueKind.Object &&
                    (filter.TryGetProperty("refunded", out _) && !refunded
                     || filter.TryGetProperty("period", out var p) && p.GetString() != period
                     || filter.TryGetProperty("reason", out var r) && r.GetString() != reason)) continue;
                writer.WriteStartObject();
                writer.WriteString("id", $"order:{index:D6}");
                writer.WriteString("period", period); writer.WriteString("reason", reason);
                writer.WriteBoolean("refunded", refunded);
                writer.WriteNumber("amount_minor", 1000L + index % 100 * 37 + (revision == 2 && index < Scale / 100 ? 100 : 0));
                if (revision == 2) writer.WriteNull("note");
                writer.WriteEndObject();
            }
            writer.WriteEndArray();
        }
        var fields = new List<SchemaField>
        {
            new("id", "string"), new("period", "string"), new("reason", "string"),
            new("refunded", "bool"), new("amount_minor", "int64"),
        };
        if (revision == 2) fields.Add(new("note", "string", Nullable: true));
        using var document = JsonDocument.Parse(buffer.GetBuffer().AsMemory(0, checked((int)buffer.Length)));
        return ValueTask.FromResult(new ResultSnapshot($"seed-20261102-demo-v{revision}",
            AnchorFrameCache.ComputeAnchorId(new FrameSchema { Fields = fields }), document.RootElement.Clone()));
    }

    private static ActionNodeException Invalid() => ActionNodeException.ParamsInvalid("Unsupported demo source query.");
}
