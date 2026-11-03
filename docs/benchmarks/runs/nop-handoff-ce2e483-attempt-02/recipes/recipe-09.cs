// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NPS.Core.Alpha20;
using NPS.NIP.Invocation;
using NPS.NWP.ActionNode;
using NPS.NWP.Results;

namespace NPS.Tests.Nwp;

// D20-01 baseline and counters belong only to this loopback measurement fixture.
// Both paths reuse the same source/store operators so representation/framing, not
// a deliberately weaker arithmetic implementation, explains observed differences.
public sealed partial class ResultInvocationTlsTests
{
    private bool _gzipResponses;
    private TlsTraffic? _downstreamTraffic;
    private int _gzipResponseCount;

    private async Task HandleOrdinaryRuntime(HttpContext context)
    {
        if (context.Request.Method != "POST" || _oracleScale == 0) { context.Response.StatusCode = 404; return; }
        try
        {
            var peer = context.Connection.ClientCertificate;
            if (!context.Request.IsHttps || peer is null
                || peer.GetCertHashString(HashAlgorithmName.SHA256) != _ownerCertificate.GetCertHashString(HashAlgorithmName.SHA256)
                || !await CurrentNipIdentityValid(_aliceIdentity, context.RequestAborted))
                throw new NipInvocationException("NIP-CONTEXT-UNTRUSTED");
            var principal = new ResultPrincipal("fixture-tenant", Alice, "v1");
            using var buffer = new MemoryStream();
            var bytes = new byte[8192];
            int length;
            while ((length = await context.Request.Body.ReadAsync(bytes, context.RequestAborted)) != 0)
            {
                if (buffer.Length + length > 65536) throw ActionNodeException.ParamsInvalid("Request exceeds bound.");
                buffer.Write(bytes, 0, length);
            }
            var raw = System.Text.Encoding.UTF8.GetString(buffer.ToArray());
            var body = NipInvocationJson.Parse(raw, 65536);
            var operation = context.Request.Path.Value!.Split('/').Last();
            string Text(string key) => body.GetProperty(key).GetString()!;
            object payload;
            switch (operation)
            {
                case "create":
                    var sourceParameters = body.GetProperty("source");
                    ValidateFixtureQuery(sourceParameters);
                    if (sourceParameters.GetProperty("source_id").GetString() != "orders")
                        throw ActionNodeException.ParamsInvalid("Unsupported source.");
                    var digest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(NipInvocationJson.Canonicalize(body)));
                    payload = await _store.CreateAsync(principal, "orders", Text("key"), digest, _ =>
                    {
                        Interlocked.Increment(ref _captures);
                        return ValueTask.FromResult(OracleCapture(sourceParameters));
                    }, ct: context.RequestAborted, toModel: _toModel);
                    break;
                case "aggregate":
                    var fields = body.GetProperty("group_by").EnumerateArray().Select(value => value.GetString()!).ToArray();
                    var result = _store.Aggregate(principal, Text("result_ref"), Text("snapshot_id"), Text("schema_ref"), fields,
                        body.TryGetProperty("sum_field", out var sum) && sum.ValueKind == JsonValueKind.String ? sum.GetString() : null,
                        ct: context.RequestAborted, toModel: _toModel);
                    payload = new ResultAggregate(result.Descriptor, result.Groups.Select(row =>
                    {
                        var named = fields.Zip(row.GetProperty("keys").EnumerateArray()).ToDictionary(pair => pair.First, pair => pair.Second);
                        named.Add("count", row.GetProperty("count"));
                        if (row.TryGetProperty("sum", out var amount)) named.Add("sum", amount);
                        return JsonSerializer.SerializeToElement(named);
                    }).ToArray());
                    break;
                case "read":
                    payload = _store.Read(principal, Text("result_ref"), Text("snapshot_id"), Text("schema_ref"),
                        body.GetProperty("limit").GetInt32(),
                        body.GetProperty("fields").EnumerateArray().Select(value => value.GetString()!).ToArray(), ct: context.RequestAborted, toModel: _toModel);
                    break;
                case "release": payload = _store.Release(principal, Text("result_ref"), _toModel); break;
                default: context.Response.StatusCode = 404; return;
            }
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(payload), context.RequestAborted);
        }
        catch (ActionNodeException error) { await OrdinaryError(context, error.HttpStatus, error.NpsStatus, error.ErrorCode); }
        catch (NipInvocationException error)
        {
            var mapping = Alpha20ErrorStatus.Mappings[error.Code];
            await OrdinaryError(context, mapping.HttpStatus, mapping.NpsStatus, error.Code);
        }
    }

    private static Task OrdinaryError(HttpContext context, int status, string npsStatus, string code)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(new { status = npsStatus, error = code }));
    }

    private RuntimeResultPath RuntimePath(bool nps, TlsTraffic traffic)
    {
        if (nps) return new(PublicClient(traffic: traffic));
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false, UseCookies = false };
        ConfigureTls(handler, _ownerCertificate, traffic);
        return new(new HttpClient(handler) { BaseAddress = new Uri(_endpoint, "/ordinary/"), Timeout = TimeSpan.FromSeconds(120),
            DefaultRequestVersion = HttpVersion.Version11, DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact });
    }

    private sealed class RuntimeResultPath : IDisposable
    {
        private readonly HttpClient? _ordinary;
        private readonly ResultHttpClient? _nps;
        internal string LastOperation { get; private set; } = "not-started";
        internal RuntimeResultPath(HttpClient ordinary) => _ordinary = ordinary;
        internal RuntimeResultPath(ResultHttpClient nps) => _nps = nps;

        internal Task<ResultDescriptor> Create(bool refunded = false)
        {
            LastOperation = "result.create";
            return _nps is not null
            ? _nps.CreateAsync("orders", Guid.NewGuid(), refunded ? JsonSerializer.SerializeToElement(new { filter = new { refunded = true } }) : null)
            : Post<ResultDescriptor>("create", new { key = Guid.NewGuid().ToString("D"), source = refunded
                ? JsonSerializer.SerializeToElement(new { source_id = "orders", filter = new { refunded = true } })
                : JsonSerializer.SerializeToElement(new { source_id = "orders" }) });
        }
        internal Task<ResultAggregate> Aggregate(ResultDescriptor result, bool sum)
        {
            LastOperation = "result.aggregate";
            return _nps is not null
            ? _nps.AggregateAsync(result, ["reason", "period"], sum ? "amount_minor" : null)
            : Post<ResultAggregate>("aggregate", new { result_ref = result.ResultRef, snapshot_id = result.SnapshotId,
                schema_ref = result.SchemaRef, group_by = new[] { "reason", "period" }, sum_field = sum ? "amount_minor" : null });
        }
        internal Task<ResultPage> Read(ResultDescriptor result)
        {
            LastOperation = "result.read";
            return _nps is not null
            ? _nps.ReadAsync(result, 100, ["id", "period", "reason", "refunded", "amount_minor"])
            : Post<ResultPage>("read", new { result_ref = result.ResultRef, snapshot_id = result.SnapshotId,
                schema_ref = result.SchemaRef, limit = 100, fields = new[] { "id", "period", "reason", "refunded", "amount_minor" } });
        }
        internal async Task Release(ResultDescriptor result)
        {
            LastOperation = "result.release";
            var receipt = _nps is not null ? await _nps.ReleaseAsync(result)
                : await Post<ResultRelease>("release", new { result_ref = result.ResultRef });
            Assert.True(receipt.Released);
            Assert.Equal(result.ResultRef, receipt.ResultRef);
        }
        private async Task<T> Post<T>(string operation, object parameters)
        {
            using var response = await _ordinary!.PostAsJsonAsync(operation, parameters);
            if (response.StatusCode != HttpStatusCode.OK)
                throw new ResultHttpException("Ordinary runtime request failed; wire status retained without inferring a protocol code.",
                    (int)response.StatusCode >= 500, (int)response.StatusCode);
            Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
            await using var stream = await response.Content.ReadAsStreamAsync();
            using var bounded = new MemoryStream();
            var bytes = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(bytes)) != 0)
            {
                Assert.True(bounded.Length + read <= 131072);
                bounded.Write(bytes, 0, read);
            }
            return JsonSerializer.Deserialize<T>(bounded.ToArray())!;
        }
        public void Dispose() { _ordinary?.Dispose(); _nps?.Dispose(); }
    }

    // This wrapper sits BELOW SslStream: counts TCP-stream bytes including TLS records
    // and HTTP framing, without recording content. TCP/IP packet overhead is excluded.
    private sealed class TlsTraffic
    {
        private long _read, _written;
        internal long Total => Interlocked.Read(ref _read) + Interlocked.Read(ref _written);
        internal void Configure(SocketsHttpHandler handler) => handler.ConnectCallback = async (context, ct) =>
        {
            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(context.DnsEndPoint, ct);
                return new CountedStream(new NetworkStream(socket, ownsSocket: true), this);
            }
            catch { socket.Dispose(); throw; }
        };

        private sealed class CountedStream(Stream inner, TlsTraffic counter) : Stream
        {
            public override bool CanRead => inner.CanRead;
            public override bool CanWrite => inner.CanWrite;
            public override bool CanSeek => false;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => inner.Flush();
            public override Task FlushAsync(CancellationToken ct) => inner.FlushAsync(ct);
            public override int Read(byte[] bytes, int offset, int count)
            {
                var read = inner.Read(bytes, offset, count);
                Interlocked.Add(ref counter._read, read);
                return read;
            }
            public override async ValueTask<int> ReadAsync(Memory<byte> bytes, CancellationToken ct = default)
            {
                var read = await inner.ReadAsync(bytes, ct);
                Interlocked.Add(ref counter._read, read);
                return read;
            }
            public override void Write(byte[] bytes, int offset, int count)
            {
                inner.Write(bytes, offset, count);
                Interlocked.Add(ref counter._written, count);
            }
            public override async ValueTask WriteAsync(ReadOnlyMemory<byte> bytes, CancellationToken ct = default)
            {
                await inner.WriteAsync(bytes, ct);
                Interlocked.Add(ref counter._written, bytes.Length);
            }
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
            public override async ValueTask DisposeAsync() { await inner.DisposeAsync(); GC.SuppressFinalize(this); }
        }
    }
}
