using UnityEditor;
using UnityEngine;

namespace AstroAces.EditorTools
{
    /// <summary>
    /// One-click project configuration: tags, layers, and the few project settings the
    /// game depends on.
    ///
    /// WHY A SCRIPT AND NOT "just add them in the inspector": layer INDICES are load-bearing.
    /// Scripts use LayerMask by index, the minimap camera culls by index, and the projectile
    /// raycast masks by index. Hand-adding layers in a different order silently breaks all
    /// three in ways that look like unrelated bugs. This is idempotent -- run it as often
    /// as you like.
    ///
    /// Run: menu bar > Astro Aces > Setup Project (Tags, Layers, Settings)
    /// </summary>
    public static class ProjectSetup
    {
        // Indices are contractual. Do not reorder. Layers.cs mirrors these at runtime.
        static readonly (int index, string name)[] Layers =
        {
            (6,  "Aircraft"),
            (7,  "Projectile"),
            (8,  "Ground"),
            (9,  "MinimapIcon"),
            (10, "Cloud"),
        };

        // "Player" is a Unity built-in tag and must NOT be added again.
        static readonly string[] Tags = { "Enemy" };

        [MenuItem("Astro Aces/Setup Project (Tags, Layers, Settings)")]
        public static void Run()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[AstroAces] Could not open ProjectSettings/TagManager.asset.");
                return;
            }

            var so = new SerializedObject(assets[0]);
            int added = 0;

            var layersProp = so.FindProperty("layers");
            foreach (var (index, name) in Layers)
            {
                if (index >= layersProp.arraySize)
                {
                    Debug.LogError($"[AstroAces] Layer slot {index} does not exist.");
                    continue;
                }
                var slot = layersProp.GetArrayElementAtIndex(index);
                if (slot.stringValue == name) continue;

                if (!string.IsNullOrEmpty(slot.stringValue))
                {
                    Debug.LogError($"[AstroAces] Layer {index} is already '{slot.stringValue}', " +
                                   $"expected empty or '{name}'. Resolve by hand before continuing " +
                                   "-- moving it now would break every LayerMask in the project.");
                    continue;
                }
                slot.stringValue = name;
                added++;
            }

            var tagsProp = so.FindProperty("tags");
            foreach (var tag in Tags)
            {
                bool exists = false;
                for (int i = 0; i < tagsProp.arraySize; i++)
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == tag) { exists = true; break; }
                if (exists) continue;

                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = tag;
                added++;
            }

            so.ApplyModifiedProperties();

            // Default shadow distance is 50 m, which is nothing when the play area is 5 km
            // wide and the aircraft sits 900 m up. Terrain shadows just vanish without this.
            QualitySettings.shadowDistance = 400f;

            AssetDatabase.SaveAssets();
            Debug.Log($"[AstroAces] Project setup complete. {added} tag/layer change(s) applied. " +
                      "Shadow distance set to 400 m.");
        }

        [MenuItem("Astro Aces/Verify Project Setup")]
        public static void Verify()
        {
            bool ok = true;
            foreach (var (index, name) in Layers)
            {
                string actual = LayerMask.LayerToName(index);
                if (actual != name)
                {
                    Debug.LogError($"[AstroAces] Layer {index} is '{actual}', expected '{name}'.");
                    ok = false;
                }
            }
            Debug.Log(ok
                ? "[AstroAces] Layers verified."
                : "[AstroAces] Layer setup is WRONG -- run Astro Aces > Setup Project.");
        }
    }
}
