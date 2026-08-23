using UnityEngine;

namespace AstroAces.Flight
{
    /// <summary>
    /// Every tuning number for one aircraft, in one place.
    ///
    /// THE DEFAULTS BELOW ARE DERIVED, NOT GUESSED. See DESIGN.md "Flight Model Derivation".
    /// The headline targets (300 mph top speed, ~100 mph stall, ~3,000 ft ceiling) are
    /// EMERGENT from these coefficients -- nothing in the codebase clamps speed or altitude.
    /// Changing liftCoefficient, dragCoefficient, maxThrust, massKg or densityScaleHeight
    /// moves those headline numbers. Edit Mode tests assert them, so a careless tweak
    /// fails the test rather than silently changing the game.
    ///
    /// Create the asset via: Assets > Create > Astro Aces > Aircraft Config.
    /// A freshly created asset is already correctly tuned -- do not hand-enter values.
    /// </summary>
    [CreateAssetMenu(fileName = "AircraftConfig", menuName = "Astro Aces/Aircraft Config")]
    public class AircraftConfig : ScriptableObject
    {
        // ---------------------------------------------------------------- mass & engine
        [Header("Mass & Engine")]
        [Tooltip("kg. Also set this on the Rigidbody.")]
        public float massKg = 1000f;

        [Tooltip("Newtons at 100% throttle at sea-level density. Thrust scales with air " +
                 "density, which is what stops the aircraft climbing forever.")]
        public float maxThrust = 15000f;

        [Tooltip("Throttle the aircraft spawns at (0..1 of the 0-110% range expressed as 0..1.1).")]
        public float startThrottle = 0.80f;

        [Tooltip("Upper throttle limit. 1.10 = 110% overdrive, worth about +15 mph.")]
        public float maxThrottle = 1.10f;

        [Tooltip("Throttle units per second held on W/S. 0.6 = 0 to 100% in ~1.7s.")]
        public float throttleRatePerSecond = 0.6f;

        [Tooltip("Throttle change per mouse-wheel notch.")]
        public float throttleStepPerNotch = 0.05f;

        [Tooltip("Throttle change per S-key tick, for the fine ~1% trim the design calls for.")]
        public float throttleFineStep = 0.01f;

        // ---------------------------------------------------------------- atmosphere
        [Header("Atmosphere")]
        [Tooltip("Metres of altitude per e-fold of air density. 780 puts the sustainable " +
                 "ceiling at 2,986 ft. LOWER = lower ceiling. This is the ONLY knob that " +
                 "sets the ceiling; nothing clamps altitude.")]
        public float densityScaleHeight = 780f;

        [Tooltip("Density never drops below this, so controls never go completely dead.")]
        public float minDensity = 0.02f;

        // ---------------------------------------------------------------- aerodynamics
        [Header("Aerodynamics")]
        [Tooltip("Lift = density * liftCoefficient * speed^2 * Cl(AoA). Drives stall speed " +
                 "AND ceiling together -- raising it lowers stall and raises the ceiling.")]
        public float liftCoefficient = 3.7f;

        [Tooltip("Drag = density * dragCoefficient * speed^2. With maxThrust 15000 this " +
                 "puts top speed at 300 mph. There is NO speed clamp anywhere.")]
        public float dragCoefficient = 0.813f;

        [Tooltip("Sideways resistance: density * sideSlip * speed * this. THE anti-spaceship " +
                 "knob. Much stronger than forward drag by design. Raise if turns feel floaty.")]
        public float sideDragCoefficient = 20f;

        [Tooltip("Drag multiplier while airbrakes are deployed. 2.2 roughly halves the time " +
                 "to bleed from top speed to stall speed. Not an instant brake.")]
        public float airbrakeDragMultiplier = 2.2f;

        // NOTE: there is deliberately no inducedDragCoefficient. Lift acts along the
        // aircraft's up axis, so at high AoA it tilts backwards and produces induced drag
        // for free. That is why hard turns bleed energy. See DESIGN.md.

        // ---------------------------------------------------------------- angle of attack
        [Header("Angle of Attack")]
        [Tooltip("Degrees. Below this the aircraft is fully controllable. Cl = 1.0 here.")]
        public float safeAoADeg = 14f;

        [Tooltip("Degrees. Peak lift. Elevator authority has faded to zero by this point, " +
                 "so the player cannot pull into a stall -- but CAN still fall out of the " +
                 "sky by flying too slowly, which is the intended failure mode.")]
        public float criticalAoADeg = 20f;

        [Tooltip("Degrees. Lift has fully collapsed to postStallLift by this AoA.")]
        public float postStallAoADeg = 45f;

        [Tooltip("Residual Cl once fully stalled. Keeps the fall a mush, not a brick drop.")]
        public float postStallLift = 0.35f;

        [Tooltip("Elevator authority never fades all the way to 0 -- floored here instead. " +
                 "Without a floor, a full-throttle full-pull loop takes ~28s (simulated) " +
                 "because the aircraft settles at the edge of the AoA fade band with only " +
                 "~10-15% authority and crawls there for a long stretch. Does NOT touch the " +
                 "lift curve (still fully collapses at postStallAoADeg) or any headline number " +
                 "-- this is purely how much elevator survives a hard pull. Raise for an " +
                 "easier/faster loop, lower toward 0 to restore the original slow crawl.")]
        [Range(0f, 1f)] public float elevatorStallFloor = 0.3f;

        // ---------------------------------------------------------------- mouse aim
        [Header("Mouse Aim")]
        [Tooltip("Degrees of aim movement per pixel of mouse delta. Input System delta is " +
                 "already per-frame -- do NOT multiply by Time.deltaTime.")]
        public float mouseSensitivity = 0.15f;

        [Tooltip("Degrees. Hard limit on aim pitch, kept under 90 to avoid gimbal flip.")]
        public float maxAimPitch = 80f;

        [Tooltip("Degrees. Aim direction is clamped to this cone around the nose, so a fast " +
                 "flick cannot leave the aim behind the aircraft. War Thunder does the same.")]
        public float maxAimConeAngle = 55f;

        // ---------------------------------------------------------------- control law
        [Header("Control Law - Aim to Command")]
        [Tooltip("Degrees of bank commanded per degree of horizontal aim error.")]
        public float bankGain = 2.5f;

        [Tooltip("Degrees. Clamp on commanded bank.")]
        public float maxBankAngle = 80f;

        [Tooltip("Pitch demand per degree of vertical aim error.")]
        public float pitchGain = 1.0f;

        [Header("Control Law - PD Gains")]
        public float rollKp = 0.025f;   // 40 deg of roll error saturates the aileron
        public float rollKd = 0.004f;   // 200 deg/s of roll rate opposes with 0.8
        public float pitchKp = 0.050f;  // 20 deg of pitch error saturates the elevator
        public float pitchKd = 0.008f;
        public float yawKp = 0.010f;    // deliberately weak: turns are flown with bank
        public float yawKd = 0.010f;

        [Tooltip("Rudder per m/s of sideslip. Keeps turns coordinated automatically.")]
        public float coordinationStrength = 0.10f;

        [Tooltip("Extra pitch demand (degrees) per degree of bank, to hold the nose up in a " +
                 "turn. 0.12 recovers most but not all of the lost altitude -- deliberately " +
                 "not a full altitude hold.")]
        public float turnCompensationStrength = 0.12f;

        [Tooltip("How hard pitch is held back until the wings have rolled to the commanded " +
                 "bank. 1 = pull immediately (feels twitchy), 0.35 = bank first, then pull.")]
        [Range(0f, 1f)] public float rollAlignmentFloor = 0.35f;

        // ---------------------------------------------------------------- rotation rates
        [Header("Rotation - Rates & Authority")]
        [Tooltip("Degrees/second. Steady-state roll rate at full aileron.")]
        public float maxRollRate = 200f;
        [Tooltip("Degrees/second^2 cap on roll acceleration.")]
        public float rollAuthority = 1400f;
        [Tooltip("1/second. How fast roll rate converges on its target. Raise to kill wobble.")]
        public float rollDamping = 6f;

        public float maxPitchRate = 60f;
        public float pitchAuthority = 400f;
        public float pitchDamping = 5f;

        public float maxYawRate = 25f;
        public float yawAuthority = 150f;
        public float yawDamping = 4f;

        // ---------------------------------------------------------------- speed authority
        [Header("Speed-Based Control Authority")]
        [Tooltip("m/s. Below this, control authority is at the arcade floor.")]
        public float minimumControlSpeed = 20f;

        [Tooltip("m/s. At and above this, full control authority.")]
        public float fullControlSpeed = 70f;

        [Tooltip("Authority never falls below this, so a slow aircraft is still flyable. " +
                 "Raise if low-speed recovery feels hopeless.")]
        [Range(0.05f, 1f)] public float minimumArcadeAuthority = 0.35f;

        // ---------------------------------------------------------------- combat
        [Header("Combat")]
        public float maxHealth = 30f;
        [Tooltip("5 damage x 6 hits = 30 HP kill, per the design brief.")]
        public float damagePerHit = 5f;
        [Tooltip("Rounds per second, per aircraft. 20 = one round every 0.05s.")]
        public float fireRate = 20f;
        [Tooltip("m/s of muzzle velocity, ADDED to the aircraft's own velocity so the " +
                 "aircraft never flies into its own rounds.")]
        public float muzzleSpeed = 500f;
        [Tooltip("Seconds before a round despawns. 2s x ~630 m/s = ~1,260 m of reach.")]
        public float projectileLifetime = 2f;
        [Tooltip("Metres. Where the gunnery crosshair is projected.")]
        public float crosshairDistance = 500f;

        /// <summary>Convenience: metres/second at which lift can no longer hold level flight
        /// at sea level, using peak (critical-AoA) lift. Informational; see Aero for the math.</summary>
        public float ApproxStallSpeedMps => Aero.StallSpeedMps(this, 1f, criticalAoADeg);
    }
}
