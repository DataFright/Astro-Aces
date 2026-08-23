using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AstroAces.Flight;

namespace AstroAces.Tests.PlayMode
{
    /// <summary>
    /// Holds E through a full hands-off loop (no mouse input at all) and checks the
    /// crosshair's angle off the nose every tick, specifically through the loop's
    /// inverted/vertical portion -- built to test whether AA-008's glue
    /// (SetDesiredDirection(transform.forward) every frame with no mouse delta) breaks down
    /// there, since it recomputes aimYaw/aimPitch via Atan2/Asin from the direction every
    /// call, exactly the kind of decomposition that goes singular near straight up/down (the
    /// same class of problem FlightControlLaw.BankAngle already has to guard against
    /// explicitly).
    ///
    /// Result (2026-08-22): it does NOT break down -- max off-nose across a full 16s hands-off
    /// loop was 1.3 deg, and exactly 0.0 deg at the moment pitch crossed 90 deg (dead
    /// vertical). The user's continued "crosshair looks stuck/disorienting when upside-down"
    /// report is a separate, real thing, but it's the Phase 2 placeholder camera (rigidly
    /// parented, rolls exactly with the aircraft, so the whole screen flips during that part
    /// of the loop) -- not this aim-tracking code. Deferred to Phase 4 (ChaseCamera). Kept as
    /// a real regression test rather than deleted, since it directly guards the thing it
    /// found clean.
    /// </summary>
    public class FullLoopAimPlayModeTests : InputTestFixture
    {
        const string SceneName = "Dogfight";
        const string RigName = "PlayerAircraft (Phase1 Test Rig)";

        Keyboard keyboard;
        Mouse mouse;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        [UnityTest]
        public IEnumerator FullLoop_HandsOffE_AimStaysGluedThroughInvertedPortion()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            GameObject rig = GameObject.Find(RigName);
            Assert.IsNotNull(rig);
            Transform t = rig.transform;
            AircraftAimController aim = rig.GetComponent<AircraftAimController>();

            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Press(keyboard.eKey);

            float maxOffNose = 0f;
            float worstTickTime = -1f;
            // 16 real seconds of held E, comfortably past the ~8-13s expected loop time.
            for (int i = 0; i < 800; i++)
            {
                yield return new WaitForFixedUpdate();
                float off = Vector3.Angle(t.forward, aim.DesiredDirection);
                if (off > maxOffNose)
                {
                    maxOffNose = off;
                    worstTickTime = i * Time.fixedDeltaTime;
                }
            }

            Release(keyboard.eKey);
            yield return new WaitForFixedUpdate();

            Debug.Log($"[FullLoopAimTest] max off-nose during hands-off E hold: {maxOffNose:F2} deg at t={worstTickTime:F2}s");
            Assert.Less(maxOffNose, 5f,
                $"AA-008 regression: crosshair drifted {maxOffNose:F1} deg off nose (at t={worstTickTime:F2}s) " +
                "during a hands-off full loop -- should stay glued to the nose the whole way through, " +
                "including the vertical/inverted portion.");
        }
    }
}
