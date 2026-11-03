// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NPS.Core.Serialization;
using NPS.NOP.Frames;

namespace NPS.NOP.Runtime;

// Deliberately constrained canonical input: bounded JSON with safe integer numbers.
// Reject broader numeric inputs instead of claiming a general JCS implementation.
internal static class NopTaskIdentity
{
    internal static (TaskFrame Task, string Digest) Freeze(TaskFrame task)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(task);
        if (json.Length > 65536) throw new NopRuntimeException(NopErrorCodes.TaskDagInvalid);
        var copy = JsonSerializer.Deserialize<TaskFrame>(json)!;
        var buffer = new StringBuilder();
        using var document = JsonDocument.Parse(json);
        Write(document.RootElement, buffer, 0);
        return (copy, Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(buffer.ToString()))));
    }

    private static void Write(JsonElement value, StringBuilder output, int depth)
    {
        if (depth > 32) throw new NopRuntimeException(NopErrorCodes.TaskDagInvalid);
        void Raw(string text) => output.Append(text);
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                Raw("{"); var first = true;
                var properties = value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal).ToArray();
                if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                    throw new NopRuntimeException(NopErrorCodes.TaskDagInvalid);
                foreach (var property in properties)
                {
                    if (!first) Raw(","); first = false;
                    JcsString.Append(output, property.Name); Raw(":"); Write(property.Value, output, depth + 1);
                }
                Raw("}"); break;
            case JsonValueKind.Array:
                Raw("["); var index = 0;
                foreach (var item in value.EnumerateArray()) { if (index++ > 0) Raw(","); Write(item, output, depth + 1); }
                Raw("]"); break;
            case JsonValueKind.String: JcsString.Append(output, value.GetString()!); break;
            case JsonValueKind.Number:
                if (!value.TryGetInt64(out var number) || number is < -9007199254740991 or > 9007199254740991)
                    throw new NopRuntimeException(NopErrorCodes.TaskDagInvalid);
                Raw(number.ToString(System.Globalization.CultureInfo.InvariantCulture)); break;
            case JsonValueKind.True: Raw("true"); break;
            case JsonValueKind.False: Raw("false"); break;
            case JsonValueKind.Null: Raw("null"); break;
            default: throw new NopRuntimeException(NopErrorCodes.TaskDagInvalid);
        }
    }
}
