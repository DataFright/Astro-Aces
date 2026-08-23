using UnityEngine;

namespace AstroAces.Flight
{
    /// <summary>
    /// Owns the persistent "where the player wants to fly" direction -- the INTENT layer
    /// from DESIGN.md §1.1. Never rotates the aircraft itself; FlightControlLaw and
    /// AircraftPhysics do that from DesiredDirection in Phase 3.
    ///
    /// Works two ways depending on what's on the GameObject:
    ///   - With an AircraftInput sibling: reads mouse delta every frame via
    ///     FlightControlLaw.StepAim, which never auto-centres -- stop moving the mouse and
    ///     the direction holds exactly where it was.
    ///   - Without one (no AircraftInput component -- the enemy case): Update() does
    ///     nothing, and whatever drives this aircraft (EnemyPilot, Phase 8) calls
    ///     SetDesiredDirection directly. Same control law, same aircraft, different pilot.
    /// </summary>
    public class AircraftAimController : MonoBehaviour
    {
        [SerializeField] AircraftConfig cfg;

        AircraftInput input;
        float aimYawDeg;
        float aimPitchDeg;

        public Vector3 DesiredDirection { get; private set; }

        void Awake()
        {
            input = GetComponent<AircraftInput>();   // null is valid -- see class remarks

            // Start the aim exactly on the nose, not at (0,0) world-yaw/pitch, so an
            // aircraft spawned facing any direction doesn't immediately show a huge aim
            // error on the very first frame.
            Vector3 f = transform.forward;
            aimYawDeg = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;
            aimPitchDeg = -Mathf.Asin(Mathf.Clamp(f.y, -1f, 1f)) * Mathf.Rad2Deg;
            DesiredDirection = f;
        }

        void Update()
        {
            if (input == null) return;   // externally driven (AI) -- see SetDesiredDirection

            // DESIGN.md Sec 5: free-look (right mouse) orbits the camera around the aircraft
            // "Flight direction is untouched." That decoupling only actually exists on the
            // camera side (ChaseCamera reads FreeLookHeld) -- this component had no matching
            // gate, so the exact same mouse delta was ALSO being fed into StepAim the whole
            // time, meaning holding right-click to look around silently kept steering the
            // aircraft too. Freeze the aim while free-look is held rather than centre or
            // reset it -- consistent with the aim's own "never auto-centres" rule elsewhere;
            // it simply holds at wherever it was and resumes from there on release.
            if (input.FreeLookHeld) return;

            DesiredDirection = FlightControlLaw.StepAim(ref aimYawDeg, ref aimPitchDeg,
                                                         input.MouseDelta, transform, cfg);
        }

        /// <summary>For AI or any non-mouse driver. Keeps the internal yaw/pitch in sync so
        /// switching a component between mouse and AI control later never causes a jump.</summary>
        public void SetDesiredDirection(Vector3 direction)
        {
            if (direction.sqrMagnitude < 1e-6f) return;
            DesiredDirection = direction.normalized;
            aimYawDeg = Mathf.Atan2(DesiredDirection.x, DesiredDirection.z) * Mathf.Rad2Deg;
            aimPitchDeg = -Mathf.Asin(Mathf.Clamp(DesiredDirection.y, -1f, 1f)) * Mathf.Rad2Deg;
        }
    }
}
