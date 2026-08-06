using UnityEngine;

/// <summary>
/// A single point on a waypoint graph used for pathfinding.
/// Place at intersections, corners, and key positions of a maze or navigation area.
/// Connect neighbors via the Inspector to define walkable edges.
/// </summary>
public sealed class WaypointNode : MonoBehaviour
{
    [Tooltip("Adjacent nodes reachable from this node. Drag and drop in Inspector.")]
    [SerializeField] private WaypointNode[] neighbors = System.Array.Empty<WaypointNode>();

    [HideInInspector]
    [SerializeField] private int serializedInstanceId;

    public Vector3 Position => transform.position;
    public WaypointNode[] Neighbors => neighbors;

#if UNITY_EDITOR
    public void SetNeighbors(WaypointNode[] newNeighbors)
    {
        neighbors = newNeighbors;
        UnityEditor.EditorUtility.SetDirty(this);
    }

    private void OnValidate()
    {
        // Unity's GetInstanceID() is not stable across Playmode changes or Editor restarts.
        // The previous duplication detection logic caused nodes to auto-link to missing references 
        // when clearing the array. Removed to prevent the "Missing (Waypoint Node)" bug.
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(transform.position, 1f);

        for (int i = 0; i < neighbors.Length; i++)
        {
            WaypointNode neighbor = neighbors[i];
            if (neighbor == null)
            {
                continue;
            }

            // Check if connection is bidirectional
            bool isBidirectional = false;
            if (neighbor.neighbors != null)
            {
                for (int j = 0; j < neighbor.neighbors.Length; j++)
                {
                    if (neighbor.neighbors[j] == this)
                    {
                        isBidirectional = true;
                        break;
                    }
                }
            }

            // Green for two-way, Orange for one-way
            Gizmos.color = isBidirectional ? Color.green : new Color(1f, 0.5f, 0f);
            DrawArrow(transform.position, neighbor.transform.position);
        }

        // Draw name label above the node
        UnityEditor.Handles.color = Color.white;
        UnityEditor.Handles.Label(transform.position + Vector3.up * 0.35f, gameObject.name);
    }

    private static void DrawArrow(Vector3 from, Vector3 to)
    {
        Gizmos.DrawLine(from, to);

        Vector3 direction = (to - from).normalized;
        float arrowHeadLength = 0.25f;
        float arrowHeadAngle = 20f;

        if (direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        Vector3 arrowBase = to - direction * arrowHeadLength;
        Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, arrowHeadAngle, 0f) * Vector3.back;
        Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(0f, -arrowHeadAngle, 0f) * Vector3.back;

        Gizmos.DrawLine(to, arrowBase + right * arrowHeadLength);
        Gizmos.DrawLine(to, arrowBase + left * arrowHeadLength);
    }
#endif
}
