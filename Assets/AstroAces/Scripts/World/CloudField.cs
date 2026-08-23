using UnityEngine;
using AstroAces.Core;

namespace AstroAces.World
{
    /// <summary>
    /// Cloud clusters -- BUILD_PLAN.md 6.5, DESIGN.md Sec 9: N clusters of 4-8 jittered
    /// low-poly spheres, flat toon material, colliders removed, slow drift. "They must never
    /// affect flight" (DESIGN.md) is the load-bearing requirement here -- every sphere has
    /// its Collider destroyed immediately after creation, not just left disabled, so there is
    /// no way for a future change to accidentally re-enable collision on a cloud.
    /// </summary>
    public class CloudField : MonoBehaviour
    {
        [SerializeField] Material cloudMaterial;
        [SerializeField] int clusterCount = 40;
        [SerializeField] float areaSize = 5000f;
        [SerializeField] Vector2 altitudeRange = new Vector2(300f, 800f);
        [SerializeField] Vector2 puffScaleRange = new Vector2(40f, 120f);
        [SerializeField] Vector2Int puffsPerClusterRange = new Vector2Int(4, 8);
        [SerializeField] float driftMetersPerSecond = 1.5f;
        [SerializeField] int seed = 555;

        Transform[] clusters;
        Vector3[] driftDirections;

        void Start() => Build();

        [ContextMenu("Rebuild")]
        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            var rng = new System.Random(seed);
            float half = areaSize * 0.5f;

            clusters = new Transform[clusterCount];
            driftDirections = new Vector3[clusterCount];

            for (int c = 0; c < clusterCount; c++)
            {
                var cluster = new GameObject($"CloudCluster_{c}");
                cluster.transform.SetParent(transform, false);

                float x = (float)(rng.NextDouble() * areaSize - half);
                float z = (float)(rng.NextDouble() * areaSize - half);
                float y = Mathf.Lerp(altitudeRange.x, altitudeRange.y, (float)rng.NextDouble());
                cluster.transform.position = new Vector3(x, y, z);

                int puffCount = rng.Next(puffsPerClusterRange.x, puffsPerClusterRange.y + 1);
                for (int p = 0; p < puffCount; p++)
                    BuildPuff(cluster.transform, rng);

                clusters[c] = cluster.transform;
                float angle = (float)(rng.NextDouble() * Mathf.PI * 2.0);
                driftDirections[c] = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            }
        }

        void BuildPuff(Transform parent, System.Random rng)
        {
            GameObject puff = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            puff.name = "Puff";
            puff.layer = Layers.Cloud;

            Collider col = puff.GetComponent<Collider>();
            if (col != null) Destroy(col);   // clouds must never affect flight -- DESIGN.md Sec 9

            puff.transform.SetParent(parent, false);
            puff.transform.localPosition = new Vector3(
                (float)(rng.NextDouble() * 2.0 - 1.0),
                (float)(rng.NextDouble() * 2.0 - 1.0) * 0.4f,   // flatter vertical jitter -- clouds read as puffy discs, not spheres
                (float)(rng.NextDouble() * 2.0 - 1.0)) * 20f;

            float scale = Mathf.Lerp(puffScaleRange.x, puffScaleRange.y, (float)rng.NextDouble());
            puff.transform.localScale = Vector3.one * scale;

            if (cloudMaterial != null)
                puff.GetComponent<MeshRenderer>().sharedMaterial = cloudMaterial;
        }

        void Update()
        {
            if (clusters == null) return;

            for (int i = 0; i < clusters.Length; i++)
            {
                if (clusters[i] == null) continue;
                clusters[i].position += driftDirections[i] * driftMetersPerSecond * Time.deltaTime;
            }
        }
    }
}
