using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace UTI
{
    /// <summary>Overrides BeanSnapshotExporter's flat/2D-vs-3D auto-detection when needed.</summary>
    public enum BeanDimensionMode
    {
        Auto,
        Force2D,
        Force3D
    }

    /// <summary>
    /// A named camera angle for BeanSnapshotExporter's multi-angle capture. Auto is the original
    /// single-shot behavior (orthographic front-on for a flat path, elevated broadside for a 3D
    /// one) - Above/Side/Behind are explicit perspective angles, useful together when one angle
    /// alone doesn't show enough context (see README.md Roadmap, T23 in TESTS/TestTracker.md).
    /// </summary>
    public enum BeanSnapshotAngle
    {
        Auto,
        Above,
        Side,
        Behind
    }

    // Renders through a real Camera rather than Editor Gizmos, so the output is a durable file
    // (openable after the run ends, shareable, diffable) and captures actual scene geometry
    // around the path (floor, walls, props) - not just an abstract line in empty space. This is
    // also what a build/Play-mode-only environment can produce, unlike gizmos which are
    // Editor/Scene-view only.
    /// <summary>
    /// Captures a PNG of a BeanTracker's recorded path drawn into a real Camera render, so a
    /// developer gets a persisted, reviewable artifact instead of only a live Editor Gizmo trail.
    /// Deliberately a fast, basic sanity artifact - low default resolution, no lighting/quality
    /// tuning - meant to be quick to create and quick to glance at and understand, not a polished
    /// screenshot. If a case needs more visual fidelity than that, this isn't the tool for it.
    /// </summary>
    public class BeanSnapshotExporter : MonoBehaviour
    {
        // All snapshots land in one subfolder under the project root by default (see
        // BeanArtifactPaths), separate from BeanLogger's CSVs - one place to look for
        // "the pictures", not scattered loose files.
        private const string SnapshotSubfolder = "BeanSnapshots";

        [SerializeField] private BeanTracker tracker;
        [SerializeField] private Camera captureCamera;
        [SerializeField] private Color pathColor = Color.yellow;
        [SerializeField] private float lineWidth = 0.1f;
        // Deliberately low-res by default - this is meant to be a fast, "good enough to
        // understand at a glance" sanity artifact, not a polished screenshot. Small resolution
        // means a quick render, a quick PNG encode/write, and a small/quick-to-open file.
        // Bump these per-Bean if a specific case genuinely needs more detail.
        [SerializeField] private int captureWidth = 640;
        [SerializeField] private int captureHeight = 360;
        // On by default: repositions a copy of the capture camera's settings (restored after)
        // to frame the whole recorded path plus surrounding geometry, instead of using whatever
        // the gameplay camera happens to be pointed at (e.g. a tight follow-cam, which showed a
        // near-featureless close-up with no visible path in T17's first attempt).
        [SerializeField] private bool autoFrameCamera = true;
        // Auto detects flat/2D vs real 3D from the path's own bounds (see IsFlatPath) - fine for
        // every test project so far, but a 2D game built on a non-standard plane, or a 3D game
        // whose captured path happens to be flat, would fool it. Force2D/Force3D let a dev
        // override the auto-guess outright instead.
        [SerializeField] private BeanDimensionMode dimensionMode = BeanDimensionMode.Auto;
        // Scale-dependent floor for both the orthographic half-size and the perspective camera
        // distance, so a single-point/near-zero path (e.g. walked up to a wall and stopped)
        // still frames with real margin instead of an unhelpful close-up. Was a fixed 2f literal
        // - project 2's actual scale needed more (T23, TESTS/TestTracker.md) - now a per-Bean
        // field, defaultable per-project via BeanConfig.DefaultMinFramingRadius.
        [SerializeField] private float minFramingRadius = 2f;
        // One angle (the default, Auto) behaves exactly like the original single-shot capture -
        // same file, same naming. More than one angle captures one PNG per angle in the same
        // CaptureSnapshot() call, sharing a group timestamp/token (see ResolveMultiAngleSnapshotPath).
        [SerializeField] private BeanSnapshotAngle[] captureAngles = { BeanSnapshotAngle.Auto };
        [SerializeField] private bool captureOnStopTracking = true;
        [SerializeField] private string filePath;

        public BeanTracker Tracker { get => tracker; set => tracker = value; }
        public Camera CaptureCamera { get => captureCamera; set => captureCamera = value; }
        public Color PathColor { get => pathColor; set => pathColor = value; }
        public float LineWidth { get => lineWidth; set => lineWidth = value; }
        public int CaptureWidth { get => captureWidth; set => captureWidth = value; }
        public int CaptureHeight { get => captureHeight; set => captureHeight = value; }
        public bool AutoFrameCamera { get => autoFrameCamera; set => autoFrameCamera = value; }
        public BeanDimensionMode DimensionMode { get => dimensionMode; set => dimensionMode = value; }
        public float MinFramingRadius { get => minFramingRadius; set => minFramingRadius = value; }
        public BeanSnapshotAngle[] CaptureAngles { get => captureAngles; set => captureAngles = value; }
        public bool CaptureOnStopTracking { get => captureOnStopTracking; set => captureOnStopTracking = value; }
        public string FilePath { get => filePath; set => filePath = value; }

        /// <summary>Path the last successful CaptureSnapshot() call wrote to, if any (the last
        /// angle written, when capturing more than one).</summary>
        public string LastSnapshotPath { get; private set; }

        /// <summary>Every path written by the last successful CaptureSnapshot() call, one entry
        /// per configured angle. Empty until a capture succeeds.</summary>
        public IReadOnlyList<string> LastSnapshotPaths { get; private set; } = Array.Empty<string>();

        /// <summary>
        /// The line width (world units) actually used for the last capture, after auto-frame
        /// scaling - exposed so a manual verification pass can report the real number instead of
        /// guessing visibility from the image alone. See LineWidthScaleFactor.
        /// </summary>
        public float LastLineWidth { get; private set; }

        private void OnEnable()
        {
            if (tracker == null)
                tracker = GetComponent<BeanTracker>();

            if (captureOnStopTracking && tracker != null)
                tracker.OnStopTracking += HandleStopTracking;
        }

        private void OnDisable()
        {
            if (tracker != null)
                tracker.OnStopTracking -= HandleStopTracking;
        }

        // See BeanTracker.Reset() for why this hook and not OnEnable/runtime - same reasoning
        // applies here.
        private void Reset() => ApplyConfigDefaults(BeanConfig.Load());

        /// <summary>
        /// Applies a BeanConfig's defaults to this exporter's serialized fields - a no-op if
        /// config is null. Separated from Reset() so it's testable directly. See
        /// BeanConfig/CONFIG.md.
        /// </summary>
        public void ApplyConfigDefaults(BeanConfig config)
        {
            if (config == null)
                return;

            dimensionMode = config.DefaultDimensionMode;
            minFramingRadius = config.DefaultMinFramingRadius;
        }

        private void HandleStopTracking() => CaptureSnapshot();

        /// <summary>
        /// Renders the capture camera to an off-screen texture with the tracker's path drawn in
        /// via a temporary LineRenderer, then writes the result to disk as a uniquely-named PNG -
        /// one per configured angle in captureAngles, sharing one timestamp/uniqueToken group (so
        /// repeated runs on the same Bean don't overwrite each other, and a multi-angle capture's
        /// files are obviously related - see ResolveMultiAngleSnapshotPath). Call manually for a
        /// mid-run snapshot, or leave captureOnStopTracking on to fire automatically when the
        /// paired BeanTracker stops. Not EditMode-testable - needs a live Camera to render, same
        /// category as BeanVisualizer's actual gizmo draw. BuildPathPositions, ComputePathBounds,
        /// ComputeFramingForAngle, and ResolveSnapshotPath/ResolveMultiAngleSnapshotPath below are
        /// the pure pieces that are testable.
        /// </summary>
        public void CaptureSnapshot()
        {
            BeanTracker activeTracker = tracker != null ? tracker : GetComponent<BeanTracker>();
            Camera activeCamera = captureCamera != null ? captureCamera : Camera.main;
            if (activeTracker == null || activeCamera == null)
                return;

            IReadOnlyList<BeanSample> samples = activeTracker.Samples;
            Vector3[] positions = BuildPathPositions(samples);

            // Found live 2026-08-08 (project 2 T27/T28): this reads the tracker's live ring buffer,
            // not the CSV - BeanBuffer has a fixed capacity (BeanTracker.MaxSamples) and silently
            // overwrites its oldest samples once full. Tracking left running well past the
            // interesting part (a long idle tail) can silently evict the entire real path before a
            // snapshot ever happens, leaving only near-identical stationary samples behind - which
            // frames as a tight, empty-looking close-up and draws as an invisible near-zero-length
            // line, with no error anywhere to explain why. This warning is the only signal short of
            // reading the code that this could be happening.
            if (IsBufferAtCapacity(samples.Count, activeTracker.MaxSamples))
            {
                Debug.LogWarning($"[Bean] '{gameObject.name}'s tracker buffer is full ({activeTracker.MaxSamples} samples) - older path data may already be evicted from the ring buffer, so this snapshot could be missing earlier movement (e.g. if tracking was left running well past the part you actually wanted). The CSV, if BeanLogger is attached, keeps the full history regardless. Raise BeanTracker's Max Samples, or call StopTracking() promptly once you have what you need, if this matters for your capture.");
            }

            BeanSnapshotAngle[] angles = (captureAngles == null || captureAngles.Length == 0)
                ? new[] { BeanSnapshotAngle.Auto }
                : captureAngles;
            var writtenPaths = new List<string>(angles.Length);

            GameObject lineObject = null;
            RenderTexture renderTexture = null;
            RenderTexture previousActive = RenderTexture.active;
            RenderTexture previousTarget = activeCamera.targetTexture;
            Transform camTransform = activeCamera.transform;
            Vector3 previousPosition = camTransform.position;
            Quaternion previousRotation = camTransform.rotation;
            bool previousOrthographic = activeCamera.orthographic;
            float previousOrthographicSize = activeCamera.orthographicSize;

            try
            {
                Bounds bounds = ComputePathBounds(samples);
                Vector3 travelDirection = positions.Length >= 2
                    ? positions[positions.Length - 1] - positions[0]
                    : Vector3.zero;
                bool isFlat = ResolveIsFlat(dimensionMode, bounds);
                float aspect = captureWidth / (float)captureHeight;

                DateTime captureTime = DateTime.UtcNow;
                string uniqueToken = BeanArtifactPaths.NewUniqueToken();

                renderTexture = new RenderTexture(captureWidth, captureHeight, 24);
                activeCamera.targetTexture = renderTexture;

                for (int i = 0; i < angles.Length; i++)
                {
                    // Framing is computed (and the line width scaled to match) before the line
                    // object is created - a fixed lineWidth tuned for a close manual shot becomes
                    // sub-pixel and invisible once auto-framing pulls the camera back far enough
                    // to fit an entire path, which is exactly what made the first auto-framed
                    // capture look like an empty scene (see DESIGN.md Sec 8.4 Change Log).
                    float effectiveLineWidth = lineWidth;

                    if (autoFrameCamera && positions.Length > 0)
                    {
                        SnapshotFraming framing = ComputeFramingForAngle(
                            bounds, travelDirection, isFlat, aspect, activeCamera.fieldOfView, minFramingRadius, angles[i]);

                        float framingScale = framing.Orthographic
                            ? framing.OrthographicSize
                            : Vector3.Distance(framing.Position, bounds.center);
                        effectiveLineWidth = Mathf.Max(lineWidth, framingScale * LineWidthScaleFactor);

                        camTransform.SetPositionAndRotation(framing.Position, framing.Rotation);
                        activeCamera.orthographic = framing.Orthographic;
                        if (framing.Orthographic)
                            activeCamera.orthographicSize = framing.OrthographicSize;
                    }

                    LastLineWidth = effectiveLineWidth;

                    if (lineObject != null)
                    {
                        SafeDestroy(lineObject);
                        lineObject = null;
                    }
                    if (positions.Length >= 2)
                        lineObject = CreatePathLine(positions, effectiveLineWidth);

                    activeCamera.Render();

                    RenderTexture.active = renderTexture;
                    var texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
                    try
                    {
                        texture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0);
                        texture.Apply();

                        string path = angles.Length > 1
                            ? ResolveMultiAngleSnapshotPath(filePath, gameObject.name, angles[i], i, angles.Length, uniqueToken, captureTime)
                            : ResolveSnapshotPath(filePath, gameObject.name, uniqueToken, captureTime);

                        string directory = Path.GetDirectoryName(path);
                        if (!string.IsNullOrEmpty(directory))
                            Directory.CreateDirectory(directory);

                        File.WriteAllBytes(path, texture.EncodeToPNG());

                        writtenPaths.Add(path);
                        LastSnapshotPath = path;
                    }
                    catch (Exception ex)
                    {
                        // Writing one angle's PNG is a real system boundary (disk I/O - disk full,
                        // permission denied) - a failure on one angle in a multi-angle capture
                        // shouldn't lose the angles that already succeeded or abort the ones still
                        // queued after it.
                        Debug.LogWarning($"[Bean] Failed to write snapshot for angle {angles[i]} on '{gameObject.name}': {ex.Message}");
                    }
                    finally
                    {
                        SafeDestroy(texture);
                    }
                }

                LastSnapshotPaths = writtenPaths;
            }
            finally
            {
                activeCamera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (autoFrameCamera)
                {
                    camTransform.SetPositionAndRotation(previousPosition, previousRotation);
                    activeCamera.orthographic = previousOrthographic;
                    activeCamera.orthographicSize = previousOrthographicSize;
                }
                if (renderTexture != null)
                    renderTexture.Release();
                if (lineObject != null)
                    SafeDestroy(lineObject);
            }
        }

        // How much of the auto-frame's scale (orthographic size, or camera distance for a
        // perspective shot) the path line's width should be, so it stays visible regardless of
        // how far back auto-framing pulls the camera. lineWidth is still the floor - this only
        // ever grows the line for a wide auto-framed shot, never shrinks a manually-tuned one.
        private const float LineWidthScaleFactor = 0.02f;

        private GameObject CreatePathLine(Vector3[] positions, float width)
        {
            var line = new GameObject("BeanSnapshotPath");
            var renderer = line.AddComponent<LineRenderer>();
            renderer.positionCount = positions.Length;
            renderer.SetPositions(positions);
            renderer.startWidth = width;
            renderer.endWidth = width;
            renderer.material = new Material(Shader.Find("Sprites/Default"));
            renderer.startColor = pathColor;
            renderer.endColor = pathColor;
            renderer.useWorldSpace = true;
            return line;
        }

        // Object.Destroy() is a documented no-op (logs an error, doesn't actually destroy) when
        // called outside Play Mode - found live 2026-08-08 driving CaptureSnapshot() from an
        // Editor script: the temporary BeanSnapshotPath GameObjects it creates were silently
        // leaking into the scene every capture instead of being cleaned up. CaptureSnapshot()'s
        // main documented use is captureOnStopTracking firing during real Play Mode, where
        // Destroy() is correct - but nothing stops a dev (or an Editor tool) from calling it
        // directly outside Play Mode too, so this needs to work in both.
        private static void SafeDestroy(UnityEngine.Object obj)
        {
            if (Application.isPlaying)
                Destroy(obj);
            else
                DestroyImmediate(obj);
        }

        /// <summary>
        /// Extracts just the position from each sample, in chronological order. Pure function of
        /// the sample list - independent of any live Camera/LineRenderer state - so it's
        /// unit-testable without Play Mode.
        /// </summary>
        public static Vector3[] BuildPathPositions(IReadOnlyList<BeanSample> samples)
        {
            var positions = new Vector3[samples.Count];
            for (int i = 0; i < samples.Count; i++)
                positions[i] = samples[i].Position;
            return positions;
        }

        /// <summary>
        /// Whether a tracker's live sample buffer is full - meaning it's a ring buffer that has
        /// started overwriting its own oldest samples, so anything currently in it may no longer
        /// be the full recorded history (found live 2026-08-08, project 2 T28: a long idle tail
        /// after real movement finished silently evicted the entire interesting path this way).
        /// Pure function, testable without a live GameObject.
        /// </summary>
        public static bool IsBufferAtCapacity(int sampleCount, int maxSamples) =>
            sampleCount > 0 && sampleCount >= maxSamples;

        /// <summary>
        /// Axis-aligned bounding box of every recorded position. Pure function of the sample
        /// list, used to decide how to frame the auto-camera - testable without Play Mode.
        /// </summary>
        public static Bounds ComputePathBounds(IReadOnlyList<BeanSample> samples)
        {
            if (samples.Count == 0)
                return new Bounds(Vector3.zero, Vector3.zero);

            var bounds = new Bounds(samples[0].Position, Vector3.zero);
            for (int i = 1; i < samples.Count; i++)
                bounds.Encapsulate(samples[i].Position);

            return bounds;
        }

        // Below this much depth (world units) along Z, a path is treated as living on a single
        // 2D plane rather than moving through real 3D space - matches this project's existing
        // convention that 2D scenes keep Z at 0 (see DESIGN.md Sec 1 / package.json). Not a
        // perfect general detector (a 2D game rotated onto a different plane would fool it), but
        // right for every test project this has been built against so far - and DimensionMode
        // lets a dev override the guess outright when it isn't, via ResolveIsFlat below.
        private const float FlatPathDepthThreshold = 0.5f;

        /// <summary>Whether a path's bounds are flat enough along Z to treat as a 2D scene.</summary>
        public static bool IsFlatPath(Bounds bounds) => bounds.size.z < FlatPathDepthThreshold;

        /// <summary>
        /// Resolves the flat/2D-vs-3D decision for framing: the DimensionMode override if set,
        /// otherwise the IsFlatPath auto-guess. Pure function, testable without a live GameObject.
        /// </summary>
        public static bool ResolveIsFlat(BeanDimensionMode mode, Bounds bounds)
        {
            switch (mode)
            {
                case BeanDimensionMode.Force2D: return true;
                case BeanDimensionMode.Force3D: return false;
                default: return IsFlatPath(bounds);
            }
        }

        private const float FramingPadding = 1.4f; // ~40% margin so the path isn't touching the frame edge.
        private const float ElevationRatio = 0.75f; // how much "up" blends into the horizontal broadside/behind offset.

        /// <summary>
        /// Computes where to put the capture camera so the whole path (plus a margin) is in
        /// frame: an orthographic front-on view for a flat/2D path, or an elevated 3/4 angle for
        /// a genuinely 3D one (identical to the Side angle below), sized to the path's own bounds
        /// rather than a fixed distance. This is the Auto angle's framing - kept as its own
        /// function (rather than folded into ComputeFramingForAngle) so its original signature/
        /// behavior stays exactly as it was before multi-angle capture existed. Pure function of
        /// the bounds/direction/isFlat/aspect/FOV/minFramingRadius - independent of any live
        /// Camera - so it's unit-testable without Play Mode.
        /// </summary>
        public static SnapshotFraming ComputeFraming(Bounds bounds, Vector3 travelDirection, bool isFlat, float aspect, float fieldOfView, float minFramingRadius)
        {
            if (isFlat)
            {
                Vector3 center = bounds.center;
                float safeAspect = Mathf.Max(aspect, 0.01f);
                float halfHeight = Mathf.Max(bounds.extents.y, bounds.extents.x / safeAspect);
                float orthoSize = Mathf.Max(halfHeight * FramingPadding, minFramingRadius * 0.5f);
                Vector3 position2D = center + Vector3.back * (orthoSize * 4f);
                return new SnapshotFraming(position2D, Quaternion.identity, true, orthoSize);
            }

            return ComputeSideFraming(bounds, travelDirection, fieldOfView, minFramingRadius);
        }

        /// <summary>
        /// Resolves the camera placement for one named BeanSnapshotAngle - Auto delegates to
        /// ComputeFraming unchanged (flat-vs-3D auto-guess); Above/Side/Behind are always
        /// perspective shots at the same distance formula as the 3D branch of ComputeFraming,
        /// just from a different offset direction. Pure function, testable without Play Mode -
        /// same split as ComputeFraming itself.
        /// </summary>
        public static SnapshotFraming ComputeFramingForAngle(Bounds bounds, Vector3 travelDirection, bool isFlat, float aspect, float fieldOfView, float minFramingRadius, BeanSnapshotAngle angle)
        {
            switch (angle)
            {
                case BeanSnapshotAngle.Above:
                    return ComputeAboveFraming(bounds, travelDirection, fieldOfView, minFramingRadius);
                case BeanSnapshotAngle.Behind:
                    return ComputeBehindFraming(bounds, travelDirection, fieldOfView, minFramingRadius);
                case BeanSnapshotAngle.Side:
                    return ComputeSideFraming(bounds, travelDirection, fieldOfView, minFramingRadius);
                case BeanSnapshotAngle.Auto:
                default:
                    return ComputeFraming(bounds, travelDirection, isFlat, aspect, fieldOfView, minFramingRadius);
            }
        }

        private static float ComputePerspectiveDistance(Bounds bounds, float fieldOfView, float minFramingRadius)
        {
            float radius = Mathf.Max(bounds.extents.magnitude, minFramingRadius);
            float halfFovRadians = Mathf.Clamp(fieldOfView, 10f, 150f) * 0.5f * Mathf.Deg2Rad;
            return (radius * FramingPadding) / Mathf.Max(Mathf.Sin(halfFovRadians), 0.1f);
        }

        // The 3D branch's offset direction is derived from the path's own horizontal travel
        // direction (rotated 90 degrees, so the shot is always broadside) rather than a fixed
        // diagonal - a fixed direction foreshortens into a thick stripe whenever a path happens
        // to travel roughly parallel to it, found live during T17/T18 verification in little
        // wings. Falls back to a forward broadside when travelDirection has no horizontal
        // component (e.g. a purely vertical path - a platformer's jump/climb, for instance).
        private static SnapshotFraming ComputeSideFraming(Bounds bounds, Vector3 travelDirection, float fieldOfView, float minFramingRadius)
        {
            float distance = ComputePerspectiveDistance(bounds, fieldOfView, minFramingRadius);

            Vector3 horizontalTravel = new Vector3(travelDirection.x, 0f, travelDirection.z);
            Vector3 broadside = horizontalTravel.sqrMagnitude > 0.0001f
                ? new Vector3(-horizontalTravel.z, 0f, horizontalTravel.x).normalized
                : Vector3.forward;
            Vector3 offsetDirection = (broadside + Vector3.up * ElevationRatio).normalized;

            Vector3 position = bounds.center + offsetDirection * distance;
            Quaternion rotation = Quaternion.LookRotation((bounds.center - position).normalized, Vector3.up);
            return new SnapshotFraming(position, rotation, false, 0f);
        }

        // Camera positioned opposite the path's own travel direction, looking along it - "where
        // the object came from, looking toward where it's going." Falls back to a fixed backward
        // direction when there's no horizontal travel to derive one from.
        private static SnapshotFraming ComputeBehindFraming(Bounds bounds, Vector3 travelDirection, float fieldOfView, float minFramingRadius)
        {
            float distance = ComputePerspectiveDistance(bounds, fieldOfView, minFramingRadius);

            Vector3 horizontalTravel = new Vector3(travelDirection.x, 0f, travelDirection.z);
            Vector3 behind = horizontalTravel.sqrMagnitude > 0.0001f
                ? -horizontalTravel.normalized
                : Vector3.back;
            Vector3 offsetDirection = (behind + Vector3.up * ElevationRatio).normalized;

            Vector3 position = bounds.center + offsetDirection * distance;
            Quaternion rotation = Quaternion.LookRotation((bounds.center - position).normalized, Vector3.up);
            return new SnapshotFraming(position, rotation, false, 0f);
        }

        // Straight-down top-down shot. The up-hint for LookRotation is derived from the path's
        // own horizontal travel direction (so "up" in the resulting image roughly matches the
        // direction of travel) rather than Vector3.up, which is parallel to the look direction
        // here and would leave the roll effectively undefined.
        private static SnapshotFraming ComputeAboveFraming(Bounds bounds, Vector3 travelDirection, float fieldOfView, float minFramingRadius)
        {
            float distance = ComputePerspectiveDistance(bounds, fieldOfView, minFramingRadius);

            Vector3 position = bounds.center + Vector3.up * distance;
            Vector3 horizontalTravel = new Vector3(travelDirection.x, 0f, travelDirection.z);
            Vector3 upHint = horizontalTravel.sqrMagnitude > 0.0001f ? horizontalTravel.normalized : Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(Vector3.down, upHint);
            return new SnapshotFraming(position, rotation, false, 0f);
        }

        /// <summary>
        /// Resolves where a single-angle PNG gets written - explicit filePath if set (used as-is,
        /// no uniqueness applied), otherwise a default under
        /// BeanArtifactPaths.RootDirectory/BeanSnapshots/ (project root, not
        /// Application.persistentDataPath - see BeanArtifactPaths for why) that's unique both
        /// across repeated runs (timestamp, sorts a folder of many runs chronologically for easy
        /// comparison) and across duplicate GameObject names alive at once (uniqueToken - two
        /// clones capturing in the same frame/millisecond still don't collide, matching
        /// BeanLogger's CSV naming - see DESIGN.md Sec 13/T13). Unchanged since before multi-angle
        /// capture existed - still what a single (or Auto-only) capture uses. Pure function (given
        /// a timestamp/token) so it's testable without a live GameObject.
        /// </summary>
        public static string ResolveSnapshotPath(string explicitPath, string objectName, string uniqueToken, DateTime captureTimeUtc)
        {
            if (!string.IsNullOrEmpty(explicitPath))
                return explicitPath;

            string timestamp = captureTimeUtc.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string fileName = $"{timestamp}_{objectName}_{uniqueToken}_bean_snapshot.png";
            return BeanArtifactPaths.ResolveDefaultPath(SnapshotSubfolder, fileName);
        }

        /// <summary>
        /// Resolves where one angle's PNG gets written when capturing more than one angle in a
        /// single call - always under BeanArtifactPaths.RootDirectory/BeanSnapshots/, named
        /// "{timestamp}.{n}_{objectName}_{angleName}_{uniqueToken}_bean_snapshot.png" (1-based n)
        /// so every file from the same CaptureSnapshot() call shares a timestamp and sorts/groups
        /// together, with the angle name making each one identifiable at a glance. An explicit
        /// filePath override is deliberately NOT honored here (unlike the single-angle
        /// ResolveSnapshotPath) - one fixed path can't safely back more than one output file
        /// without silently overwriting itself between angles, so multi-angle capture always uses
        /// the default location. Pure function, testable without a live GameObject.
        /// </summary>
        public static string ResolveMultiAngleSnapshotPath(string explicitPath, string objectName, BeanSnapshotAngle angle, int angleIndex, int angleCount, string uniqueToken, DateTime captureTimeUtc)
        {
            string timestamp = captureTimeUtc.ToString("yyyyMMdd_HHmmss_fff", CultureInfo.InvariantCulture);
            string fileName = $"{timestamp}.{angleIndex + 1}_{objectName}_{angle}_{uniqueToken}_bean_snapshot.png";
            return BeanArtifactPaths.ResolveDefaultPath(SnapshotSubfolder, fileName);
        }
    }

    /// <summary>Camera placement computed by BeanSnapshotExporter.ComputeFraming/ComputeFramingForAngle.</summary>
    public readonly struct SnapshotFraming
    {
        public readonly Vector3 Position;
        public readonly Quaternion Rotation;
        public readonly bool Orthographic;
        public readonly float OrthographicSize;

        public SnapshotFraming(Vector3 position, Quaternion rotation, bool orthographic, float orthographicSize)
        {
            Position = position;
            Rotation = rotation;
            Orthographic = orthographic;
            OrthographicSize = orthographicSize;
        }
    }
}
