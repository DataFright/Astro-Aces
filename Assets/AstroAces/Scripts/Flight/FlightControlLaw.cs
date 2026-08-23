using UnityEngine;

namespace AstroAces.Flight
{
    /// <summary>
    /// Body rotation rates in PILOT convention, degrees/second. Unity's local angular
    /// velocity uses different signs on two of three axes, and mixing them up produces an
    /// aircraft that rolls away from the mouse -- which reads as "the physics is broken"
    /// when it is really just a minus sign. Convert here, once, and never inline it.
    /// </summary>
    public struct BodyRates
    {
        public float rollRight;   // + = right wing dropping
        public float pitchUp;     // + = nose rising
        public float yawRight;    // + = nose swinging right

        public BodyRates(float rollRight, float pitchUp, float yawRight)
        {
            this.rollRight = rollRight; this.pitchUp = pitchUp; this.yawRight = yawRight;
        }

        /// <param name="localAngularVelocity">transform.InverseTransformDirection(rb.angularVelocity), rad/s.</param>
        public static BodyRates FromUnity(Vector3 localAngularVelocity) => new BodyRates(
            -localAngularVelocity.z * Mathf.Rad2Deg,   // +Z rotation lifts the right wing
            -localAngularVelocity.x * Mathf.Rad2Deg,   // +X rotation drops the nose
             localAngularVelocity.y * Mathf.Rad2Deg);  // +Y rotation swings the nose right

        /// <summary>Back to a Unity local angular velocity in rad/s.</summary>
        public Vector3 ToUnity() => new Vector3(
            -pitchUp * Mathf.Deg2Rad,
             yawRight * Mathf.Deg2Rad,
            -rollRight * Mathf.Deg2Rad);
    }

    /// <summary>Normalised control-surface commands, -1..1, plus values the HUD/debug want.</summary>
    public struct ControlCommand
    {
        public float elevator;   // + = nose up
        public float aileron;    // + = roll right
        public float rudder;     // + = nose right

        public float horizontalErrorDeg;
        public float verticalErrorDeg;
        public float desiredBankDeg;
        public float rollErrorDeg;
        public float aoaLimitFactor;   // 1 = free, 0 = stall protection fully engaged
    }

    /// <summary>
    /// Turns "where the player wants to fly" into elevator / aileron / rudder.
    ///
    /// This is the middle of the three layers named in DESIGN.md:
    ///     intent (mouse aim)  ->  THIS  ->  physics (forces and rates)
    /// It never touches a Rigidbody and never reads input. Pure function, fully testable.
    ///
    /// The aircraft turns by BANKING, not by yawing. Horizontal error commands bank angle;
    /// pitch then pulls the nose around the turn. Rudder only coordinates. If the aircraft
    /// ever feels like it is sliding around corners, the fix is more bank or more side drag,
    /// never more yaw.
    /// </summary>
    public static class FlightControlLaw
    {
        /// <summary>
        /// Signed bank angle in degrees, POSITIVE = right wing down.
        /// Degenerate when the nose points straight up or down, so the caller passes in the
        /// previous value to hold through the singularity.
        /// </summary>
        public static float BankAngle(Transform t, float fallbackDeg = 0f)
        {
            Vector3 horizonRight = Vector3.Cross(Vector3.up, t.forward);
            if (horizonRight.sqrMagnitude < 1e-5f) return fallbackDeg;   // nose vertical
            return -Vector3.SignedAngle(horizonRight.normalized, t.right, t.forward);
        }

        public static ControlCommand Compute(Vector3 localTargetDirection, float currentBankDeg,
                                             BodyRates rates, float aoaDeg, float sideSlipMps,
                                             AircraftConfig cfg)
        {
            ControlCommand c = default;

            // --- 1. Where is the target, relative to the nose? -------------------------
            // Forward component is floored so a target abeam or behind saturates the error
            // at roughly +/-90 deg instead of wrapping to the wrong sign.
            Vector3 d = localTargetDirection.sqrMagnitude > 1e-6f
                ? localTargetDirection.normalized : Vector3.forward;
            float fwd = Mathf.Max(d.z, 0.05f);

            c.horizontalErrorDeg = Mathf.Atan2(d.x, fwd) * Mathf.Rad2Deg;
            c.verticalErrorDeg = Mathf.Atan2(d.y, fwd) * Mathf.Rad2Deg;

            // --- 2. Horizontal error becomes BANK, not yaw ----------------------------
            c.desiredBankDeg = Mathf.Clamp(c.horizontalErrorDeg * cfg.bankGain,
                                           -cfg.maxBankAngle, cfg.maxBankAngle);
            c.rollErrorDeg = Mathf.DeltaAngle(currentBankDeg, c.desiredBankDeg);

            c.aileron = Mathf.Clamp(c.rollErrorDeg * cfg.rollKp - rates.rollRight * cfg.rollKd, -1f, 1f);

            // --- 3. Pitch pulls the nose around --------------------------------------
            // Banking tilts the lift vector, so some of it stops fighting gravity. A little
            // automatic back-pressure proportional to bank recovers most of the lost
            // altitude. Deliberately not enough to fully hold altitude for the player.
            float pitchDemand = c.verticalErrorDeg * cfg.pitchGain
                              + Mathf.Abs(currentBankDeg) * cfg.turnCompensationStrength;

            float elevator = Mathf.Clamp(pitchDemand * cfg.pitchKp - rates.pitchUp * cfg.pitchKd, -1f, 1f);

            // Hold back the pull until the wings have actually rolled to where they were
            // told to go, otherwise the aircraft pitches up out of level flight before it
            // has banked and the turn looks like a cartoon zoom-climb.
            float align = Mathf.Cos(Mathf.Clamp(Mathf.Abs(c.rollErrorDeg), 0f, 90f) * Mathf.Deg2Rad);
            elevator *= Mathf.Max(align, cfg.rollAlignmentFloor);

            // --- 4. Stall protection --------------------------------------------------
            c.aoaLimitFactor = Aero.AoALimiter(aoaDeg, cfg.safeAoADeg, cfg.criticalAoADeg, cfg.elevatorStallFloor);
            c.elevator = ApplyAoALimiter(elevator, aoaDeg, c.aoaLimitFactor);

            // --- 5. Rudder: coordination only ----------------------------------------
            // Slipping right means the nose is left of the flight path, so yaw right to
            // line them back up. This is what kills the "flying sideways" look.
            c.rudder = Mathf.Clamp(
                c.horizontalErrorDeg * cfg.yawKp
                - rates.yawRight * cfg.yawKd
                + sideSlipMps * cfg.coordinationStrength, -1f, 1f);

            return c;
        }

        /// <summary>
        /// Fades out only the command that would make |AoA| worse; a command pushing away
        /// from the stall always stays available, so the aircraft is always recoverable.
        /// Shared between the mouse-driven path above and AircraftPhysics's keyboard
        /// override (see AA-004 in BUGS.md) -- stall protection must apply to manual pitch
        /// too, not just mouse-driven pitch, or holding E lets the player force a stall the
        /// mouse could never cause.
        /// </summary>
        public static float ApplyAoALimiter(float rawElevator, float aoaDeg, float aoaLimitFactor)
        {
            bool wouldWorsenAoA = (rawElevator > 0f && aoaDeg > 0f) || (rawElevator < 0f && aoaDeg < 0f);
            return wouldWorsenAoA ? rawElevator * aoaLimitFactor : rawElevator;
        }

        /// <summary>
        /// Advance body rates one physics step toward what the commands ask for.
        ///
        /// This is rate control, not torque control: the target rate is approached with a
        /// first-order response (damping) under an acceleration cap (authority). It cannot
        /// oscillate, it honours maxRate exactly, and it is immune to the Rigidbody's
        /// inertia tensor -- which is why DESIGN.md picks it over AddRelativeTorque.
        /// </summary>
        public static BodyRates StepRates(BodyRates current, ControlCommand cmd,
                                          float speedFactor, float dt, AircraftConfig cfg)
        {
            return new BodyRates(
                Approach(current.rollRight, cmd.aileron * cfg.maxRollRate * speedFactor,
                         cfg.rollDamping, cfg.rollAuthority * speedFactor, dt),
                Approach(current.pitchUp, cmd.elevator * cfg.maxPitchRate * speedFactor,
                         cfg.pitchDamping, cfg.pitchAuthority * speedFactor, dt),
                Approach(current.yawRight, cmd.rudder * cfg.maxYawRate * speedFactor,
                         cfg.yawDamping, cfg.yawAuthority * speedFactor, dt));
        }

        static float Approach(float current, float target, float damping, float maxAccel, float dt)
        {
            float accel = Mathf.Clamp((target - current) * damping, -maxAccel, maxAccel);
            return current + accel * dt;
        }

        /// <summary>
        /// Advance the persistent mouse aim and return the desired flight direction.
        ///
        /// The aim is world-referenced and NEVER auto-centres: stop moving the mouse and the
        /// aircraft keeps turning until it gets there. It is clamped to a cone around the
        /// nose, and the clamp is written BACK into aimYaw/aimPitch -- without that the aim
        /// silently accumulates error off-screen and the aircraft chases a ghost.
        /// </summary>
        public static Vector3 StepAim(ref float aimYawDeg, ref float aimPitchDeg,
                                      Vector2 mouseDelta, Transform aircraft, AircraftConfig cfg)
        {
            aimYawDeg += mouseDelta.x * cfg.mouseSensitivity;
            aimPitchDeg -= mouseDelta.y * cfg.mouseSensitivity;
            aimPitchDeg = Mathf.Clamp(aimPitchDeg, -cfg.maxAimPitch, cfg.maxAimPitch);
            if (aimYawDeg > 360f) aimYawDeg -= 360f;
            else if (aimYawDeg < -360f) aimYawDeg += 360f;

            Vector3 dir = Quaternion.Euler(aimPitchDeg, aimYawDeg, 0f) * Vector3.forward;

            float off = Vector3.Angle(aircraft.forward, dir);
            if (off > cfg.maxAimConeAngle)
            {
                dir = Vector3.RotateTowards(aircraft.forward, dir,
                                            cfg.maxAimConeAngle * Mathf.Deg2Rad, 0f).normalized;
                // Re-derive the stored angles from the clamped direction so the aim cannot
                // wind up past the cone while the player keeps moving the mouse.
                aimPitchDeg = -Mathf.Asin(Mathf.Clamp(dir.y, -1f, 1f)) * Mathf.Rad2Deg;
                aimYawDeg = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
            }
            return dir;
        }
    }
}
