// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;

namespace NPS.NOP.Orchestration;

/// <summary>
/// Resolves NOP JSONPath expressions of the form <c>$.node_id.field.subfield</c>
/// against a dictionary of upstream node results (NPS-5 §3.1.3).
/// <para>
/// Path syntax:
/// <list type="bullet">
///   <item><c>$</c> — the entire upstream context (all node results combined).</item>
///   <item><c>$.node_id</c> — the full result object of a specific node.</item>
///   <item><c>$.node_id.field</c> — a specific field within a node's result.</item>
///   <item><c>$.node_id.field.sub</c> — nested navigation (max <see cref="NopConstants.MaxInputMappingDepth"/> levels).</item>
/// </list>
/// </para>
/// </summary>
public static class NopInputMapper
{
    private static readonly JsonSerializerOptions s_json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented        = false,
    };

    /// <summary>
    /// Resolves a single JSONPath expression against the upstream node result context.
    /// Returns <c>null</c> when the path leads to a missing property.
    /// </summary>
    /// <exception cref="NopMappingException">Thrown for malformed paths or depth violations.</exception>
    public static JsonElement? Resolve(string path, IReadOnlyDictionary<string, JsonElement> context)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new NopMappingException($"Input mapping path must not be empty.", NopErrorCodes.InputMappingError);

        if (!path.StartsWith("$."))
            throw new NopMappingException($"Input mapping path must start with '$.' — got: {path}", NopErrorCodes.InputMappingError);

        // Split: "$", "node_id", "field", "sub", ...
        var parts = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        // parts[0] == "$"

        if (parts.Length > NopConstants.MaxInputMappingDepth + 1)
            throw new NopMappingException(
                $"Input mapping path depth {parts.Length - 1} exceeds maximum {NopConstants.MaxInputMappingDepth}: {path}",
                NopErrorCodes.InputMappingError);

        if (parts.Length == 1)
        {
            // Just "$" → serialize the entire context as a JSON object
            var allDict = context.ToDictionary(kv => kv.Key, kv => kv.Value);
            var json = JsonSerializer.Serialize(allDict, s_json);
            return JsonDocument.Parse(json).RootElement;
        }

        var nodeId = parts[1];
        if (!context.TryGetValue(nodeId, out var nodeResult))
            return null;

        if (parts.Length == 2)
            return nodeResult; // "$.node_id" → full result

        // Navigate deeper into the JSON element
        var current = nodeResult;
        for (int i = 2; i < parts.Length; i++)
        {
            if (current.ValueKind != JsonValueKind.Object)
                return null;
            if (!current.TryGetProperty(parts[i], out current))
                return null;
        }
        return current;
    }

    /// <summary>
    /// Builds an <c>ActionFrame.params</c> object by resolving all <c>input_mapping</c>
    /// entries against the upstream result context.
    /// </summary>
    /// <param name="inputMapping">The node's <c>input_mapping</c> dictionary (parameter → JSONPath).</param>
    /// <param name="context">Upstream node results.</param>
    /// <returns>A <see cref="JsonElement"/> object suitable for <c>DelegateFrame.Params</c>.</returns>
    public static JsonElement BuildParams(
        IReadOnlyDictionary<string, JsonElement>? inputMapping,
        IReadOnlyDictionary<string, JsonElement>  context)
    {
        if (inputMapping is null or { Count: 0 })
            return JsonDocument.Parse("{}").RootElement;

        var dict = new Dictionary<string, object?>(inputMapping.Count);
        foreach (var (paramName, pathElement) in inputMapping)
        {
            // The spec allows either a string JSONPath or an array of JSONPaths.
            if (pathElement.ValueKind == JsonValueKind.String)
            {
                var resolved = Resolve(pathElement.GetString()!, context);
                dict[paramName] = resolved;
            }
            else if (pathElement.ValueKind == JsonValueKind.Array)
            {
                var list = new List<JsonElement?>();
                foreach (var p in pathElement.EnumerateArray())
                {
                    list.Add(p.ValueKind == JsonValueKind.String
                        ? Resolve(p.GetString()!, context)
                        : p);
                }
                dict[paramName] = list;
            }
            else
            {
                dict[paramName] = pathElement;
            }
        }

        var json = JsonSerializer.Serialize(dict, s_json);
        return JsonDocument.Parse(json).RootElement;
    }
}

/// <summary>Thrown when an input mapping path cannot be resolved.</summary>
public sealed class NopMappingException : Exception
{
    public string ErrorCode { get; }
    public NopMappingException(string message, string errorCode) : base(message) => ErrorCode = errorCode;
}
