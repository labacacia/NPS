// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;

namespace NPS.Tests.Nwp;

// Explicit actual-fixture entry point, not a protocol endpoint or synthetic billing.
internal static class NopHandoffCapture
{
    private static readonly string[] Faults=["missing-grant","revoked-grant","hidden-field","revoked-worker","expired-input","lost-query-response","create-audit-loss"];
    internal static async Task<int> ExecuteAsync(string label)
    {
        var conditions=(from rows in new[] {100,1000,100000} from repeat in Enumerable.Range(0,3)
            select (Id:$"handoff-{rows}-pass-r{repeat}",Rows:rows,Fault:(string?)null))
            .Concat(Faults.Select(f=>(Id:$"handoff-100-{f}-r0",Rows:100,Fault:(string?)f))).ToArray();
        var selected=conditions.Where(c=>c.Id==label).ToArray();
        var directory=Environment.GetEnvironmentVariable("NPS_NOP_HANDOFF_CAPTURE_DIR");
        if(selected.Length!=1||directory is null||!Path.IsPathFullyQualified(directory)||!Directory.Exists(directory)
            ||File.Exists(Path.Combine(directory,"raw-case.json"))||File.Exists(Path.Combine(directory,"execution-receipt.json")))
            return 2;
        var condition=selected[0];
        var timer=Stopwatch.StartNew(); var allocated=GC.GetTotalAllocatedBytes();
        using var process=Process.GetCurrentProcess(); var cpu=process.TotalProcessorTime;
        var fixture=new ResultInvocationTlsTests();
        try
        {
            if(Environment.ProcessorCount!=4||GC.GetGCMemoryInfo().TotalAvailableMemoryBytes!=335544320)
                throw new InvalidOperationException("Frozen CPU/managed-heap controls missing.");
            try
            {
                await fixture.InitializeAsync();
                if(condition.Fault is null) await fixture.NopWorkersExchangeAuthorizedReferencesWithoutBulkModelContext(condition.Rows);
                else await fixture.NopHandoffRejectsUnprovenOrUnauthorizedInputAndDoesNotRunReport(condition.Fault);
            }
            finally { await fixture.DisposeAsync(); }
            var raw=await File.ReadAllBytesAsync(Path.Combine(directory,"raw-case.json"));
            using var document=JsonDocument.Parse(raw);
            if(document.RootElement.GetProperty("rows").GetInt32()!=condition.Rows
                ||document.RootElement.GetProperty("fault").GetString()!=condition.Fault) throw new InvalidDataException();
            await using var output=new FileStream(Path.Combine(directory,"execution-receipt.json"),FileMode.CreateNew,FileAccess.Write,FileShare.None);
            await JsonSerializer.SerializeAsync(output,new
            {
                case_id=label,rows=condition.Rows,fault=condition.Fault,passed=true,execution_mode="direct_asserted_fixture",
                process_id=Environment.ProcessId,processor_count=Environment.ProcessorCount,
                gc_available_memory_bytes=GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,gc_server=System.Runtime.GCSettings.IsServerGC,
                framework=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                test_assembly_sha256=Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(typeof(NopHandoffCapture).Assembly.Location))),
                raw_record_sha256=Convert.ToHexStringLower(SHA256.HashData(raw)),wall_ms=timer.Elapsed.TotalMilliseconds,
                process_cpu_ms=(process.TotalProcessorTime-cpu).TotalMilliseconds,managed_allocated_bytes=GC.GetTotalAllocatedBytes()-allocated,
            });
            Console.WriteLine("Completed declared handoff condition: "+label); return 0;
        }
        catch(Exception error)
        {
            Console.Error.WriteLine(error.GetType().FullName); return 1; // No task bodies/proofs/keys.
        }
    }
}
