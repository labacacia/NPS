// Copyright 2026 INNO LOTUS PTY LTD
// SPDX-License-Identifier: Apache-2.0

using NPS.NOP.Models;

namespace NPS.NOP.Validation;

/// <summary>
/// Validates a <see cref="TaskDag"/> against NPS-5 §3.1.1 rules:
/// acyclicity, node count limit, start/end node presence, edge consistency,
/// and condition expression length.
/// </summary>
public static class DagValidator
{
    /// <summary>
    /// Validates the given DAG and returns a topological ordering on success.
    /// </summary>
    public static DagValidationResult Validate(TaskDag dag)
    {
        if (dag.Nodes.Count == 0)
            return DagValidationResult.Failure(
                NopErrorCodes.TaskDagInvalid,
                "DAG must contain at least one node.");

        if (dag.Nodes.Count > NopConstants.MaxDagNodes)
            return DagValidationResult.Failure(
                NopErrorCodes.TaskDagTooLarge,
                $"DAG contains {dag.Nodes.Count} nodes, exceeding the maximum of {NopConstants.MaxDagNodes}.");

        var nodeIds = new HashSet<string>(dag.Nodes.Count);
        foreach (var node in dag.Nodes)
        {
            if (!nodeIds.Add(node.Id))
                return DagValidationResult.Failure(
                    NopErrorCodes.TaskDagInvalid,
                    $"Duplicate node ID: '{node.Id}'.");
        }

        // Validate edges reference existing nodes
        var adjacency = new Dictionary<string, List<string>>(nodeIds.Count);
        var inDegree = new Dictionary<string, int>(nodeIds.Count);
        foreach (var id in nodeIds)
        {
            adjacency[id] = [];
            inDegree[id] = 0;
        }

        foreach (var edge in dag.Edges)
        {
            if (!nodeIds.Contains(edge.From))
                return DagValidationResult.Failure(
                    NopErrorCodes.TaskDagInvalid,
                    $"Edge references unknown source node: '{edge.From}'.");

            if (!nodeIds.Contains(edge.To))
                return DagValidationResult.Failure(
                    NopErrorCodes.TaskDagInvalid,
                    $"Edge references unknown target node: '{edge.To}'.");

            adjacency[edge.From].Add(edge.To);
            inDegree[edge.To]++;
        }

        // Validate input_from references are consistent with edges
        foreach (var node in dag.Nodes)
        {
            if (node.InputFrom is not { Count: > 0 })
                continue;

            foreach (var upstream in node.InputFrom)
            {
                if (!nodeIds.Contains(upstream))
                    return DagValidationResult.Failure(
                        NopErrorCodes.TaskDagInvalid,
                        $"Node '{node.Id}' references unknown upstream node '{upstream}' in input_from.");
            }
        }

        // Must have at least one start node (no incoming edges)
        bool hasStart = inDegree.Values.Any(d => d == 0);
        if (!hasStart)
            return DagValidationResult.Failure(
                NopErrorCodes.TaskDagInvalid,
                "DAG must have at least one start node (no incoming edges).");

        // Must have at least one end node (no outgoing edges)
        bool hasEnd = adjacency.Values.Any(list => list.Count == 0);
        if (!hasEnd)
            return DagValidationResult.Failure(
                NopErrorCodes.TaskDagInvalid,
                "DAG must have at least one end node (no outgoing edges).");

        // Validate condition expression lengths
        foreach (var node in dag.Nodes)
        {
            if (node.Condition is { Length: > NopConstants.MaxConditionLength })
                return DagValidationResult.Failure(
                    NopErrorCodes.ConditionEvalError,
                    $"Node '{node.Id}' condition expression exceeds {NopConstants.MaxConditionLength} characters.");
        }

        // Kahn's algorithm for topological sort + cycle detection
        var queue = new Queue<string>();
        foreach (var (id, degree) in inDegree)
        {
            if (degree == 0)
                queue.Enqueue(id);
        }

        var sorted = new List<string>(nodeIds.Count);
        var remaining = new Dictionary<string, int>(inDegree);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            sorted.Add(current);

            foreach (var neighbor in adjacency[current])
            {
                remaining[neighbor]--;
                if (remaining[neighbor] == 0)
                    queue.Enqueue(neighbor);
            }
        }

        if (sorted.Count != nodeIds.Count)
            return DagValidationResult.Failure(
                NopErrorCodes.TaskDagCycle,
                "DAG contains a cycle.");

        return DagValidationResult.Success(sorted);
    }
}
