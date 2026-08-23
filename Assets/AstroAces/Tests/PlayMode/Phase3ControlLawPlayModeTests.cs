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
    /// Solo-runnable Play Mode regression guards for AA-004 and AA-007, driven through
    /// InputTestFixture's isolated virtual Keyboard/Mouse -- Setup() fully severs the input
    /// system from real hardware for the duration of the test (see InputTestFixture's own
    /// remarks), so a held key genuinely stays held for the whole test. That is NOT true of
    /// InputSystem.QueueStateEvent against the live device outside a test: the real OS
    /// keyboard backend keeps polling and overwrites a synthetic state within a frame or two
    /// -- confirmed directly before writing this file, not assumed.
    ///
    /// AA-006 (persistent cursor drift in the Editor's Game view) is deliberately NOT covered
    /// here. It is specifically about whether CursorLockMode.Locked pins the real OS cursor
    /// inside the Editor's Game view -- a virtual test device has no OS cursor to mispin, so
    /// no automated test can exercise the actual bug. That one still needs a human running
    /// Play Mode with a real mouse.
    /// </summary>
    public class Phase3ControlLawPlayModeTests : InputTestFixture
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

        public override void TearDown()
        {
            AircraftInput.GamePaused = false;
            base.TearDown();
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
        public IEnumerator AA004_HeldRoll_StaysCommanded_NeverDecaysToZeroMidRoll()
        {
            yield return LoadRig();

            // Let the spawn velocity settle before introducing roll input.
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            Press(keyboard.dKey);
            yield return null; // let AircraftInput.Update() see the press

            // Skip StepRates' ramp-up toward the commanded rate before sampling.
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate();

            float cumulativeRollDeg = 0f;
            float minAbsRollRate = float.MaxValue;
            const float sampleSeconds = 3f;
            float elapsed = 0f;

            while (elapsed < sampleSeconds)
            {
                yield return new WaitForFixedUpdate();
                float dt = Time.fixedDeltaTime;
                float rate = state.Rates.rollRight;
                cumulativeRollDeg += Mathf.Abs(rate) * dt;
                minAbsRollRate = Mathf.Min(minAbsRollRate, Mathf.Abs(rate));
                elapsed += dt;
            }

            Release(keyboard.dKey);
            yield return null;

            // AA-004's exact failure: the roll rate decays to ~0 and rotation stops dead at a
            // predictable point instead of continuing, because the keyboard command was being
            // added to an already-saturated mouse-restoring term and cancelling out. A held D
            // that still works never lets the rate collapse, and comfortably clears a full
            // 360 deg turn well within 3 s at a 200 deg/s max roll rate.
            Assert.Greater(minAbsRollRate, 30f,
                $"AA-004 regression: roll rate dropped to {minAbsRollRate:F1} deg/s while D was " +
                "held continuously -- looks like the keyboard command is being cancelled again.");
            Assert.Greater(cumulativeRollDeg, 300f,
                $"AA-004 regression: only accumulated {cumulativeRollDeg:F0} deg of roll in " +
                $"{sampleSeconds:F0}s of held D -- expected a full rotation (~360 deg) or more.");
        }

        [UnityTest]
        public IEnumerator AA007_GamePaused_MouseMovement_DoesNotChangeAim()
        {
            yield return LoadRig();
            yield return new WaitForFixedUpdate();

            AircraftInput.GamePaused = true;
            yield return null; // let AircraftInput.Update() see GamePaused and go neutral

            Vector3 aimBefore = aim.DesiredDirection;

            // Simulate a large, sustained mouse movement while "paused".
            for (int i = 0; i < 5; i++)
            {
                Set(mouse.delta, new Vector2(400f, 250f));
                yield return null;
            }

            AircraftInput.GamePaused = false;

            Assert.AreEqual(Vector2.zero, input.MouseDelta,
                "AA-007 regression: AircraftInput.MouseDelta was non-zero while GamePaused -- " +
                "the pause gate isn't neutralising mouse reads.");
            Assert.AreEqual(aimBefore, aim.DesiredDirection,
                "AA-007 regression: DesiredDirection drifted from residual mouse movement while " +
                "GamePaused was true.");
        }
    }
}
