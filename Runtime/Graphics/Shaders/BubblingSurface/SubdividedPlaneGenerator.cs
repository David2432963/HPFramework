using UnityEngine;
using UnityEngine.Rendering;

namespace HP.Framework.Graphics
{
    /// <summary>
    /// Generates an XZ grid mesh with upward-facing normals.
    /// Use it when the source mesh does not have enough vertices for displacement.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public sealed class SubdividedPlaneGenerator : MonoBehaviour
    {
        [Min(1)]
        [SerializeField]
        private int subdivisionsX = 40;

        [Min(1)]
        [SerializeField]
        private int subdivisionsZ = 40;

        [SerializeField]
        private Vector2 size = new Vector2(10f, 10f);

        [SerializeField]
        private bool generateOnAwake = true;

        private Mesh generatedMesh;

        private void Awake()
        {
            if (generateOnAwake)
            {
                GenerateMesh();
            }
        }

        [ContextMenu("Generate Mesh")]
        public void GenerateMesh()
        {
            subdivisionsX = Mathf.Max(1, subdivisionsX);
            subdivisionsZ = Mathf.Max(1, subdivisionsZ);
            size.x = Mathf.Max(0.001f, size.x);
            size.y = Mathf.Max(0.001f, size.y);

            int vertexCountX = subdivisionsX + 1;
            int vertexCountZ = subdivisionsZ + 1;
            int vertexCount = vertexCountX * vertexCountZ;
            int triangleIndexCount = subdivisionsX * subdivisionsZ * 6;

            Vector3[] vertices = new Vector3[vertexCount];
            Vector3[] normals = new Vector3[vertexCount];
            Vector4[] tangents = new Vector4[vertexCount];
            Vector2[] uvs = new Vector2[vertexCount];
            int[] triangles = new int[triangleIndexCount];

            int vertexIndex = 0;

            for (int z = 0; z < vertexCountZ; z++)
            {
                float v = z / (float)subdivisionsZ;
                float localZ = Mathf.Lerp(-size.y * 0.5f, size.y * 0.5f, v);

                for (int x = 0; x < vertexCountX; x++)
                {
                    float u = x / (float)subdivisionsX;
                    float localX = Mathf.Lerp(-size.x * 0.5f, size.x * 0.5f, u);

                    vertices[vertexIndex] = new Vector3(localX, 0f, localZ);
                    normals[vertexIndex] = Vector3.up;
                    tangents[vertexIndex] = new Vector4(1f, 0f, 0f, 1f);
                    uvs[vertexIndex] = new Vector2(u, v);

                    vertexIndex++;
                }
            }

            int triangleIndex = 0;

            for (int z = 0; z < subdivisionsZ; z++)
            {
                for (int x = 0; x < subdivisionsX; x++)
                {
                    int bottomLeft = z * vertexCountX + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + vertexCountX;
                    int topRight = topLeft + 1;

                    triangles[triangleIndex++] = bottomLeft;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = bottomRight;

                    triangles[triangleIndex++] = bottomRight;
                    triangles[triangleIndex++] = topLeft;
                    triangles[triangleIndex++] = topRight;
                }
            }

            ReleaseGeneratedMesh();

            generatedMesh = new Mesh
            {
                name = $"Generated Bubbling Plane {subdivisionsX}x{subdivisionsZ}",
                indexFormat = vertexCount > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16
            };

            generatedMesh.vertices = vertices;
            generatedMesh.normals = normals;
            generatedMesh.tangents = tangents;
            generatedMesh.uv = uvs;
            generatedMesh.triangles = triangles;
            generatedMesh.RecalculateBounds();

            GetComponent<MeshFilter>().sharedMesh = generatedMesh;
        }

        private void OnDestroy()
        {
            ReleaseGeneratedMesh();
        }

        private void ReleaseGeneratedMesh()
        {
            if (generatedMesh == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(generatedMesh);
            }
            else
            {
                DestroyImmediate(generatedMesh);
            }

            generatedMesh = null;
        }
    }
}
