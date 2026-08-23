using UnityEngine;

namespace AstroAces.World
{
    /// <summary>
    /// Scatters SimpleNaturePack rocks across the ground -- BUILD_PLAN.md 6.4. Seeded
    /// System.Random so layout is reproducible between runs (same seed = same rocks in the
    /// same places), which matters for anyone tuning around a specific piece of terrain.
    ///
    /// Depends on GroundBuilder having already built its collider (GroundBuilder.Awake()
    /// runs before this component's Start() -- Unity guarantees every Awake() completes
    /// before any Start() runs, so this ordering is safe without an explicit dependency call).
    /// </summary>
    public class RockScatter : MonoBehaviour
    {
        [SerializeField] GroundBuilder ground;
        [SerializeField] GameObject[] rockPrefabs;
        [Tooltip("SimpleNaturePack's own rock materials use the Built-in Render Pipeline's " +
                 "Standard shader, which renders hot magenta under URP. Overriding with a " +
                 "URP-compatible material here (every renderer, every instance) is what " +
                 "keeps the rocks from doing that -- same fix history as the ground/aircraft " +
                 "placeholders had in Phase 0/1, see HANDOFF.md.")]
        [SerializeField] Material rockMaterial;
        [SerializeField] int count = 400;
        [SerializeField] float areaSize = 5000f;
        [SerializeField] Vector2 scaleRange = new Vector2(20f, 80f);
        [SerializeField] int seed = 777;

        void Start() => Scatter();

        [ContextMenu("Scatter")]
        public void Scatter()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyChild(transform.GetChild(i).gameObject);

            if (ground == null || rockPrefabs == null || rockPrefabs.Length == 0) return;

            var rng = new System.Random(seed);
            float half = areaSize * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float x = (float)(rng.NextDouble() * areaSize - half);
                float z = (float)(rng.NextDouble() * areaSize - half);

                if (!ground.TrySampleHeight(new Vector3(x, 0f, z), out float y)) continue;

                GameObject prefab = rockPrefabs[rng.Next(rockPrefabs.Length)];
                float yaw = (float)(rng.NextDouble() * 360.0);
                GameObject rock = Instantiate(prefab, new Vector3(x, y, z), Quaternion.Euler(0f, yaw, 0f), transform);

                float scale = Mathf.Lerp(scaleRange.x, scaleRange.y, (float)rng.NextDouble());
                rock.transform.localScale = Vector3.one * scale;

                if (rockMaterial != null)
                {
                    foreach (Renderer r in rock.GetComponentsInChildren<Renderer>())
                        r.sharedMaterial = rockMaterial;
                }
            }
        }

        static void DestroyChild(GameObject go)
        {
            if (Application.isPlaying) Destroy(go);
            else DestroyImmediate(go);
        }
    }
}
