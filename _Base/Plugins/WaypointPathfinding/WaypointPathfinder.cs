using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* pathfinder operating on a WaypointGraph.
/// Pure C# class â€” no MonoBehaviour dependency.
/// Reuses internal collections across calls to avoid GC allocations.
///
/// Usage:
///   var pathfinder = new WaypointPathfinder();
///   var result = new List&lt;WaypointNode&gt;();
///   if (pathfinder.TryFindPath(graph, start, goal, result))
///   {
///       // result contains the ordered waypoints from start to goal (exclusive of start)
///   }
/// </summary>
public sealed class WaypointPathfinder
{
    private readonly List<WaypointNode> openList = new List<WaypointNode>();
    private readonly HashSet<WaypointNode> closedSet = new HashSet<WaypointNode>();
    private readonly Dictionary<WaypointNode, WaypointNode> cameFrom = new Dictionary<WaypointNode, WaypointNode>();
    private readonly Dictionary<WaypointNode, float> gCost = new Dictionary<WaypointNode, float>();
    private readonly Dictionary<WaypointNode, float> fCost = new Dictionary<WaypointNode, float>();

    /// <summary>
    /// Finds the shortest path from <paramref name="start"/> to <paramref name="goal"/>
    /// on the given <paramref name="graph"/> using A* with Euclidean heuristic.
    /// Results are written into <paramref name="resultPath"/> (cleared first).
    /// The path includes waypoints from the node after start up to and including goal.
    /// </summary>
    /// <returns>True if a path was found (or start == goal), false otherwise.</returns>
    public bool TryFindPath(WaypointGraph graph, WaypointNode start, WaypointNode goal, List<WaypointNode> resultPath)
    {
        if (resultPath == null) throw new System.ArgumentNullException(nameof(resultPath));

        resultPath.Clear();

        if (graph == null || start == null || goal == null)
        {
            return false;
        }

        if (start == goal)
        {
            return true;
        }

        ClearInternalState();

        gCost[start] = 0f;
        fCost[start] = Heuristic(start, goal);
        openList.Add(start);

        while (openList.Count > 0)
        {
            WaypointNode current = PopLowestFCost();
            if (current == goal)
            {
                ReconstructPath(start, goal, resultPath);
                return true;
            }

            closedSet.Add(current);

            WaypointNode[] neighbors = current.Neighbors;
            for (int i = 0; i < neighbors.Length; i++)
            {
                WaypointNode neighbor = neighbors[i];
                if (neighbor == null || closedSet.Contains(neighbor))
                {
                    continue;
                }

                float tentativeG = gCost[current] + Vector3.Distance(current.Position, neighbor.Position);

                bool isNewNode = !gCost.ContainsKey(neighbor);
                if (!isNewNode && tentativeG >= gCost[neighbor])
                {
                    continue;
                }

                cameFrom[neighbor] = current;
                gCost[neighbor] = tentativeG;
                fCost[neighbor] = tentativeG + Heuristic(neighbor, goal);

                if (isNewNode)
                {
                    openList.Add(neighbor);
                }
            }
        }

        return false;
    }

    private void ClearInternalState()
    {
        openList.Clear();
        closedSet.Clear();
        cameFrom.Clear();
        gCost.Clear();
        fCost.Clear();
    }

    private WaypointNode PopLowestFCost()
    {
        int bestIndex = 0;
        float bestF = fCost[openList[0]];

        for (int i = 1; i < openList.Count; i++)
        {
            float f = fCost[openList[i]];
            if (f < bestF)
            {
                bestF = f;
                bestIndex = i;
            }
        }

        WaypointNode best = openList[bestIndex];
        int lastIndex = openList.Count - 1;
        openList[bestIndex] = openList[lastIndex];
        openList.RemoveAt(lastIndex);
        return best;
    }

    private void ReconstructPath(WaypointNode start, WaypointNode goal, List<WaypointNode> resultPath)
    {
        WaypointNode current = goal;
        while (current != start)
        {
            resultPath.Add(current);
            current = cameFrom[current];
        }

        resultPath.Reverse();
    }

    private static float Heuristic(WaypointNode a, WaypointNode b)
    {
        return Vector3.Distance(a.Position, b.Position);
    }
}
