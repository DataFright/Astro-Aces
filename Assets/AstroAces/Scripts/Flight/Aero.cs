using UnityEngine;

namespace AstroAces.Flight
{
    /// <summary>
    /// Pure aerodynamic maths. No MonoBehaviour, no scene, no side effects -- so Edit Mode
    /// tests can assert the headline flight numbers without ever entering Play Mode.
    ///
    /// SIGN CONVENTIONS (get these wrong and the aircraft flies inverted):
    ///   local +X = right wing        local +Y = canopy/up        local +Z = nose
    ///   Angle of attack : POSITIVE = nose above the flight path.
    ///   Sideslip        : POSITIVE = airflow pushing the aircraft to its right.
    /// </summary>
    public static class Aero
    {
        public const float Gravity = 9.81f;
        public const float MpsToMph = 2.2369363f;
        public const float MetersToFeet = 3.2808399f;

        // ------------------------------------------------------------------ atmosphere

        /// <summary>
        /// Air density as a fraction of sea level, falling off exponentially with altitude.
        /// THIS FUNCTION IS THE FLIGHT CEILING. Lift, drag and thrust all scale by it, so
        /// high up the aircraft simply runs out of air rather than hitting an invisible wall.
        /// </summary>
        public static float DensityAt(float altitudeMeters, float scaleHeight, float minDensity)
        {
            if (scaleHeight <= 0.01f) return 1f;
            float d = Mathf.Exp(-Mathf.Max(0f, altitudeMeters) / scaleHeight);
            return Mathf.Max(d, minDensity);
        }

        public static float DensityAt(AircraftConfig cfg, float altitudeMeters)
            => DensityAt(altitudeMeters, cfg.densityScaleHeight, cfg.minDensity);

        // ------------------------------------------------------------------ flow angles

        /// <summary>Angle of attack in degrees, nose-up positive. Input is velocity expressed
        /// in aircraft local space (transform.InverseTransformDirection(rb.linearVelocity)).</summary>
        public static float AngleOfAttack(Vector3 localVelocity)
        {
            if (localVelocity.sqrMagnitude < 0.25f) return 0f;   // parked / falling: undefined
            return Mathf.Atan2(-localVelocity.y, Mathf.Max(localVelocity.z, 0.1f)) * Mathf.Rad2Deg;
        }

        /// <summary>Sideslip in m/s, positive = drifting right. This is what side drag kills.</summary>
        public static float SideSlip(Vector3 localVelocity) => localVelocity.x;

        // ------------------------------------------------------------------ lift curve

        /// <summary>
        /// Lift coefficient vs angle of attack. Linear up to criticalAoA (Cl = 1.0 exactly at
        /// safeAoA, peaking at criticalAoA/safeAoA), then collapsing to postStallLift.
        /// Symmetric for negative AoA so inverted flight works.
        /// </summary>
        public static float LiftCurve(float aoaDeg, float safeAoA, float criticalAoA,
                                      float postStallAoA, float postStallLift)
        {
            if (safeAoA <= 0.01f) return 0f;
            float a = Mathf.Abs(aoaDeg);
            float sign = Mathf.Sign(aoaDeg);
            float clPeak = criticalAoA / safeAoA;

            if (a <= criticalAoA) return sign * (a / safeAoA);

            float t = Mathf.InverseLerp(criticalAoA, Mathf.Max(postStallAoA, criticalAoA + 1f), a);
            return sign * Mathf.Lerp(clPeak, postStallLift, t);
        }

        public static float LiftCurve(AircraftConfig cfg, float aoaDeg)
            => LiftCurve(aoaDeg, cfg.safeAoADeg, cfg.criticalAoADeg, cfg.postStallAoADeg, cfg.postStallLift);

        /// <summary>
        /// Elevator authority multiplier: 1 below safeAoA, fading toward 0 at criticalAoA and
        /// beyond, floored at <paramref name="floor"/> rather than hitting exactly 0.
        ///
        /// The floor exists because "fades all the way to 0" turned a full-throttle, full-pull
        /// loop into a ~28s crawl (verified by simulation, see DESIGN.md Sec 2.6 log) -- the
        /// aircraft settles into a stable equilibrium right at the edge of the fade band, where
        /// residual authority is only ~10-15%, and stays there for a very long stretch before
        /// gravity/speed dynamics eventually let it complete. A small floor is not a change to
        /// the LIFT curve (that still fully collapses at postStallAoADeg, unchanged) -- it only
        /// changes how much the ELEVATOR keeps working deep into a hard pull, which is its own
        /// layer per DESIGN.md Sec 1.1 and does not touch liftCoefficient, dragCoefficient,
        /// safeAoADeg or criticalAoADeg, so the verified 300/100/3,000 headline numbers (which
        /// never call this function) are unaffected.
        /// </summary>
        public static float AoALimiter(float aoaDeg, float safeAoA, float criticalAoA, float floor = 0f)
        {
            float a = Mathf.Abs(aoaDeg);
            if (a <= safeAoA) return 1f;
            float fade = 1f - Mathf.Clamp01(Mathf.InverseLerp(safeAoA, Mathf.Max(criticalAoA, safeAoA + 0.1f), a));
            return Mathf.Max(fade, floor);
        }

        // ------------------------------------------------------------------ forces

        /// <summary>
        /// The four aerodynamic forces, in WORLD space, ready to hand to Rigidbody.AddForce.
        /// Gravity is left to Unity. Thrust is separate (see ThrustForce).
        /// </summary>
        public static Vector3 AerodynamicForce(AircraftConfig cfg, Transform t, Vector3 worldVelocity,
                                               float density, bool airbrakes)
        {
            float speed = worldVelocity.magnitude;
            if (speed < 0.1f) return Vector3.zero;

            Vector3 localVel = t.InverseTransformDirection(worldVelocity);
            float aoa = AngleOfAttack(localVel);
            float q = density * speed * speed;               // dynamic-pressure stand-in

            // Lift acts along the aircraft's up axis. Because that axis tilts back as AoA
            // rises, lift picks up a rearward component -- that IS our induced drag, and it
            // is why a hard turn bleeds energy. No separate induced-drag term exists.
            Vector3 lift = t.up * (q * cfg.liftCoefficient * LiftCurve(cfg, aoa));

            float dragK = cfg.dragCoefficient * (airbrakes ? cfg.airbrakeDragMultiplier : 1f);
            Vector3 drag = -(worldVelocity / speed) * (q * dragK);

            // Side drag scales with speed as well as slip, so it stays strong at combat speeds.
            Vector3 sideDrag = -t.right * (density * SideSlip(localVel) * speed * cfg.sideDragCoefficient);

            return lift + drag + sideDrag;
        }

        public static Vector3 ThrustForce(AircraftConfig cfg, Transform t, float throttle, float density)
            => t.forward * (Mathf.Max(0f, throttle) * cfg.maxThrust * density);

        // ------------------------------------------------------------------ authority

        /// <summary>Control effectiveness 0..1 from airspeed, floored so slow flight is never hopeless.</summary>
        public static float SpeedFactor(AircraftConfig cfg, float airspeed)
        {
            float f = Mathf.InverseLerp(cfg.minimumControlSpeed, cfg.fullControlSpeed, airspeed);
            return Mathf.Max(f, cfg.minimumArcadeAuthority);
        }

        // ------------------------------------------------------------------ analytic predictions
        // These exist so tests (and the HUD debug overlay) can state the headline numbers
        // without flying the aircraft. They solve the same equations the force code applies.

        /// <summary>
        /// Speed below which lift can no longer hold the aircraft up in level flight, at the
        /// given AoA. Pass criticalAoADeg for the absolute stall, safeAoADeg for the speed at
        /// which stall protection starts letting the aircraft sink.
        /// </summary>
        public static float StallSpeedMps(AircraftConfig cfg, float density, float aoaDeg)
        {
            float cl = LiftCurve(cfg, aoaDeg) * Mathf.Cos(aoaDeg * Mathf.Deg2Rad);
            float denom = density * cfg.liftCoefficient * cl;
            if (denom <= 0.0001f) return float.PositiveInfinity;
            return Mathf.Sqrt(cfg.massKg * Gravity / denom);
        }

        /// <summary>
        /// Is level flight possible at all in this air, without exceeding aoaMaxDeg?
        /// Sweeps AoA and asks whether thrust can cover drag at the speed that AoA needs to
        /// hold the aircraft up. Induced drag (weight * tan(AoA)) does NOT fall off with
        /// density but thrust does, so high enough up the answer becomes no -- that is the
        /// ceiling, and it is thrust-limited rather than lift-limited, same as a real jet.
        /// </summary>
        public static bool CanHoldLevelFlight(AircraftConfig cfg, float density, float throttle,
                                              float aoaMaxDeg, out float speedMps)
        {
            float thrust = cfg.maxThrust * throttle * density;
            float weight = cfg.massKg * Gravity;

            for (float a = 0.1f; a <= aoaMaxDeg; a += 0.1f)
            {
                float cl = LiftCurve(cfg, a) * Mathf.Cos(a * Mathf.Deg2Rad);
                if (cl <= 0.0001f) continue;

                float vSq = weight / (density * cfg.liftCoefficient * cl);
                float drag = density * cfg.dragCoefficient * vSq + weight * Mathf.Tan(a * Mathf.Deg2Rad);
                if (thrust >= drag) { speedMps = Mathf.Sqrt(vSq); return true; }
            }
            speedMps = 0f;
            return false;
        }

        /// <summary>
        /// Trimmed level-flight top speed, by bisection on "does thrust still beat drag".
        /// Returns 0 where level flight is impossible (above the ceiling) rather than
        /// returning a nonsense number.
        /// </summary>
        public static float TopSpeedMps(AircraftConfig cfg, float density, float throttle)
        {
            if (!CanHoldLevelFlight(cfg, density, throttle, cfg.safeAoADeg, out _)) return 0f;

            float thrust = cfg.maxThrust * throttle * density;
            float weight = cfg.massKg * Gravity;
            float lo = 5f, hi = 400f;

            for (int i = 0; i < 50; i++)
            {
                float v = (lo + hi) * 0.5f;
                float clNeeded = weight / Mathf.Max(density * cfg.liftCoefficient * v * v, 0.0001f);
                float aoa = Mathf.Min(clNeeded, 2f) * cfg.safeAoADeg;
                float drag = density * cfg.dragCoefficient * v * v + weight * Mathf.Tan(aoa * Mathf.Deg2Rad);
                if (drag < thrust) lo = v; else hi = v;
            }
            return lo;
        }

        /// <summary>
        /// Highest altitude (metres) at which level flight is still sustainable.
        /// Runs a few hundred thousand float ops -- fine in a test or an editor tool,
        /// NOT something to call every frame.
        /// </summary>
        public static float CeilingMeters(AircraftConfig cfg, float aoaMaxDeg, float throttle = 1f)
        {
            float best = 0f;
            for (float h = 0f; h <= 8000f; h += 10f)
            {
                if (!CanHoldLevelFlight(cfg, DensityAt(cfg, h), throttle, aoaMaxDeg, out _)) break;
                best = h;
            }
            return best;
        }
    }
}
