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
    /// Exercises mouse aim tracking under extreme/adversarial input, using
    /// InputTestFixture's isolated virtual devices (same technique as
    /// Phase3ControlLawPlayModeTests -- see that file's header for why this is trustworthy
    /// and a raw device-poke from execute_code isn't). Real telemetry comes for free: the
    /// test rig's existing LoopDiagnostic + BeanTracker + BeanLogger (already wired in
    /// Dogfight.unity, CSV output) captures mouseDeltaX/Y, offNoseDeg, DesiredDirection and
    /// the aircraft's own forward vector on every FixedUpdate during the whole run -- read
    /// the resulting CSV under UTI/BeanLogs/ after this test for the full trace, not just
    /// the pass/fail below.
    ///
    /// Built to verify two things at once, requested together 2026-08-22: (1) that mouse
    /// aim tracking itself behaves sanely under extreme input (large single deltas, rapid
    /// direction reversals, the 55 deg cone clamp), and (2) AA-008's second fix -- gluing
    /// the aim to the nose while E is held should NOT swallow real concurrent mouse
    /// movement, which the first version of that fix did (reported immediately by the user
    /// as "E auto-recentres the crosshair," not intended).
    /// </summary>
    public class AimTrackingPlayModeTests : InputTestFixture
    {
        const string SceneName = "Dogfight";
        const string RigName = "PlayerAircraft (Phase1 Test Rig)";

        Keyboard keyboard;
        Mouse mouse;
        GameObject rig;
        AircraftState state;
        AircraftInput input;
        AircraftAimController aim;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        IEnumerator LoadRig()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            rig = GameObject.Find(RigName);
            Assert.IsNotNull(rig, $"'{RigName}' not found in the {SceneName} scene -- did the scene layout change?");

            state = rig.GetComponent<AircraftState>();
            input = rig.GetComponent<AircraftInput>();
            aim = rig.GetComponent<AircraftAimController>();
            Assert.IsNotNull(state);
            Assert.IsNotNull(input);
            Assert.IsNotNull(aim);
        }

        [UnityTest]
        public IEnumerator AimTracking_ExtremeInput_AndE_Hold_MouseInterrupt()
        {
            yield return LoadRig();

            // Let spawn velocity settle before doing anything.
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            // --- Phase A: one huge single-frame delta -- confirm the 55 deg cone clamp ----
            Set(mouse.delta, new Vector2(3000f, 2000f));
            yield return null;
            Set(mouse.delta, Vector2.zero);
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            float offNoseAfterHugeFlick = Vector3.Angle(rig.transform.forward, aim.DesiredDirection);
            Assert.LessOrEqual(offNoseAfterHugeFlick, 55.5f,
                $"Aim cone clamp violated: {offNoseAfterHugeFlick:F1} deg off nose after a huge single-frame flick (cap is 55).");

            // --- Phase B: rapid direction reversals -- confirm no NaN/explosion, still clamped
            for (int i = 0; i < 20; i++)
            {
                Vector2 d = (i % 2 == 0) ? new Vector2(800f, -600f) : new Vector2(-900f, 700f);
                Set(mouse.delta, d);
                yield return new WaitForFixedUpdate();
            }
            Set(mouse.delta, Vector2.zero);
            yield return new WaitForFixedUpdate();

            float offNoseAfterReversals = Vector3.Angle(rig.transform.forward, aim.DesiredDirection);
            Assert.LessOrEqual(offNoseAfterReversals, 55.5f,
                $"Aim cone clamp violated after rapid reversals: {offNoseAfterReversals:F1} deg.");
            Assert.IsFalse(float.IsNaN(aim.DesiredDirection.x) || float.IsNaN(aim.DesiredDirection.y) || float.IsNaN(aim.DesiredDirection.z),
                "DesiredDirection went NaN after rapid mouse reversals.");

            // --- Phase C: hold E, NO mouse movement -- aim should glue to the nose ---------
            Press(keyboard.eKey);
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate(); // let it settle onto the nose

            float offNoseDuringHandsOffPitch = Vector3.Angle(rig.transform.forward, aim.DesiredDirection);
            for (int i = 0; i < 100; i++) // ~2s at 0.02 fixedDeltaTime
            {
                yield return new WaitForFixedUpdate();
                float off = Vector3.Angle(rig.transform.forward, aim.DesiredDirection);
                offNoseDuringHandsOffPitch = Mathf.Max(offNoseDuringHandsOffPitch, off);
            }
            Assert.Less(offNoseDuringHandsOffPitch, 2f,
                $"AA-008 regression: aim drifted {offNoseDuringHandsOffPitch:F1} deg off nose during a hands-off " +
                "E-hold -- should stay glued to the nose when the mouse isn't moving.");

            // --- Phase D: keep E held, but NOW also move the mouse -- mouse should win -----
            Vector3 desiredBeforeMouseNudge = aim.DesiredDirection;
            Set(mouse.delta, new Vector2(400f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Set(mouse.delta, Vector2.zero);
            yield return new WaitForFixedUpdate();

            float aimChangeFromMouseWhileEHeld = Vector3.Angle(desiredBeforeMouseNudge, aim.DesiredDirection);
            Release(keyboard.eKey);

            Assert.Greater(aimChangeFromMouseWhileEHeld, 1f,
                "AA-008 second-fix regression: moving the mouse while E was held had no effect on " +
                "DesiredDirection -- E is swallowing real mouse input again (\"auto-recentring\").");

            yield return new WaitForFixedUpdate();
        }

        [UnityTest]
        public IEnumerator FreeLook_MouseMovement_DoesNotChangeFlightDirection()
        {
            yield return LoadRig();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Vector3 desiredBeforeFreeLook = aim.DesiredDirection;

            // DESIGN.md Sec 5: "Flight direction is untouched" while free-look (right mouse)
            // is held. Reported by the user 2026-08-22: it "kinda works" but the ship still
            // turns while looking around -- AircraftAimController had no gate on
            // FreeLookHeld at all, so the same mouse delta ChaseCamera used for the free-look
            // orbit was ALSO being fed straight into StepAim the whole time.
            Press(mouse.rightButton);
            for (int i = 0; i < 20; i++)
            {
                Set(mouse.delta, new Vector2(300f, -150f));
                yield return new WaitForFixedUpdate();
            }
            Set(mouse.delta, Vector2.zero);
            Release(mouse.rightButton);
            yield return new WaitForFixedUpdate();

            float aimChangeFromFreeLook = Vector3.Angle(desiredBeforeFreeLook, aim.DesiredDirection);
            Assert.Less(aimChangeFromFreeLook, 0.5f,
                $"Free-look regression: DesiredDirection moved {aimChangeFromFreeLook:F1} deg while only " +
                "right-click + mouse (free-look) was held -- flight direction should be untouched (DESIGN.md Sec 5).");

            // Confirm normal mouse-driven aim still works once free-look is released --
            // this isn't just "nothing moves the aim ever," the gate should be specific to
            // FreeLookHeld.
            Set(mouse.delta, new Vector2(300f, 0f));
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();
            Set(mouse.delta, Vector2.zero);

            float aimChangeAfterRelease = Vector3.Angle(desiredBeforeFreeLook, aim.DesiredDirection);
            Assert.Greater(aimChangeAfterRelease, 1f,
                "Normal mouse-driven aim should still work after releasing free-look.");
        }
    }
}
