// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;

namespace NPS.Tests.Ncp;

// The test SDK already builds an executable assembly. This opt-in entry replaces
// its generated placeholder, preserving VSTest discovery while allowing a complete
// asserted resource workflow without a parent test platform. No protocol endpoint.
internal static class SchemaBatchCaptureProgram
{
    public static async Task<int> Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--nop-handoff")
            return await NPS.Tests.Nwp.NopHandoffCapture.ExecuteAsync(args[1]);
        var directory = Environment.GetEnvironmentVariable("NPS_NCP_TRANSFER_CAPTURE_DIR");
        if (args.Length != 2 || args[0] != "--case" || directory is null || !Path.IsPathFullyQualified(directory)
            || !Directory.Exists(directory))
        {
            Console.Error.WriteLine("Explicit --case and an existing absolute NPS_NCP_TRANSFER_CAPTURE_DIR are required.");
            return 2;
        }
        var label = args[1];
        var selected = NcpSchemaBatchSessionTests.TransferConditions().Where(row => (string)row[4] == label).ToArray();
        if (selected.Length != 1) { Console.Error.WriteLine("Select exactly one declared transfer condition."); return 2; }
        var capturePath = Path.Combine(directory, $"transfer-{label}.json");
        var receiptPath = Path.Combine(directory, "execution-receipt.json");
        if (File.Exists(capturePath) || File.Exists(receiptPath) || File.Exists(Path.Combine(directory, $"partial-{label}.json")))
        { Console.Error.WriteLine("Refuse to overwrite an earlier capture."); return 2; }
        Environment.SetEnvironmentVariable("NPS_NCP_TRANSFER_CASE", label);
        Environment.SetEnvironmentVariable("NPS_NCP_TRANSFER_EXECUTION_MODE", "direct_fixture");
        try
        {
            var condition = selected[0];
            await new NcpSchemaBatchSessionTests().ComparableTransferResourceEvidence(
                (int)condition[0], (string)condition[1], (bool)condition[2], (bool)condition[3], label);
            var bytes = await File.ReadAllBytesAsync(capturePath);
            using var capture = JsonDocument.Parse(bytes);
            var root = capture.RootElement;
            if (root.GetProperty("case_id").GetString() != label || root.GetProperty("samples").GetArrayLength() != 30)
                throw new InvalidDataException("Completed fixture capture is incomplete.");
            await using var file = new FileStream(receiptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await JsonSerializer.SerializeAsync(file, new
            {
                case_id = label, execution_mode = "direct_fixture", executed_cases = 1, passed_cases = 1,
                test_assembly_sha256 = root.GetProperty("test_assembly_sha256").GetString(),
                raw_record_sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
            });
            Console.WriteLine($"Completed declared transfer condition: {label}");
            return 0;
        }
        catch (Exception error)
        {
            // Fixture data are synthetic. Do not print raw response/credential bodies.
            Console.Error.WriteLine(error.GetType().FullName);
            Console.Error.WriteLine(error.StackTrace);
            return 1;
        }
    }
}
