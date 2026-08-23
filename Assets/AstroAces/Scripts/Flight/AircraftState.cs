using UnityEngine;

namespace AstroAces.Flight
{
    /// <summary>
    /// Per-frame flight state, computed once and read by everything else. Nothing outside
    /// this class should call InverseTransformDirection on velocity or angular velocity --
    /// do it here, once, so there is exactly one place that could get the sign wrong.
    ///
    /// WHY EXPLICIT Refresh() AND NOT ITS OWN FixedUpdate: Unity does not guarantee
    /// MonoBehaviour callback order across components on the same GameObject unless you
    /// configure Script Execution Order by hand, which is an easy thing to forget and a
    /// nasty thing to debug ("state is one frame stale" bugs). Instead, whoever needs fresh
    /// state calls Refresh() explicitly as the first line of its own FixedUpdate.
    /// AircraftPhysics owns that call; nothing else should call Refresh().
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AircraftState : MonoBehaviour
    {
        [SerializeField] AircraftConfig cfg;

        public Rigidbody Body { get; private set; }
        public Vector3 LocalVelocity { get; private set; }
        public float AirspeedMps { get; private set; }
        public float AltitudeMeters { get; private set; }   // transform.position.y; ground plane is y = 0
        public float Density { get; private set; }
        public float AngleOfAttack { get; private set; }    // degrees, nose-up positive
        public float SideSlip { get; private set; }         // m/s, positive = drifting right
        public float BankAngle { get; private set; }        // degrees, positive = right wing down
        public BodyRates Rates { get; private set; }
        public bool IsAlive { get; set; } = true;

        void Awake()
        {
            Body = GetComponent<Rigidbody>();
        }

        /// <summary>Recompute every derived value from the Rigidbody's current physics state.</summary>
        public void Refresh()
        {
            LocalVelocity = transform.InverseTransformDirection(Body.linearVelocity);
            AirspeedMps = Body.linearVelocity.magnitude;
            AltitudeMeters = transform.position.y;
            Density = Aero.DensityAt(cfg, AltitudeMeters);
            AngleOfAttack = Aero.AngleOfAttack(LocalVelocity);
            SideSlip = Aero.SideSlip(LocalVelocity);
            BankAngle = FlightControlLaw.BankAngle(transform, BankAngle);   // hold through the singularity

            Vector3 localAngularVelocity = transform.InverseTransformDirection(Body.angularVelocity);
            Rates = BodyRates.FromUnity(localAngularVelocity);
        }
    }
}
