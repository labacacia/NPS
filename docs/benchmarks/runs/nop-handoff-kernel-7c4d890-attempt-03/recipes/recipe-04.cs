// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;
using System.Text.Json;
using NPS.NOP;
using NPS.NOP.Frames;
using NPS.NOP.Models;
using NPS.NOP.Orchestration;
using NPS.NOP.Runtime;
using NPS.NOP.Storage;
using NPS.NWP.Results;
using NPS.Samples.NopDag.Orchestration;

namespace NPS.Tests.Nwp;

// Worker roles dispatch locally under host policy. Every data operation crosses real
// mTLS/current NIP/gateway/receiver admission; this does not qualify remote NOP transport.
public sealed partial class ResultInvocationTlsTests
{
    [Theory]
    [InlineData(100)][InlineData(1000)][InlineData(100000)]
    public async Task NopWorkersExchangeAuthorizedReferencesWithoutBulkModelContext(int scale)
    {
        var observation=await RunResultHandoff(scale,null);
        Assert.Equal(TaskState.Completed,observation.Result.FinalState);
        Assert.Equal(NopExecutionOutcome.KnownTerminal,observation.Result.ExecutionOutcome);
        Assert.Equal(4,observation.Dispatches);
        Assert.Equal(0,observation.ModelVisibleBytes);
        Assert.InRange(observation.DelegateBytes,1,16384);
        Assert.InRange(observation.AlignBytes,1,32768);
        Assert.True(observation.PublicTlsBytes>observation.DelegateBytes);
        Assert.Equal(2,observation.Released);
        Assert.Equal(0,observation.UnresolvedCreates);
        Assert.Equal(2,_captures); Assert.Equal(7,_handlers);
    }

    [Theory]
    [InlineData("missing-grant")][InlineData("revoked-grant")][InlineData("hidden-field")]
    [InlineData("revoked-worker")][InlineData("expired-input")][InlineData("lost-query-response")][InlineData("create-audit-loss")]
    public async Task NopHandoffRejectsUnprovenOrUnauthorizedInputAndDoesNotRunReport(string fault)
    {
        var observation=await RunResultHandoff(100,fault);
        Assert.Equal(TaskState.Failed,observation.Result.FinalState);
        Assert.True(observation.Dispatches<4);
        Assert.Equal(fault is "lost-query-response" or "create-audit-loss"?NopExecutionOutcome.Indeterminate:NopExecutionOutcome.KnownTerminal,
            observation.Result.ExecutionOutcome);
        Assert.Empty(observation.Result.NodeResults);
        Assert.Equal(0,observation.ModelVisibleBytes);
        var expected=fault switch
        {
            "missing-grant" or "revoked-grant" => (3,2,4,2,0,"NWP-RESULT-NOT-FOUND"),
            "hidden-field" => (3,2,5,2,0,"NWP-RESULT-NOT-FOUND"),
            "revoked-worker" => (3,2,4,2,0,"NIP-CONTEXT-UNTRUSTED"),
            "expired-input" => (3,2,2,0,0,"NWP-RESULT-NOT-FOUND"),
            "lost-query-response" => (1,1,2,1,0,NopErrorCodes.ExecutionIndeterminate),
            "create-audit-loss" => (1,1,1,0,1,NopErrorCodes.ExecutionIndeterminate),
            _ => throw new InvalidOperationException(),
        };
        Assert.Equal(expected,(observation.Dispatches,_captures,_handlers,observation.Released,observation.UnresolvedCreates,observation.Result.ErrorCode));
    }

    private sealed record HandoffObservation(NopTaskResult Result,int Dispatches,int ModelVisibleBytes,
        long DelegateBytes,long AlignBytes,long PublicTlsBytes,long GatewayReceiverTlsBytes,int Released,int UnresolvedCreates);

    private async Task<HandoffObservation> RunResultHandoff(int scale,string? fault)
    {
        _oracleScale=scale;
        _policy.SetSourceAuthority(Authority() with { Fields=["id","period","reason","refunded","amount_minor"] });
        var ownerPrincipal=new ResultPrincipal("fixture-tenant",Alice,"v1");
        var traffic=new TlsTraffic();
        _downstreamTraffic=new TlsTraffic();
        using var owner=PublicClient(traffic:traffic);
        using var analyst=PublicClient(_recipientCertificate,traffic);
        using var journal=new SqliteNopRuntimeJournal(Path.Combine(_directory,"nop-tasks.sqlite"),initializeNewFile:true);
        var resources=new List<ResultDescriptor>();
        var grants=new List<ResultRecipientGrant>();
        long delegates=0,aligns=0; var dispatches=0; var released=0;
        var reportRan=false;
        var uncertainCreates=new List<(Guid Key,JsonElement? Query,int Ttl)>();
        if(fault=="create-audit-loss") _beforeResultReply=audit=>
        {
            if(audit.Operation=="result.create"&&audit.Outcome=="success") throw new IOException("Synthetic lost post-allocation audit reply.");
        };
        async IAsyncEnumerable<NopAuthenticatedFrame> Execute(DelegateFrame frame,[EnumeratorCancellation] CancellationToken ct)
        {
            dispatches++;
            delegates+=JsonSerializer.SerializeToUtf8Bytes(frame).Length;
            // Host-owned worker table, independent of incoming frame claims/task context.
            var actualWorker=frame.NodeId=="analysis"?Bob:Alice;
            var expectedAction=frame.NodeId switch
            {
                "orders" or "refunds"=>"nwp://fixture/result.create", "analysis"=>"nwp://fixture/result.aggregate",
                "report"=>"nwp://fixture/report", _=>throw new InvalidOperationException("Unknown worker operation."),
            };
            if(frame.TargetAgentNid!=actualWorker||frame.Action!=expectedAction)
                throw new InvalidOperationException("Worker admission rejected.");
            JsonElement? output=null; StreamError? error=null;
            try
            {
                if(frame.NodeId is "orders" or "refunds")
                {
                    var query=frame.NodeId=="refunds"?JsonSerializer.SerializeToElement(new { filter=new { refunded=true } }):(JsonElement?)null;
                    var operationKey=Guid.Parse(frame.IdempotencyKey!);
                    uncertainCreates.Add((operationKey,query,fault=="expired-input"?1:300));
                    var descriptor=await owner.CreateAsync("orders",operationKey,query,
                        ttlSeconds:fault=="expired-input"?1:300,ct:ct);
                    uncertainCreates.RemoveAll(item=>item.Key==operationKey);
                    resources.Add(descriptor);
                    if(fault!="missing-grant")
                    {
                        var fields=frame.NodeId=="refunds"&&fault!="hidden-field"
                            ?new[] { "reason","period","amount_minor" }:new[] { "reason","period" };
                        grants.Add(_policy.IssueGrant(ownerPrincipal,_store.GetOwnerGrantScope(ownerPrincipal,descriptor.ResultRef),
                            Bob,["result.aggregate"],fields,descriptor.ExpiresAt));
                    }
                    output=JsonSerializer.SerializeToElement(descriptor);
                }
                else if(frame.NodeId=="analysis")
                {
                    var orders=frame.Params!.Value.GetProperty("orders").Deserialize<ResultDescriptor>()!;
                    var refunds=frame.Params.Value.GetProperty("refunds").Deserialize<ResultDescriptor>()!;
                    Assert.Equal(resources[0],orders); Assert.Equal(resources[1],refunds);
                    if(fault=="revoked-grant") foreach(var grant in grants) Assert.True(_policy.RevokeGrant(ownerPrincipal,grant.GrantId));
                    if(fault=="revoked-worker") await _identityCaStore.RevokeAsync(Bob,"synthetic D06 revocation",DateTime.UtcNow,ct);
                    if(fault=="expired-input") await Task.Delay(1100,ct);
                    var before=_handlers;
                    try
                    {
                        var counts=await analyst.AggregateAsync(orders,["reason","period"],ct:ct);
                        var sums=await analyst.AggregateAsync(refunds,["reason","period"],"amount_minor",ct:ct);
                        var expected=FrozenMethod().GetProperty("reference_answers").GetProperty(scale.ToString()).GetProperty("groups");
                        foreach(var group in counts.Groups)
                        {
                            var oracle=expected.GetProperty(group.GetProperty("reason").GetString()!).GetProperty(group.GetProperty("period").GetString()!);
                            Assert.Equal(oracle.GetProperty("orders").GetInt64(),group.GetProperty("count").GetInt64());
                        }
                        foreach(var group in sums.Groups)
                        {
                            var oracle=expected.GetProperty(group.GetProperty("reason").GetString()!).GetProperty(group.GetProperty("period").GetString()!);
                            Assert.Equal(oracle.GetProperty("refunds").GetInt64(),group.GetProperty("count").GetInt64());
                            Assert.Equal(oracle.GetProperty("refund_amount_minor").GetInt64(),group.GetProperty("sum").GetInt64());
                        }
                        // The aggregate-only recipient cannot read rows, release or widen egress.
                        var deniedBefore=_handlers;
                        var denied=await Assert.ThrowsAsync<ResultHttpException>(()=>analyst.ReadAsync(refunds,1,["id"],ct:ct));
                        Assert.Equal("NWP-RESULT-NOT-FOUND",denied.ErrorCode); Assert.Equal(deniedBefore,_handlers);
                        await Assert.ThrowsAsync<ResultHttpException>(()=>analyst.ReleaseAsync(refunds,ct));
                        output=JsonSerializer.SerializeToElement(new { refunds,counts,sums });
                    }
                    catch(ResultHttpException)
                    {
                        Assert.Equal(before+(fault=="hidden-field"?1:0),_handlers);
                        throw;
                    }
                }
                else
                {
                    reportRan=true;
                    var analysis=frame.Params!.Value.GetProperty("analysis");
                    var refunds=analysis.GetProperty("refunds").Deserialize<ResultDescriptor>()!;
                    var rows=await owner.ReadAsync(refunds,5,["id","reason","period","amount_minor"],ct:ct);
                    Assert.Equal(5,rows.Rows.Count);
                    Assert.Equal(refunds,rows.Descriptor);
                    output=JsonSerializer.SerializeToElement(new { report="exact-source-oracle-groups",provenance=refunds,
                        groups=analysis.GetProperty("sums").GetProperty("groups"),evidence=rows.Rows });
                }
            }
            catch(ResultHttpException failure)
            {
                // Authenticated pinned HTTP rejection is a known outcome, not an invented retry authorization.
                if(fault=="create-audit-loss")
                {
                    Assert.True(failure.OutcomeUnknown);
                    Assert.Equal("NWP-RESULT-POLICY-UNAVAILABLE",failure.ErrorCode);
                    Assert.Equal(1,_captures);
                }
                error=new() { Code=failure.OutcomeUnknown?NopErrorCodes.ExecutionIndeterminate
                        :failure.ErrorCode??NopErrorCodes.ExecutionIndeterminate,
                    Message="Synthetic resource operation failed.",Retryable=false };
            }
            if(fault=="lost-query-response"&&frame.NodeId=="orders") yield break;
            var terminal=new AlignStreamFrame { TaskId=frame.ParentTaskId,SubtaskId=frame.SubtaskId,
                StreamId=Guid.NewGuid().ToString("D"),Seq=0,IsFinal=true,SenderNid=actualWorker,
                ExecutionOutcome=error?.Code==NopErrorCodes.ExecutionIndeterminate?NopExecutionOutcome.Indeterminate:NopExecutionOutcome.KnownTerminal,
                Error=error,Data=output };
            aligns+=JsonSerializer.SerializeToUtf8Bytes(terminal).Length;
            yield return new(actualWorker,terminal);
        }
        var worker=new HandoffWorker(Execute);
        var task=new TaskFrame
        {
            TaskId=Guid.NewGuid().ToString("D"),TimeoutMs=120000,ResultTtlSeconds=300,MaxRetries=2,
            Dag=new() { Nodes=[
                new() { Id="orders",Agent=Alice,Action="nwp://fixture/result.create" },
                new() { Id="refunds",Agent=Alice,Action="nwp://fixture/result.create" },
                new() { Id="analysis",Agent=Bob,Action="nwp://fixture/result.aggregate",InputFrom=["orders","refunds"],
                    InputMapping=new Dictionary<string,JsonElement> { ["orders"]=JsonSerializer.SerializeToElement("$.orders"),["refunds"]=JsonSerializer.SerializeToElement("$.refunds") } },
                new() { Id="report",Agent=Alice,Action="nwp://fixture/report",InputFrom=["analysis"],
                    InputMapping=new Dictionary<string,JsonElement> { ["analysis"]=JsonSerializer.SerializeToElement("$.analysis") } } ],
                Edges=[new() { From="orders",To="analysis" },new() { From="refunds",To="analysis" },new() { From="analysis",To="report" }] },
        };
        var runtime=new NopOrchestrator(new HandoffLegacyDenied(),new InMemoryNopTaskStore(),new()
            { RuntimeBinding=new(Alice,journal,worker),EnableCallback=false });
        NopTaskResult result;
        try
        {
            result=await runtime.ExecuteAsync(task);
            var count=dispatches;
            Assert.Equal(JsonSerializer.Serialize(result),JsonSerializer.Serialize(await runtime.ExecuteAsync(task)));
            Assert.Equal(count,dispatches);
            Assert.Equal(fault is null,reportRan);
        }
        finally
        {
            _beforeResultReply=null;
            // Unknown creates retain their original keys for a separate owner decision.
            // Cleanup never automatically replays them or claims their effect was rolled back.
            foreach(var grant in grants) _policy.RevokeGrant(ownerPrincipal,grant.GrantId);
            foreach(var resource in resources)
            {
                try { if((await owner.ReleaseAsync(resource)).Released) released++; }
                catch(ResultHttpException failure) when(fault=="expired-input"&&failure.ErrorCode=="NWP-RESULT-NOT-FOUND") { }
            }
        }
        var observation=new HandoffObservation(result,dispatches,0,delegates,aligns,traffic.Total,_downstreamTraffic.Total,released,uncertainCreates.Count);
        var capture=Environment.GetEnvironmentVariable("NPS_NOP_HANDOFF_CAPTURE_DIR");
        if(capture is not null)
        {
            if(!Path.IsPathFullyQualified(capture)||!Directory.Exists(capture)) throw new InvalidOperationException("Explicit existing absolute capture directory required.");
            using var output=new FileStream(Path.Combine(capture,"raw-case.json"),FileMode.CreateNew,FileAccess.Write,FileShare.None);
            JsonSerializer.Serialize(output,new { rows=scale,fault,observation,source_captures=_captures,resource_handler_dispatches=_handlers,
                unresolved_create_keys=uncertainCreates.Select(item=>new { idempotency_key=item.Key,query=item.Query,ttl_seconds=item.Ttl }) });
        }
        return observation;
    }

    private sealed class HandoffWorker(Func<DelegateFrame,CancellationToken,IAsyncEnumerable<NopAuthenticatedFrame>> execute) : INopAuthenticatedWorkerClient
    { public IAsyncEnumerable<NopAuthenticatedFrame> DelegateAsync(DelegateFrame frame,CancellationToken ct)=>execute(frame,ct); }
    private sealed class HandoffLegacyDenied : INopWorkerClient
    {
        public IAsyncEnumerable<AlignStreamFrame> DelegateAsync(DelegateFrame frame,CancellationToken ct=default)=>throw new InvalidOperationException();
        public Task<PreflightResult> PreflightAsync(string nid,string action,long estimatedNpt=0,IReadOnlyList<string>? requiredCapabilities=null,CancellationToken ct=default)=>throw new InvalidOperationException();
    }
}
