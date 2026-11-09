// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using NPS.Demo.DataWorkflow;

// Paths are operator configuration, never model input. Run artifacts stay in one fresh demo-owned directory.
if (args.Length != 4 || args[0] != "--output" || args[2] != "--rows"
    || !int.TryParse(args[3], out var rows) || rows is not (100 or 1000 or 100000))
    throw new ArgumentException("Usage: --output <new absolute directory below .demo-output> --rows <100|1000|100000>");
var directory = Path.GetFullPath(args[1]);
if (!Path.IsPathFullyQualified(args[1]) || !directory.Split(Path.DirectorySeparatorChar).Contains(".demo-output")
    || Directory.Exists(directory) || File.Exists(directory))
    throw new ArgumentException("Choose a fresh absolute run directory below .demo-output.");
Directory.CreateDirectory(directory);
if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(directory,
    UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
using var authority = new DemoAuthority();
await authority.InitializeAsync(directory);
await using var host = await DemoHost.StartAsync(authority, new OrderSource(rows), directory);
await host.WaitForShutdownAsync();
