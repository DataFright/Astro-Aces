using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AstroAces.Flight;
using AstroAces.UI;

namespace AstroAces.Tests.PlayMode
{
    /// <summary>
    /// Phase 4 (ChaseCamera) regression coverage: free-look orbit/return and the Caps Lock
    /// zoom toggle, using InputTestFixture's isolated virtual devices (see
    /// Phase3ControlLawPlayModeTests for why this is trustworthy for pure game logic).
    /// </summary>
    public class ChaseCameraPlayModeTests : InputTestFixture
    {
        const string SceneName = "Dogfight";
        const string RigName = "PlayerAircraft (Phase1 Test Rig)";
        const string CameraName = "Main Camera";

        Keyboard keyboard;
        Mouse mouse;
        GameObject rig;
        GameObject camGO;
        ChaseCamera chaseCam;
        Camera cam;

        public override void Setup()
        {
            base.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            mouse = InputSystem.AddDevice<Mouse>();
        }

        IEnumerator LoadScene()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            rig = GameObject.Find(RigName);
            camGO = GameObject.Find(CameraName);
            Assert.IsNotNull(rig);
            Assert.IsNotNull(camGO);

            chaseCam = camGO.GetComponent<ChaseCamera>();
            cam = camGO.GetComponent<Camera>();
            Assert.IsNotNull(chaseCam, "Main Camera is missing its ChaseCamera component.");
            Assert.IsNotNull(cam);
        }

        [UnityTest]
        public IEnumerator FreeLook_OrbitsWhileHeld_ReturnsToCentreOnRelease()
        {
            yield return LoadScene();
            for (int i = 0; i < 10; i++) yield return new WaitForFixedUpdate(); // let it snap in and settle

            float rotDiffBefore = Quaternion.Angle(rig.transform.rotation, camGO.transform.rotation);

            Press(mouse.rightButton);
            for (int i = 0; i < 15; i++)
            {
                Set(mouse.delta, new Vector2(200f, 0f));
                yield return null;
            }
            Set(mouse.delta, Vector2.zero);
            yield return null;

            float rotDiffWhileOrbiting = Quaternion.Angle(rig.transform.rotation, camGO.transform.rotation);
            Assert.Greater(rotDiffWhileOrbiting, rotDiffBefore + 5f,
                "Free look didn't visibly orbit the camera away from the aircraft's own facing while held.");

            Release(mouse.rightButton);
            // Two smoothing stages stack here: the free-look offset itself decays with a
            // ~0.25s time constant (DESIGN.md Sec 5), and the camera's own rotation then
            // Slerps toward that decaying target with its own ~0.1s time constant on top.
            // 3s of real ticks is comfortably past both settling (>10 time constants).
            for (int i = 0; i < 150; i++) yield return new WaitForFixedUpdate();

            float rotDiffAfterReturn = Quaternion.Angle(rig.transform.rotation, camGO.transform.rotation);
            Assert.Less(rotDiffAfterReturn, 3f,
                $"Free look didn't return to centre after release -- still {rotDiffAfterReturn:F1} deg off " +
                "the aircraft's facing 3s after letting go (started ~120 deg off while held, so this checks " +
                "it actually decayed back down, not that it hit exactly 0 -- floating-point convergence and " +
                "the aircraft's own tiny residual drift during straight flight mean it never reaches exact 0).");
        }

        [UnityTest]
        public IEnumerator FreeLook_OrbitsAroundShip_NotJustRotatesInPlace()
        {
            yield return LoadScene();

            // Freeze the ship's own motion for this test, BEFORE the settle wait below. At
            // ~114 m/s and an ~8.5m orbit radius, the ship can move farther between
            // LateUpdate calls than the entire camera-to-ship distance whenever render-frame
            // delivery is sparse (already documented as irregular in this environment --
            // TOOLING.md), which swings desiredPosition/desiredRotation wildly frame to
            // frame and never lets the Slerp settle. That's a test-environment confound, not
            // the thing under test here (the orbit math itself) -- isolate it by holding the
            // ship still from the very start, so the pre-freelook "before" snapshot below is
            // itself a stable, settled baseline rather than a mid-catch-up snapshot.
            Rigidbody rigBody = rig.GetComponent<Rigidbody>();
            if (rigBody != null)
            {
                rigBody.linearVelocity = Vector3.zero;
                rigBody.angularVelocity = Vector3.zero;
                rigBody.isKinematic = true;
            }
            for (int i = 0; i < 10; i++) yield return null;   // render-frame cadence -- see the note below

            // AA-010: the first free-look implementation only panned the VIEW from the fixed
            // chase spot -- rotation changed, position never did. User: "it kinda works but
            // ... its behind the ship so its kinda not right." This test asserts the thing
            // that regression alone (checking only rotation, as the sibling test above does)
            // could not have caught: the camera's actual POSITION has to move.
            Vector3 positionBefore = camGO.transform.position;
            float distanceBefore = Vector3.Distance(positionBefore, rig.transform.position);

            Press(mouse.rightButton);
            for (int i = 0; i < 20; i++)
            {
                Set(mouse.delta, new Vector2(250f, 0f));   // yaw hard toward the clamp
                yield return null;
            }
            Set(mouse.delta, Vector2.zero);

            // ChaseCamera's position/rotation smoothing runs in LateUpdate (a render-frame
            // callback), not FixedUpdate. How many render frames actually happen per real
            // second is NOT something this test can safely assume in this environment --
            // confirmed directly: 50 FixedUpdate ticks, then separately 300 render frames,
            // each produced wildly different amounts of actual convergence run to run (the
            // Editor's real frame delivery while unfocused is itself irregular, the same
            // thing already documented in TOOLING.md for Play Mode transitions). The
            // underlying Lerp/Slerp math is genuinely time-based
            // (`1 - exp(-rate * Time.deltaTime)`), so given enough REAL elapsed time it will
            // converge regardless of how many or few frames deliver that time. Poll on real
            // time instead of frame count.
            float deadline = Time.realtimeSinceStartup + 8f;
            float lookAngle = 180f;
            float positionDelta = 0f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                Vector3 toShipNow = (rig.transform.position - camGO.transform.position).normalized;
                lookAngle = Vector3.Angle(camGO.transform.forward, toShipNow);
                positionDelta = Vector3.Distance(positionBefore, camGO.transform.position);
                if (lookAngle < 15f && positionDelta > 3f) break;
            }

            // The break above fires as soon as look-angle/position-delta clear their
            // thresholds, which is typically still mid-transition: Vector3.Lerp moves the
            // camera along a straight chord between its old and new points on the (roughly
            // spherical) orbit, and any chord between two points on a sphere's surface passes
            // inside the sphere -- so distance-from-ship legitimately dips below the orbit
            // radius partway through, before recovering as the Lerp finishes. That dip is
            // correct chase-cam behaviour, not a bug -- give it a bit more real time to
            // finish settling onto the orbit surface before judging the held distance.
            float settleDeadline = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < settleDeadline) yield return null;

            Vector3 positionDuring = camGO.transform.position;
            Assert.Greater(positionDelta, 3f,
                $"Free look should orbit the camera to a genuinely different position around the " +
                $"ship, not just change its facing -- moved only {positionDelta:F1}m after 8 real seconds.");
            Assert.Less(lookAngle, 15f,
                $"While orbiting, the camera should be looking back at the ship -- still " +
                $"{lookAngle:F1} deg off after 8 real seconds.");

            float distanceDuring = Vector3.Distance(positionDuring, rig.transform.position);
            Assert.Less(Mathf.Abs(distanceDuring - distanceBefore), distanceBefore * 0.3f,
                $"Orbit should hold roughly the same distance from the ship ({distanceBefore:F1}m " +
                $"before), not fly off or collapse inward -- now {distanceDuring:F1}m.");

            Release(mouse.rightButton);
        }

        [UnityTest]
        public IEnumerator Zoom_CapsLockTogglesFieldOfView()
        {
            yield return LoadScene();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            float initialFov = cam.fieldOfView;
            Assert.AreEqual(60f, initialFov, 0.1f, "Camera should start at the normal 60 deg FOV.");

            Press(keyboard.capsLockKey);
            yield return new WaitForFixedUpdate();
            Release(keyboard.capsLockKey);
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(24f, cam.fieldOfView, 0.1f, "Caps Lock should toggle FOV to the zoomed 24 deg.");

            Press(keyboard.capsLockKey);
            yield return new WaitForFixedUpdate();
            Release(keyboard.capsLockKey);
            yield return new WaitForFixedUpdate();

            Assert.AreEqual(60f, cam.fieldOfView, 0.1f, "A second Caps Lock press should toggle FOV back to 60 deg.");
        }
    }
}
