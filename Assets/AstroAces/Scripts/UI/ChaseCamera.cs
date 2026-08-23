using UnityEngine;
using AstroAces.Core;
using AstroAces.Flight;

namespace AstroAces.UI
{
    /// <summary>
    /// Third-person chase camera, DESIGN.md Sec 5. Runs in LateUpdate -- the aircraft's
    /// Rigidbody uses interpolation (AircraftPhysics.Awake), so reading its transform any
    /// earlier in the frame reads last physics step's position, not this rendered frame's
    /// interpolated one, and judders.
    ///
    /// Replaces Phase 2's placeholder (Main Camera rigidly parented under the aircraft, zero
    /// smoothing) -- see BUGS.md AA-008 and the Phase 4 note in BUILD_PLAN.md for why that
    /// placeholder's rigid roll-following flipped the whole screen upside-down during any
    /// maneuver that inverts the aircraft (a loop).
    ///
    /// DELIBERATE DECISION on roll-following (the thing AA-008 asked this phase to decide
    /// explicitly, not inherit by accident): this camera DOES fully follow the aircraft's
    /// orientation, including full inversion during a loop -- that is standard, expected
    /// chase-cam behavior in a flight game (real War Thunder/Ace Combat cameras roll and
    /// invert with the aircraft too). What Phase 2's placeholder was actually missing was
    /// smoothing, not a roll limit -- an instant, unsmoothed rotation snap is what made the
    /// inversion disorienting (confirmed: the aim-tracking code itself stayed within 1.3 deg
    /// of the nose through the same inverted portion, so the aircraft's own state was never
    /// the problem). Rotation is Slerped (~10/s, DESIGN.md Sec 5), so even a fast loop's
    /// rotation eases in rather than snapping instantly. If this still reads as too
    /// disorienting after real testing, the next lever is limiting/blending the up vector
    /// during extreme bank -- not applied preemptively without evidence it is still needed.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class ChaseCamera : MonoBehaviour
    {
        [SerializeField] Transform target;

        [Header("Follow")]
        [Tooltip("Offset from the target in the TARGET's own local space: 3m up, 8m back.")]
        [SerializeField] Vector3 localOffset = new Vector3(0f, 3f, -8f);
        [Tooltip("Position follow rate, 1/second. Higher = tighter follow.")]
        [SerializeField] float positionLerpPerSecond = 8f;
        [Tooltip("Rotation follow rate, 1/second. Higher = snappier turning.")]
        [SerializeField] float rotationSlerpPerSecond = 10f;

        [Header("Free Look (right mouse button)")]
        [SerializeField] float freeLookSensitivity = 0.15f;
        [SerializeField] float maxFreeLookYawDeg = 120f;
        [SerializeField] float maxFreeLookPitchDeg = 70f;
        [Tooltip("Seconds to return to centre after releasing free look.")]
        [SerializeField] float freeLookReturnSeconds = 0.25f;

        [Header("Zoom (Caps Lock)")]
        [SerializeField] float normalFieldOfView = 60f;
        [SerializeField] float zoomedFieldOfView = 24f;

        [Header("Clip Planes")]
        [SerializeField] float nearClip = 0.3f;
        [SerializeField] float farClip = 12000f;

        Camera cam;
        AircraftInput input;   // null-guarded -- absent on an AI-piloted target

        float freeLookYawDeg;
        float freeLookPitchDeg;
        bool zoomed;

        void Awake()
        {
            cam = GetComponent<Camera>();
            cam.nearClipPlane = nearClip;
            cam.farClipPlane = farClip;
            cam.fieldOfView = normalFieldOfView;
            cam.cullingMask = Layers.MainCameraMask;
        }

        void OnEnable()
        {
            ResolveInput();

            if (target != null)
            {
                // Snap immediately rather than Lerping in from wherever the camera happens to
                // sit in the scene (e.g. the Editor's default placement) on the first frame.
                transform.SetPositionAndRotation(target.TransformPoint(localOffset), target.rotation);
            }
        }

        /// <summary>Lets a spawn system (or the Editor, for now) assign the aircraft to
        /// follow without a hand-wired scene reference baked into every scene.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            ResolveInput();
        }

        void ResolveInput() => input = target != null ? target.GetComponent<AircraftInput>() : null;

        void LateUpdate()
        {
            if (target == null) return;

            UpdateFreeLook();
            UpdateFollow();
            UpdateZoom();
        }

        void UpdateFreeLook()
        {
            if (input != null && input.FreeLookHeld)
            {
                freeLookYawDeg = Mathf.Clamp(freeLookYawDeg + input.MouseDelta.x * freeLookSensitivity,
                                              -maxFreeLookYawDeg, maxFreeLookYawDeg);
                freeLookPitchDeg = Mathf.Clamp(freeLookPitchDeg - input.MouseDelta.y * freeLookSensitivity,
                                                -maxFreeLookPitchDeg, maxFreeLookPitchDeg);
            }
            else
            {
                float returnT = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(freeLookReturnSeconds, 0.01f));
                freeLookYawDeg = Mathf.Lerp(freeLookYawDeg, 0f, returnT);
                freeLookPitchDeg = Mathf.Lerp(freeLookPitchDeg, 0f, returnT);
            }
        }

        void UpdateFollow()
        {
            // Free-look originally just panned the VIEW from the fixed chase spot (composed
            // freeLookOffset into the rotation only, position never moved) -- reported by the
            // user 2026-08-22 as "not true free look": it let you look behind you, but not
            // actually swing around to see the wings, underside or cockpit, since the camera
            // never left its spot behind the tail. Orbit the camera around the aircraft
            // instead, at the same distance as the normal chase offset, and look back at the
            // aircraft from wherever that lands -- at zero free-look this reduces to exactly
            // the same position, but a non-zero offset actually rotates the camera's position
            // to a new vantage point around the ship, not just a new look direction from the
            // old one.
            bool orbiting = Mathf.Abs(freeLookYawDeg) > 0.05f || Mathf.Abs(freeLookPitchDeg) > 0.05f;

            Vector3 desiredPosition;
            Quaternion desiredRotation;

            if (orbiting)
            {
                Quaternion freeLookOffset = Quaternion.Euler(freeLookPitchDeg, freeLookYawDeg, 0f);
                Vector3 orbitOffset = freeLookOffset * localOffset;
                desiredPosition = target.TransformPoint(orbitOffset);

                Vector3 toTarget = target.position - desiredPosition;
                desiredRotation = toTarget.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(toTarget.normalized, target.up)
                    : target.rotation;
            }
            else
            {
                desiredPosition = target.TransformPoint(localOffset);
                desiredRotation = target.rotation;
            }

            float posT = 1f - Mathf.Exp(-positionLerpPerSecond * Time.deltaTime);
            float rotT = 1f - Mathf.Exp(-rotationSlerpPerSecond * Time.deltaTime);

            transform.position = Vector3.Lerp(transform.position, desiredPosition, posT);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotT);
        }

        void UpdateZoom()
        {
            if (input != null && input.ZoomToggled)
                zoomed = !zoomed;

            cam.fieldOfView = zoomed ? zoomedFieldOfView : normalFieldOfView;
        }
    }
}
