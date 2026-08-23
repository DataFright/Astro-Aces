using UnityEngine;

namespace AstroAces.Flight
{
    /// <summary>
    /// Applies forces AND rotation to the Rigidbody. This is PHYSICS, the third of
    /// DESIGN.md §1.1's three layers -- it reads AircraftAimController's DesiredDirection
    /// (intent) and FlightControlLaw's output (controller), but owns none of that logic
    /// itself. Force order: thrust, lift/drag/side-drag, then rotation -- rotation always
    /// last, since it depends on this frame's freshly-applied AoA/bank state.
    ///
    /// Configures its own Rigidbody in Awake rather than trusting Inspector values, because
    /// a stray non-zero Linear Damping is exactly the kind of thing that quietly invalidates
    /// the whole derived flight model (see BUGS.md known risks).
    ///
    /// SPAWNS WITH VELOCITY ALREADY SET (see BUGS.md AA-002). Lift needs speed^2 to mean
    /// anything; gravity needs none. An aircraft that spawns at rest free-falls for the
    /// better part of a second before it has enough airspeed for lift to matter, and by
    /// then the angle between its (fixed, unrotated) nose and its downward-bending flight
    /// path has already blown past stall -- confirmed by simulation, not assumed. This
    /// isn't a Phase-1-only workaround: no aircraft, player or enemy, should ever spawn
    /// motionless in mid-air, so every future spawn point (Phase 9 world, Phase 8 AI) must
    /// set an initial velocity too, not just this test rig.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(AircraftState))]
    [RequireComponent(typeof(AircraftEngine))]
    [RequireComponent(typeof(AircraftAimController))]
    public class AircraftPhysics : MonoBehaviour
    {
        [SerializeField] AircraftConfig cfg;

        Rigidbody rb;
        AircraftState state;
        AircraftEngine engine;
        AircraftAimController aim;
        AircraftInput input;   // null on an AI-piloted aircraft -- see the RollAxis/PitchAxis guard below

        /// <summary>The exact command applied last FixedUpdate, after all overrides -- for
        /// diagnostics (LoopDiagnostic/UTI) only. Nothing gameplay-relevant reads this.</summary>
        public ControlCommand LastCommand { get; private set; }

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            state = GetComponent<AircraftState>();
            engine = GetComponent<AircraftEngine>();
            aim = GetComponent<AircraftAimController>();
            input = GetComponent<AircraftInput>();

            rb.mass = cfg.massKg;
            rb.useGravity = true;
            rb.linearDamping = 0f;
            rb.angularDamping = 0f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // cfg.startThrottle, not engine.Throttle: MonoBehaviour.Awake() order between
            // components on one GameObject is unspecified, and AircraftEngine.Awake() is
            // what sets Throttle from cfg.startThrottle in the first place. Reading through
            // engine here could race and spawn at throttle 0 depending on component order.
            float spawnDensity = Aero.DensityAt(cfg, transform.position.y);
            float spawnSpeed = Aero.TopSpeedMps(cfg, spawnDensity, cfg.startThrottle);
            rb.linearVelocity = transform.forward * spawnSpeed;
        }

        void FixedUpdate()
        {
            state.Refresh();   // must run before anything below reads state.*

            rb.AddForce(Aero.ThrustForce(cfg, transform, engine.Throttle, state.Density));
            rb.AddForce(Aero.AerodynamicForce(cfg, transform, rb.linearVelocity, state.Density, engine.AirbrakesOn));

            if (!state.IsAlive) return;   // wreckage tumbles on real physics -- no rate control fighting it

            Vector3 localTarget = transform.InverseTransformDirection(aim.DesiredDirection);
            ControlCommand cmd = FlightControlLaw.Compute(localTarget, state.BankAngle, state.Rates,
                                                           state.AngleOfAttack, state.SideSlip, cfg);

            // Manual keyboard roll/pitch OVERRIDES the computed command on that axis rather
            // than adding to it (see BUGS.md AA-004). Adding used to fail silently: the
            // mouse-aim's own restoring command saturates to +-1 as bank/pitch error grows
            // away from a held aim direction, and once BOTH terms are pinned at opposite
            // +-1, "add then clamp" nets to zero -- the aircraft would hit a wall mid-roll
            // and mid-loop instead of completing the manoeuvre. Full override has no such
            // failure mode: holding D always means aileron = 1, full stop.
            //
            // Pitch still routes through the AoA limiter even under manual control -- stall
            // protection is a blanket promise (DESIGN.md SS2.6: "cannot stall by pulling"),
            // not a mouse-only one. Roll has no analogous limiter, so it's a clean override.
            //
            // AA-005: while manually pitching (a loop), suppress the mouse-driven aileron
            // instead of leaving it live. The mouse aim is capped at +-maxAimPitch (80 deg)
            // and never rotates with the aircraft, so once a manual pitch carries the nose
            // past it, the LOCAL target direction ends up behind the aircraft. horizontalError
            // is deliberately floored there (see FlightControlLaw's comment) so it saturates
            // toward +-90 deg instead of flipping sign -- correct for "bank hard to chase a
            // target that's now behind me," wrong here, because the player never asked to
            // turn: it is a geometry artifact of the aim cone, not intent. Left live, that
            // saturated bank silently steals part of the rotation a manual loop needs,
            // which is exactly "climbs to vertical, then visibly veers instead of continuing
            // over the top." Manual roll has no equivalent failure (its cross-axis pitch
            // interference is damped by rollAlignmentFloor, not saturated), so only pitch
            // suppresses the other axis here.
            if (input != null)
            {
                if (input.RollAxis != 0f)
                {
                    cmd.aileron = input.RollAxis;
                }
                else if (input.PitchAxis != 0f)
                {
                    cmd.aileron = 0f;
                }

                if (input.PitchAxis != 0f)
                {
                    cmd.elevator = FlightControlLaw.ApplyAoALimiter(input.PitchAxis, state.AngleOfAttack, cmd.aoaLimitFactor);

                    // AA-008: the mouse aim deliberately never auto-centres (DESIGN.md Sec 2.6
                    // -- "stop moving the mouse and the aircraft keeps turning until it gets
                    // there"), so it stays fixed in WORLD space while the player's hands are on
                    // the keyboard, not the mouse. During a sustained manual pitch with NO mouse
                    // movement (a hands-off loop), that fixed point gets left behind as the
                    // aircraft rotates past it and eventually ends up behind the aircraft --
                    // invisible to any camera looking forward, and a stale bank/pitch target the
                    // instant the player releases E. Glue the aim to the current nose ONLY on a
                    // frame with no real mouse delta -- checked and gated the first version of
                    // this fix, which glued unconditionally every frame regardless of mouse
                    // input and made E feel like it was hijacking/recentring the crosshair even
                    // while the player was actively aiming with the mouse (reported immediately
                    // by real testing, not something the original fix considered). A frame with
                    // genuine mouse movement always wins.
                    if (input.MouseDelta.sqrMagnitude < 0.0001f)
                        aim.SetDesiredDirection(transform.forward);
                }
            }

            LastCommand = cmd;

            float speedFactor = Aero.SpeedFactor(cfg, state.AirspeedMps);
            BodyRates nextRates = FlightControlLaw.StepRates(state.Rates, cmd, speedFactor, Time.fixedDeltaTime, cfg);
            rb.angularVelocity = transform.TransformDirection(nextRates.ToUnity());
        }
    }
}
