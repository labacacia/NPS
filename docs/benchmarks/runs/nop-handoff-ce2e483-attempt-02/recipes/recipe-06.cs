// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NPS.Core.Caching;
using NPS.Core.Frames;
using NPS.NWP.ActionNode;
using NPS.NWP.Results;

namespace NPS.Tests.Nwp;

// D20-02 executes the frozen source oracle through the supported client and real
// authenticated gateway/receiver. No model/provider call or D20-05 demo is added.
public sealed partial class ResultInvocationTlsTests
{
    private int _oracleScale;
    private int _cachedOracleScale;
    private int _oracleUpdatedRows, _cachedOracleUpdatedRows;
    private bool _oracleAddedNullableField, _cachedOracleAddedNullableField;
    private ResultSnapshot? _cachedAllOrders, _cachedRefunds;
    private static readonly string[] Reasons = ["delivery", "quality", "duplicate", "other"];

    private void ValidateFixtureQuery(JsonElement parameters)
    {
        if (parameters.EnumerateObject().Any(property => property.Name is not ("source_id" or "ttl_seconds")
            && !(_oracleScale != 0 && property.Name == "filter")))
            throw ActionNodeException.ParamsInvalid("Unsupported source query.");
        if (parameters.TryGetProperty("ttl_seconds", out var ttl)
            && (ttl.ValueKind != JsonValueKind.Number || !ttl.TryGetInt32(out var seconds) || seconds is < 1 or > 900))
            throw ActionNodeException.ParamsInvalid("Invalid retention bound.");
        if (parameters.TryGetProperty("filter", out var filter)
            && (filter.ValueKind != JsonValueKind.Object || filter.EnumerateObject().Count() != 1
                || !filter.TryGetProperty("refunded", out var refunded) || refunded.ValueKind != JsonValueKind.True))
            throw ActionNodeException.ParamsInvalid("Only the registered refunded=true filter is supported.");
    }

    private ResultSnapshot OracleCapture(JsonElement parameters)
    {
        var onlyRefunds = parameters.TryGetProperty("filter", out _);
        if (_cachedOracleScale == _oracleScale && _cachedOracleUpdatedRows == _oracleUpdatedRows
            && _cachedOracleAddedNullableField == _oracleAddedNullableField
            && (onlyRefunds ? _cachedRefunds : _cachedAllOrders) is { } cached)
            return cached; // Fixture source is immutable; the store still copies each capture.
        if (_cachedOracleScale != _oracleScale || _cachedOracleUpdatedRows != _oracleUpdatedRows
            || _cachedOracleAddedNullableField != _oracleAddedNullableField)
        {
            _cachedOracleScale = _oracleScale;
            _cachedOracleUpdatedRows = _oracleUpdatedRows;
            _cachedOracleAddedNullableField = _oracleAddedNullableField;
            _cachedAllOrders = _cachedRefunds = null;
        }
        var rows = Enumerable.Range(0, _oracleScale).Select(index =>
        {
            var period = index % 100 < 50 ? "baseline" : "current";
            var reason = Reasons[index % 4];
            return new
            {
                id = $"order:{index:D6}", period, reason,
                refunded = index % 100 < 10 || period == "current" && reason == "delivery" && index % 100 < 80,
                amount_minor = 1000 + index % 100 * 37 + (index < _oracleUpdatedRows ? 100 : 0),
            };
        }).Where(row => !onlyRefunds || row.refunded);
        var fields = new List<SchemaField>
        {
            new("id", "string"), new("period", "string"), new("reason", "string"),
            new("refunded", "bool"), new("amount_minor", "int64"),
        };
        if (_oracleAddedNullableField) fields.Add(new("note", "string", Nullable: true));
        var schema = new FrameSchema { Fields = fields };
        var sourceVersion = _oracleUpdatedRows == 0 && !_oracleAddedNullableField ? "seed-20261102-immutable-v1"
            : $"seed-20261102-update-{_oracleUpdatedRows}-nullable-{(_oracleAddedNullableField ? 1 : 0)}";
        var payload = _oracleAddedNullableField
            ? JsonSerializer.SerializeToElement(rows.Select(row => new
                { row.id, row.period, row.reason, row.refunded, row.amount_minor, note = (string?)null }))
            : JsonSerializer.SerializeToElement(rows);
        var snapshot = new ResultSnapshot(sourceVersion, AnchorFrameCache.ComputeAnchorId(schema), payload);
        if (onlyRefunds) _cachedRefunds = snapshot; else _cachedAllOrders = snapshot;
        return snapshot;
    }

    [Theory]
    [InlineData(100)]
    [InlineData(1000)]
    [InlineData(100000)]
    public async Task FrozenRefundWorkflowMatchesExactOracleThroughSupportedPublicClient(int scale)
    {
        _oracleScale = scale;
        _policy.SetSourceAuthority(Authority() with { Fields = ["id", "period", "reason", "refunded", "amount_minor"] });
        using var client = PublicClient();
        var source = await client.CreateAsync("orders", Guid.NewGuid());
        Assert.Equal(scale, source.RowCount);
        var orderCounts = await client.AggregateAsync(source, ["reason", "period"]);
        var query = JsonSerializer.SerializeToElement(new { filter = new { refunded = true } });
        var refunds = await client.CreateAsync("orders", Guid.NewGuid(), query);
        var refundTotals = await client.AggregateAsync(refunds, ["reason", "period"], "amount_minor");
        Assert.Equal(source.SchemaRef, refunds.SchemaRef);
        Assert.NotEqual(source.SnapshotId, refunds.SnapshotId);
        var page = await client.ReadAsync(refunds, 100, ["id", "period", "reason", "refunded", "amount_minor"]);
        var expected = FrozenMethod().GetProperty("reference_answers").GetProperty(scale.ToString());
        var anomalies = new List<string>();
        foreach (var reason in Reasons)
        {
            var observed = new Dictionary<string, (long Orders, long Refunds, long Amount)>();
            foreach (var period in new[] { "baseline", "current" })
            {
                static JsonElement? Group(ResultAggregate aggregate, string reason, string period) =>
                    aggregate.Groups.Cast<JsonElement?>().SingleOrDefault(value => value!.Value.GetProperty("reason").GetString() == reason
                        && value.Value.GetProperty("period").GetString() == period);
                var orders = Group(orderCounts, reason, period)?.GetProperty("count").GetInt64() ?? 0;
                var count = Group(refundTotals, reason, period)?.GetProperty("count").GetInt64() ?? 0;
                var amount = Group(refundTotals, reason, period)?.GetProperty("sum").GetInt64() ?? 0;
                var oracle = expected.GetProperty("groups").GetProperty(reason).GetProperty(period);
                Assert.Equal(oracle.GetProperty("orders").GetInt64(), orders);
                Assert.Equal(oracle.GetProperty("refunds").GetInt64(), count);
                Assert.Equal(oracle.GetProperty("refund_amount_minor").GetInt64(), amount);
                observed.Add(period, (orders, count, amount));
            }
            var baseline = observed["baseline"];
            var current = observed["current"];
            if (Math.Min(baseline.Orders, current.Orders) >= 10 && current.Refunds >= 3
                && checked(10 * (current.Refunds * baseline.Orders - baseline.Refunds * current.Orders))
                >= current.Orders * baseline.Orders) anomalies.Add(reason);
            var samples = page.Rows.Where(row => row.GetProperty("period").GetString() == "current"
                && row.GetProperty("reason").GetString() == reason).Take(5).ToArray();
            Assert.Equal(expected.GetProperty("groups").GetProperty(reason).GetProperty("sample_current_refund_ids")
                .EnumerateArray().Select(value => value.GetString()), samples.Select(row => row.GetProperty("id").GetString()));
            Assert.All(samples, row => Assert.True(row.GetProperty("refunded").GetBoolean()));
        }
        Assert.Equal(expected.GetProperty("anomaly_reasons").EnumerateArray().Select(value => value.GetString()), anomalies);
        var visible = JsonSerializer.SerializeToUtf8Bytes(new
        {
            order_counts = orderCounts, refund_totals = refundTotals,
            source_rows = page.Rows.Where(row => row.GetProperty("period").GetString() == "current").Take(5),
            citations = page.Rows.Where(row => row.GetProperty("period").GetString() == "current").Take(5).Select(row => new
            {
                result_ref = refunds.ResultRef, snapshot_id = refunds.SnapshotId, record_id = row.GetProperty("id").GetString(),
            }),
        });
        Assert.True(visible.Length <= FrozenMethod().GetProperty("evidence").GetProperty("max_model_visible_bytes").GetInt32());
        // Existing direct read bindings cannot silently cross the two captured snapshots.
        var mismatch = await Assert.ThrowsAsync<ResultHttpException>(() => client.ReadAsync(refunds with { SnapshotId = source.SnapshotId }, 1));
        Assert.Equal(409, mismatch.HttpStatus);
        Assert.Equal("NWP-RESULT-SNAPSHOT-MISMATCH", mismatch.ErrorCode);
        Assert.True((await client.ReleaseAsync(refunds)).Released);
        Assert.True((await client.ReleaseAsync(source)).Released);
        Assert.Equal(2, _captures);
    }

    private static JsonElement FrozenMethod()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "spec/conformance/alpha20/data_workflow_methodology.json");
            if (!File.Exists(path)) continue;
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.Clone();
        }
        throw new FileNotFoundException("Frozen data-workflow methodology is required.");
    }
}
