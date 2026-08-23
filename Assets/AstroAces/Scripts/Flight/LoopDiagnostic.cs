using System.Collections.Generic;
using UnityEngine;
using UTI;

namespace AstroAces.Flight
{
    /// <summary>
    /// TEMPORARY. Feeds real per-FixedUpdate flight data into a BeanTracker's CustomCapture
    /// (see the vendored UTI toolkit in Assets/AstroAces/ThirdParty/UTI, from
    /// github.com/DataFright/Unity-Testing-Inspector), so a Play Mode run can be inspected
    /// afterward from the exported CSV instead of guessed at from a Python re-implementation
    /// of the flight code -- built specifically to settle whether AA-005's loop behavior is
    /// really the control law or a tuning limit, after a from-scratch simulation and the
    /// user's direct testing disagreed with each other (see BUGS.md).
    ///
    /// CSV lands at "<project root>/UTI/BeanLogs/*.csv" (BeanArtifactPaths) -- readable
    /// directly off disk, no build step, no manual export.
    ///
    /// Delete alongside the debug readouts once this question is settled and Phase 13's
    /// real tests exist.
    /// </summary>
    [RequireComponent(typeof(BeanTracker))]
    [RequireComponent(typeof(AircraftState))]
    [RequireComponent(typeof(AircraftEngine))]
    [RequireComponent(typeof(AircraftPhysics))]
    public class LoopDiagnostic : MonoBehaviour
    {
        AircraftState state;
        AircraftEngine engine;
        AircraftPhysics physics;
        AircraftInput input;               // null-guarded: absent on an AI-piloted aircraft
        AircraftAimController aim;

        void Awake()
        {
            state = GetComponent<AircraftState>();
            engine = GetComponent<AircraftEngine>();
            physics = GetComponent<AircraftPhysics>();
            input = GetComponent<AircraftInput>();
            aim = GetComponent<AircraftAimController>();

            GetComponent<BeanTracker>().CustomCapture = Capture;
        }

        Dictionary<string, float> Capture(GameObject go)
        {
            ControlCommand cmd = physics.LastCommand;
            return new Dictionary<string, float>
            {
                ["aoa"] = state.AngleOfAttack,
                ["bank"] = state.BankAngle,
                ["pitchAngleDeg"] = Mathf.Asin(Mathf.Clamp(transform.forward.y, -1f, 1f)) * Mathf.Rad2Deg,
                ["altitudeM"] = state.AltitudeMeters,
                ["speedMph"] = state.AirspeedMps * Aero.MpsToMph,
                ["throttle"] = engine.Throttle,
                ["elevator"] = cmd.elevator,
                ["aileron"] = cmd.aileron,
                ["aoaLimitFactor"] = cmd.aoaLimitFactor,
                ["pitchRateDegPerSec"] = state.Rates.pitchUp,
                ["rollRateDegPerSec"] = state.Rates.rollRight,
                // Added to chase the "crosshair starts off-centre, same every run regardless
                // of pre-Play mouse position" report (BUGS.md AA-006) -- same offNoseDeg
                // formula as Phase2DebugReadout so this is directly comparable to what's on
                // screen. mouseDeltaX/Y being nonzero with nobody touching the mouse would
                // point at the input side; offNoseDeg growing while mouseDelta stays exactly
                // zero would instead point at the aircraft's own nose moving away from a
                // DesiredDirection that never changed.
                ["mouseDeltaX"] = input != null ? input.MouseDelta.x : 0f,
                ["mouseDeltaY"] = input != null ? input.MouseDelta.y : 0f,
                ["offNoseDeg"] = aim != null ? Vector3.Angle(transform.forward, aim.DesiredDirection) : 0f,
                ["desiredDirX"] = aim != null ? aim.DesiredDirection.x : 0f,
                ["desiredDirY"] = aim != null ? aim.DesiredDirection.y : 0f,
                ["desiredDirZ"] = aim != null ? aim.DesiredDirection.z : 0f,
                ["forwardX"] = transform.forward.x,
                ["forwardY"] = transform.forward.y,
                ["forwardZ"] = transform.forward.z,
            };
        }
    }
}
