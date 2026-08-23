using UnityEngine;
using UnityEngine.Rendering;
using AstroAces.Core;

namespace AstroAces.World
{
    /// <summary>
    /// Procedural ground -- BUILD_PLAN.md 6.3, DESIGN.md Sec 9: a 5km x 5km subdivided plane
    /// with layered Perlin displacement, +-25m of relief. Replaces the flat placeholder
    /// plane that's been standing in since Phase 0 (see Dogfight.unity's
    /// "Ground (Placeholder)" object).
    ///
    /// Two Perlin octaves (large rolling shape + finer detail) rather than one, so the
    /// terrain doesn't read as a single uniform bump frequency across 5km.
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    [RequireComponent(typeof(MeshCollider))]
    public class GroundBuilder : MonoBehaviour
    {
        [SerializeField] Material groundMaterial;
        [SerializeField] float sizeMeters = 5000f;
        [SerializeField] int quadsPerSide = 200;
        [SerializeField] float reliefMeters = 25f;
        [SerializeField] float largeNoiseScale = 0.0006f;
        [SerializeField] float smallNoiseScale = 0.004f;
        [SerializeField] int seed = 12345;

        void Awake() => Build();

        [ContextMenu("Rebuild")]
        public void Build()
        {
            Mesh mesh = GenerateMesh();

            GetComponent<MeshFilter>().sharedMesh = mesh;

            var collider = GetComponent<MeshCollider>();
            collider.sharedMesh = null;   // force a fresh bake -- a stale reference otherwise sticks
            collider.sharedMesh = mesh;

            if (groundMaterial != null)
                GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

            gameObject.layer = Layers.Ground;
        }

        Mesh GenerateMesh()
        {
            int verticesPerSide = quadsPerSide + 1;
            var vertices = new Vector3[verticesPerSide * verticesPerSide];
            var uvs = new Vector2[vertices.Length];
            float step = sizeMeters / quadsPerSide;
            float half = sizeMeters * 0.5f;

            // Seeded offset into Perlin space, not the mesh itself, so the SAME seed always
            // reproduces the SAME terrain (System.Random is deterministic given a seed;
            // Mathf.PerlinNoise is deterministic given its inputs).
            var rng = new System.Random(seed);
            float offsetX = (float)rng.NextDouble() * 10000f;
            float offsetZ = (float)rng.NextDouble() * 10000f;

            for (int z = 0; z < verticesPerSide; z++)
            {
                for (int x = 0; x < verticesPerSide; x++)
                {
                    int i = z * verticesPerSide + x;
                    float worldX = -half + x * step;
                    float worldZ = -half + z * step;

                    float large = Mathf.PerlinNoise((worldX + offsetX) * largeNoiseScale, (worldZ + offsetZ) * largeNoiseScale);
                    float small = Mathf.PerlinNoise((worldX + offsetX) * smallNoiseScale, (worldZ + offsetZ) * smallNoiseScale);
                    float height = (large * 0.7f + small * 0.3f) * 2f - 1f;   // -1..1
                    height *= reliefMeters;

                    vertices[i] = new Vector3(worldX, height, worldZ);
                    uvs[i] = new Vector2((float)x / quadsPerSide, (float)z / quadsPerSide);
                }
            }

            var triangles = new int[quadsPerSide * quadsPerSide * 6];
            int t = 0;
            for (int z = 0; z < quadsPerSide; z++)
            {
                for (int x = 0; x < quadsPerSide; x++)
                {
                    int i = z * verticesPerSide + x;
                    triangles[t++] = i;
                    triangles[t++] = i + verticesPerSide;
                    triangles[t++] = i + 1;

                    triangles[t++] = i + 1;
                    triangles[t++] = i + verticesPerSide;
                    triangles[t++] = i + verticesPerSide + 1;
                }
            }

            var mesh = new Mesh { name = "GroundMesh" };
            mesh.indexFormat = vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Ground height at a world XZ position, sampled by raycasting straight down
        /// against this ground's own collider. Used by RockScatter to place props on the
        /// actual displaced surface rather than a flat assumed height.</summary>
        public bool TrySampleHeight(Vector3 worldXZ, out float height)
        {
            Vector3 origin = new Vector3(worldXZ.x, 10000f, worldXZ.z);
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 20000f, 1 << Layers.Ground))
            {
                height = hit.point.y;
                return true;
            }
            height = 0f;
            return false;
        }
    }
}
