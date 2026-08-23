using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace UTI
{
    [Flags]
    public enum BeanOutputTargets
    {
        None = 0,
        Console = 1 << 0,
        Csv = 1 << 1,
        Json = 1 << 2
    }

    /// <summary>Subscribes to a BeanTracker's samples and forwards them to one or more IBeanOutputs.</summary>
    public class BeanLogger : MonoBehaviour
    {
        [SerializeField] private BeanTracker tracker;
        [SerializeField] private BeanOutputTargets outputTargets = BeanOutputTargets.Console;
        [SerializeField] private string filePath;
        // Off by default - matches the original behavior of a fresh, freshly-timestamped CSV on
        // every Open(). A pooled object (SetActive(false)->SetActive(true) reuse instead of
        // Destroy/Instantiate) can opt in to keep one running CSV across reuse cycles instead of
        // truncating each time - see DESIGN.md Sec 13/T14.
        [SerializeField] private bool appendAcrossReuse;

        public BeanTracker Tracker { get => tracker; set => tracker = value; }

        // A plain field assignment here would silently do nothing once isOpen - Open() only ever
        // builds activeOutputs once per activation (see Open()'s isOpen guard), so a script that
        // adds BeanLogger at runtime and changes OutputTargets a moment later (after the
        // synchronous OnEnable->Open() already ran with whatever the field's default/prior value
        // was) would see zero effect until a manual Close()+Open() - confirmed live, BUG-06
        // (TESTS/BugTracker.md). Re-opening on an actual change makes the setter behave the
        // same regardless of whether it's touched via script or the Inspector.
        public BeanOutputTargets OutputTargets
        {
            get => outputTargets;
            set
            {
                if (outputTargets == value)
                    return;

                outputTargets = value;

                if (isOpen)
                {
                    Close();
                    Open();
                }
            }
        }

        public string FilePath { get => filePath; set => filePath = value; }
        public bool AppendAcrossReuse { get => appendAcrossReuse; set => appendAcrossReuse = value; }

        // Extra sinks beyond outputTargets (JSON, an analytics endpoint, whatever) without
        // touching core code. Combined with the outputTargets-built outputs, not a replacement.
        public List<IBeanOutput> CustomOutputs { get; } = new List<IBeanOutput>();

        private readonly List<IBeanOutput> activeOutputs = new List<IBeanOutput>();
        private bool isOpen;
        // Only ever set/read when appendAcrossReuse is true - resolved once on this instance's
        // first Open() and reused on every subsequent Open(), so a pooled object's repeated
        // SetActive(false)->SetActive(true) cycles keep writing to the same file instead of each
        // reopen resolving a brand-new timestamped path (which would defeat append entirely).
        private string cachedCsvPath;
        private string cachedJsonPath;

        private void OnEnable() => Open();

        private void OnDisable() => Close();

        private void OnDestroy() => Close();

        // Unity calls Reset() when this component is first added in the Editor - same hook
        // BeanTracker/BeanSnapshotExporter already use for BeanConfig defaults (see DESIGN.md
        // Sec 8.7). Deliberately does NOT run at runtime - what's visible in the Inspector for an
        // already-existing BeanLogger is always exactly what's used, no hidden config override.
        private void Reset() => ApplyConfigDefaults(BeanConfig.Load());

        /// <summary>
        /// Applies a BeanConfig's defaults to this logger's serialized fields - a no-op if config
        /// is null. Separated from Reset() so it's testable directly without touching the real
        /// filesystem, same split used by BeanTracker/BeanSnapshotExporter.
        /// </summary>
        public void ApplyConfigDefaults(BeanConfig config)
        {
            if (config == null)
                return;

            outputTargets = config.DefaultOutputTargets;
        }

        // Standalone builds don't reliably hit OnDisable/OnDestroy on quit - belt and suspenders
        // so CSV output actually gets flushed.
        private void OnApplicationQuit() => Close();

        /// <summary>
        /// Resolves the tracker, builds the active output set, and opens each output. Exposed
        /// publicly (like BeanTracker.StartTracking/SimulateFrame) so this is testable in Edit
        /// Mode without relying on OnEnable, which doesn't fire there without [ExecuteAlways].
        /// Idempotent - calling twice without an intervening Close() is a no-op.
        /// </summary>
        public void Open()
        {
            if (isOpen)
                return;

            if (tracker == null)
                tracker = GetComponent<BeanTracker>();

            if (tracker == null)
                return;

            BuildActiveOutputs();

            // Opening one output is a real system boundary (file I/O - disk full, permission
            // denied, an invalid path) that shouldn't take down every other output with it. Without
            // isolating this, a single broken CSV path would previously propagate out of Open()
            // entirely, skipping "tracker.OnSample += HandleSample; isOpen = true;" below - meaning
            // Console output (already opened successfully) would silently never receive samples
            // either, and this BeanLogger would look enabled in the Inspector while doing nothing.
            for (int i = activeOutputs.Count - 1; i >= 0; i--)
            {
                try
                {
                    activeOutputs[i].Open(tracker);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Bean] Output {activeOutputs[i].GetType().Name} failed to open for '{gameObject.name}', disabling it for this run: {ex.Message}");
                    activeOutputs.RemoveAt(i);
                }
            }

            tracker.OnSample += HandleSample;
            isOpen = true;
        }

        /// <summary>Unsubscribes and closes every active output. Idempotent.</summary>
        public void Close()
        {
            if (!isOpen)
                return;

            tracker.OnSample -= HandleSample;

            // Same reasoning as Open() above - one output failing to close (e.g. a final flush
            // hitting a full disk) shouldn't stop the others from closing/flushing cleanly.
            foreach (IBeanOutput output in activeOutputs)
            {
                try
                {
                    output.Close();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Bean] Output {output.GetType().Name} failed to close cleanly for '{gameObject.name}': {ex.Message}");
                }
            }

            isOpen = false;
        }

        private void BuildActiveOutputs()
        {
            activeOutputs.Clear();

            if ((outputTargets & BeanOutputTargets.Console) != 0)
                activeOutputs.Add(new ConsoleBeanOutput());

            // An explicit filePath override only ever backs a single file-based output safely -
            // if both Csv and Json are active at once, one fixed path can't back two different
            // formats without one silently overwriting the other's content. Same precedent as
            // BeanSnapshotExporter's multi-angle capture (DESIGN.md Sec 8.4): fall back to each
            // format's own default auto-named path instead of colliding.
            bool bothFileFormatsActive = (outputTargets & BeanOutputTargets.Csv) != 0 && (outputTargets & BeanOutputTargets.Json) != 0;

            if ((outputTargets & BeanOutputTargets.Csv) != 0)
            {
                string path = ResolveOutputPath(bothFileFormatsActive, "csv", ref cachedCsvPath);
                activeOutputs.Add(new CsvBeanOutput(path, appendAcrossReuse));
            }

            if ((outputTargets & BeanOutputTargets.Json) != 0)
            {
                string path = ResolveOutputPath(bothFileFormatsActive, "jsonl", ref cachedJsonPath);
                activeOutputs.Add(new JsonlBeanOutput(path, appendAcrossReuse));
            }

            activeOutputs.AddRange(CustomOutputs);
        }

        // Fresh path every call when appendAcrossReuse is off (original behavior); the first
        // call's path when it's on, cached (into whichever format's own cache field the caller
        // passes by ref) and reused for every subsequent Open() on this instance so a pooled
        // object's reuse cycles keep landing in the same file. Shared by both CSV and JSON Lines -
        // they differ only in extension and which cache field backs them.
        private string ResolveOutputPath(bool ignoreExplicitPath, string extension, ref string cachedPath)
        {
            if (appendAcrossReuse && !string.IsNullOrEmpty(cachedPath))
                return cachedPath;

            string explicitPath = ignoreExplicitPath ? null : filePath;
            string path = ResolveFilePath(explicitPath, gameObject.name, BeanArtifactPaths.NewUniqueToken(), DateTime.UtcNow, extension);
            if (appendAcrossReuse)
                cachedPath = path;

            return path;
        }

        // Subfolder name, nested under BeanArtifactPaths.RootDirectory ("<project root>/UTI/").
        private const string LogSubfolder = "BeanLogs";

        /// <summary>
        /// Resolves where an output file gets written - explicit filePath if set (used as-is),
        /// otherwise a default under BeanArtifactPaths.RootDirectory/BeanLogs/ that's unique both
        /// across repeated runs on the same GameObject (timestamped, so "ran it 5 times, want to
        /// compare" doesn't silently overwrite the same file) and across duplicate GameObject
        /// names alive at once (uniqueToken, so two prefab clones both named e.g. "Bullet(Clone)"
        /// opening in the same frame - and therefore the same millisecond - still don't collide).
        /// This was the CSV half of the collision gap flagged in DESIGN.md Sec 13/T13, now fixed
        /// to match BeanSnapshotExporter's PNG side. Pure function (given a timestamp/token) so
        /// it's testable without a live GameObject. The extension parameter defaults to "csv" so
        /// every existing call site (including outside this file) keeps compiling unchanged.
        /// </summary>
        public static string ResolveFilePath(string explicitPath, string objectName, string uniqueToken, DateTime captureTimeUtc, string extension = "csv")
        {
            if (!string.IsNullOrEmpty(explicitPath))
                return explicitPath;

            string timestamp = captureTimeUtc.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string fileName = $"{timestamp}_{objectName}_{uniqueToken}_bean.{extension}";
            return BeanArtifactPaths.ResolveDefaultPath(LogSubfolder, fileName);
        }

        private void HandleSample(BeanSample sample)
        {
            // Same isolation as Open()/Close(): a write failure partway through a run (e.g. disk
            // fills up) shouldn't propagate up through the tracker's own OnSample invocation - that
            // would break BeanTracker.Capture() itself for every other OnSample subscriber, not
            // just this one broken output. Drop the offending output instead of retrying it every
            // sample once it's proven itself unreliable this run.
            for (int i = activeOutputs.Count - 1; i >= 0; i--)
            {
                try
                {
                    activeOutputs[i].Write(sample);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Bean] Output {activeOutputs[i].GetType().Name} failed to write a sample for '{gameObject.name}', disabling it for the rest of this run: {ex.Message}");
                    activeOutputs.RemoveAt(i);
                }
            }
        }
    }
}
