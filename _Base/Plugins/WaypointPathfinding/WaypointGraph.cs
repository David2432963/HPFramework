using UnityEngine;

/// <summary>
/// Container for a set of WaypointNodes that form a navigation graph.
/// Provides nearest-node queries used by WaypointPathfinder.
/// Attach to a room or area GameObject and assign all child WaypointNodes via Inspector.
/// </summary>
public sealed class WaypointGraph : MonoBehaviour
{
    [Tooltip("All waypoint nodes belonging to this graph. Drag and drop in Inspector.")]
    [SerializeField] private WaypointNode[] nodes = System.Array.Empty<WaypointNode>();

    public WaypointNode[] Nodes => nodes;
    public int NodeCount => nodes.Length;

    /// <summary>
    /// Finds the nearest WaypointNode to the given world position using Euclidean distance.
    /// Returns null if the graph has no nodes.
    /// </summary>
    public WaypointNode FindNearestNode(Vector3 position)
    {
        if (nodes.Length == 0)
        {
            return null;
        }

        WaypointNode nearest = null;
        float nearestSqrDist = float.MaxValue;

        for (int i = 0; i < nodes.Length; i++)
        {
            WaypointNode node = nodes[i];
            if (node == null)
            {
                continue;
            }

            float sqrDist = (node.Position - position).sqrMagnitude;
            if (sqrDist < nearestSqrDist)
            {
                nearestSqrDist = sqrDist;
                nearest = node;
            }
        }

        return nearest;
    }

#if UNITY_EDITOR
    [Header("Auto Link Settings (Editor)")]
    [SerializeField] private float maxLinkDistance = 5f;
    [SerializeField] private LayerMask obstacleMask;

    private void OnValidate()
    {
        nodes = GetComponentsInChildren<WaypointNode>(true);
    }

    /// <summary>
    /// Finds and registers all child WaypointNodes on this GameObject.
    /// </summary>
    public void PopulateFromChildren()
    {
        nodes = GetComponentsInChildren<WaypointNode>(true);
        UnityEditor.EditorUtility.SetDirty(this);
    }

    /// <summary>
    /// Automatically connects nodes if they are within maxLinkDistance and have no obstacle in between.
    /// </summary>
    public void AutoLinkNodes()
    {
        if (nodes == null || nodes.Length == 0)
        {
            Debug.LogWarning("No nodes registered in WaypointGraph to link.");
            return;
        }

        foreach (WaypointNode node in nodes)
        {
            if (node == null) continue;

            var newNeighbors = new System.Collections.Generic.List<WaypointNode>();
            foreach (WaypointNode other in nodes)
            {
                if (other == null || other == node) continue;

                float dist = Vector3.Distance(node.Position, other.Position);
                if (dist <= maxLinkDistance)
                {
                    // Linecast from node to other (lift slightly up to avoid ground collider)
                    Vector3 start = node.Position + Vector3.up * 0.5f;
                    Vector3 end = other.Position + Vector3.up * 0.5f;

                    if (!Physics.Linecast(start, end, obstacleMask))
                    {
                        newNeighbors.Add(other);
                    }
                }
            }
            node.SetNeighbors(newNeighbors.ToArray());
        }

        Debug.Log($"Auto-linked {nodes.Length} nodes in WaypointGraph.");
    }

    /// <summary>
    /// Removes neighbor references on all registered nodes.
    /// </summary>
    public void ClearAllConnections()
    {
        if (nodes == null) return;
        foreach (WaypointNode node in nodes)
        {
            if (node != null)
            {
                node.SetNeighbors(System.Array.Empty<WaypointNode>());
            }
        }
        Debug.Log("Cleared all connections.");
    }
#endif
}

#if UNITY_EDITOR
[UnityEditor.CustomEditor(typeof(WaypointGraph))]
public class WaypointGraphEditor : UnityEditor.Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        WaypointGraph graph = (WaypointGraph)target;

        GUILayout.Space(15);
        GUILayout.Label("Graph Construction Helpers", UnityEditor.EditorStyles.boldLabel);

        if (GUILayout.Button("1. Populate Nodes from Children"))
        {
            graph.PopulateFromChildren();
        }

        GUILayout.Space(5);
        if (GUILayout.Button("2. Auto Link Neighbors (Distance + Linecast)"))
        {
            if (UnityEditor.EditorUtility.DisplayDialog("Auto Link Nodes", 
                "This will overwrite all current neighbor connections on nodes in this graph. Proceed?", "Yes", "No"))
            {
                graph.AutoLinkNodes();
            }
        }

        if (GUILayout.Button("3. Clear All Connections"))
        {
            if (UnityEditor.EditorUtility.DisplayDialog("Clear Connections", 
                "This will clear neighbor lists on all nodes in this graph. Proceed?", "Yes", "No"))
            {
                graph.ClearAllConnections();
            }
        }
    }
}
#endif

