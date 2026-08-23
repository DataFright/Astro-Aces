using System;
using UnityEngine;

namespace AstroAces.Flight
{
    /// <summary>
    /// Throttle and airbrake state. Owns no forces -- AircraftPhysics reads Throttle and
    /// AirbrakesOn and hands them to Aero. Keeping this separate means the HUD, the AI and
    /// keyboard input can all drive the same two numbers without touching physics code.
    ///
    /// Drives itself from an AircraftInput sibling if one exists (Update() below) -- same
    /// null-is-valid pattern as AircraftAimController, so an AI-piloted aircraft with no
    /// AircraftInput just never has its throttle touched by this class, and whatever drives
    /// it (EnemyPilot, Phase 8) calls the ApplyThrottle* methods directly instead.
    /// </summary>
    public class AircraftEngine : MonoBehaviour
    {
        [SerializeField] AircraftConfig cfg;

        AircraftInput input;

        /// <summary>0 .. cfg.maxThrottle (1.10 = 110% overdrive).</summary>
        public float Throttle { get; private set; }
        public bool AirbrakesOn { get; private set; }

        /// <summary>Fired whenever airbrakes flip, for the HUD message ("AIRBRAKES UP/DOWN").</summary>
        public event Action<bool> OnAirbrakeChanged;

        void Awake()
        {
            Throttle = cfg.startThrottle;
            input = GetComponent<AircraftInput>();
        }

        void Update()
        {
            if (input == null) return;

            if (input.ThrottleUpHeld) ApplyThrottleAxis(1f, Time.deltaTime);
            if (input.ThrottleDownPressed) ApplyThrottleFineStep(-1f);   // DESIGN.md §2.11: S is a discrete trim step, not a held rate
            if (input.ScrollNotches != 0f) ApplyThrottleNotch(input.ScrollNotches);
            if (input.AirbrakeToggled) ToggleAirbrakes();
        }

        /// <summary>+1 = W held, -1 = S held, 0 = neither. Called every frame regardless of dt source.</summary>
        public void ApplyThrottleAxis(float axis, float dt)
        {
            if (axis == 0f) return;
            Throttle = Mathf.Clamp(Throttle + axis * cfg.throttleRatePerSecond * dt, 0f, cfg.maxThrottle);
        }

        /// <summary>One mouse-wheel notch. Positive = up.</summary>
        public void ApplyThrottleNotch(float notches)
        {
            if (notches == 0f) return;
            Throttle = Mathf.Clamp(Throttle + notches * cfg.throttleStepPerNotch, 0f, cfg.maxThrottle);
        }

        /// <summary>The small ~1% trim step called out in the design brief, distinct from the W/S rate.</summary>
        public void ApplyThrottleFineStep(float direction)
        {
            if (direction == 0f) return;
            Throttle = Mathf.Clamp(Throttle + Mathf.Sign(direction) * cfg.throttleFineStep, 0f, cfg.maxThrottle);
        }

        public void ToggleAirbrakes()
        {
            AirbrakesOn = !AirbrakesOn;
            OnAirbrakeChanged?.Invoke(AirbrakesOn);
        }
    }
}
