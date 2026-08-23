using System;
using System.Collections.Generic;
using UnityEngine;

namespace UTI
{
    public enum BeanCaptureMode
    {
        EveryUpdate,
        EveryFixedUpdate,
        EveryNSeconds
    }

    /// <summary>Attach to any GameObject to capture its transform over time into a BeanBuffer.</summary>
    public class BeanTracker : MonoBehaviour
    {
        [SerializeField] private BeanCaptureMode captureMode = BeanCaptureMode.EveryUpdate;
        [SerializeField] private float captureInterval = 0.5f;
        [SerializeField] private bool captureRotation = true;
        [SerializeField] private int maxSamples = 1000;
        [SerializeField] private bool startTrackingOnEnable = true;

        public BeanCaptureMode CaptureMode { get => captureMode; set => captureMode = value; }
        public float CaptureInterval { get => captureInterval; set => captureInterval = value; }
        public bool CaptureRotation { get => captureRotation; set => captureRotation = value; }
        public int MaxSamples { get => maxSamples; set => maxSamples = value; }
        public bool StartTrackingOnEnable { get => startTrackingOnEnable; set => startTrackingOnEnable = value; }

        // Left null by default - only allocate a Dictionary per sample if someone actually opts in.
        public Func<GameObject, Dictionary<string, float>> CustomCapture;

        public event Action<BeanSample> OnSample;

        // Lets a paired component (e.g. BeanSnapshotExporter) react to tracking ending without
        // polling IsTracking every frame. Only fires on an actual tracking->not-tracking
        // transition, not on a redundant StopTracking() call while already stopped.
        public event Action OnStopTracking;

        private BeanBuffer buffer;
        private float intervalTimer;
        private int tickIndex;
        private bool isTracking;

        // Lazy rather than allocated in OnEnable: MonoBehaviour lifecycle callbacks don't fire
        // in Edit Mode without [ExecuteAlways], which we deliberately don't want here (that
        // would make Update()/FixedUpdate() run - and burn CPU tracking - just from a Bean
        // sitting in a scene while editing, not only during Play). This keeps Buffer valid
        // regardless of whether OnEnable has run yet.
        private BeanBuffer Buffer => buffer ??= new BeanBuffer(Mathf.Max(1, maxSamples));

        public IReadOnlyList<BeanSample> Samples => Buffer;
        public bool IsTracking => isTracking;

        private void OnEnable()
        {
            if (startTrackingOnEnable)
                StartTracking();
        }

        // Unity calls Reset() when this component is first added in the Editor (and from its
        // context menu's "Reset" entry) - the intended hook for "seed sensible initial field
        // values," not a runtime behavior. Deliberately does NOT run at runtime/OnEnable - what's
        // visible in the Inspector is always exactly what's used, no hidden "unless
        // BeanConfig.txt says otherwise" behavior for an already-existing Bean. Reset() only ever
        // fires from the Editor (component just added, or its context menu's "Reset" entry), so
        // this never affects a build either way.
        private void Reset() => ApplyConfigDefaults(BeanConfig.Load());

        /// <summary>
        /// Applies a BeanConfig's defaults to this tracker's serialized fields - a no-op if
        /// config is null. Separated from Reset() so it's testable directly without touching the
        /// real filesystem. See BeanConfig/CONFIG.md.
        /// </summary>
        public void ApplyConfigDefaults(BeanConfig config)
        {
            if (config == null)
                return;

            captureMode = config.DefaultCaptureMode;
            captureInterval = config.DefaultCaptureInterval;
        }

        private void Update() => SimulateFrame(Time.deltaTime);

        private void FixedUpdate() => SimulateFixedFrame();

        /// <summary>
        /// Advances tracking by one fixed-timestep tick. This is what FixedUpdate() calls -
        /// exposed publicly, same reasoning as SimulateFrame(), so EveryFixedUpdate capture is
        /// testable deterministically without a real physics tick (previously the one capture
        /// mode with no automated coverage - see DESIGN.md Sec 13/T12).
        /// </summary>
        public void SimulateFixedFrame()
        {
            if (isTracking && captureMode == BeanCaptureMode.EveryFixedUpdate)
                Capture();
        }

        /// <summary>
        /// Advances tracking by one frame of the given length. This is what Update() calls with
        /// Time.deltaTime - exposed publicly so EveryUpdate/EveryNSeconds behavior is testable
        /// deterministically without Play Mode, and so a Bean can be driven by a custom
        /// simulation loop instead of Unity's player loop if needed.
        /// </summary>
        public void SimulateFrame(float deltaTime)
        {
            if (!isTracking)
                return;

            if (captureMode == BeanCaptureMode.EveryUpdate)
            {
                Capture();
            }
            else if (captureMode == BeanCaptureMode.EveryNSeconds)
            {
                intervalTimer += deltaTime;
                if (intervalTimer >= captureInterval)
                {
                    intervalTimer -= captureInterval;
                    Capture();
                }
            }
        }

        public void StartTracking() => isTracking = true;

        public void StopTracking()
        {
            if (!isTracking)
                return;

            isTracking = false;
            OnStopTracking?.Invoke();
        }

        public void ClearSamples()
        {
            Buffer.Clear();
            tickIndex = 0;
        }

        private void Capture()
        {
            Dictionary<string, float> extras = null;
            if (CustomCapture != null)
            {
                // CustomCapture is caller-supplied code (a system boundary, unlike everything
                // else in this method) - a bug in it shouldn't break tracking itself. Without
                // this, a throwing delegate would propagate out of Capture() and skip the rest
                // of this method entirely (no sample added, tickIndex not advanced, OnSample
                // never fires) for every single frame it's called, silently corrupting the whole
                // recording rather than just missing one field.
                try
                {
                    extras = CustomCapture(gameObject);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[Bean] CustomCapture threw on '{gameObject.name}' - capturing this sample without extras: {ex.Message}");
                }
            }

            Quaternion rotation = captureRotation ? transform.rotation : Quaternion.identity;
            BeanSample sample = new BeanSample(tickIndex, Time.time, transform.position, rotation, extras);

            Buffer.Add(sample);
            tickIndex++;
            OnSample?.Invoke(sample);
        }
    }
}
