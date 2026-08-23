using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AstroAces.World;

namespace AstroAces.Tests.PlayMode
{
    /// <summary>Phase 6 regression coverage for the soft play-area boundary -- confirmed
    /// working organically during Phase 6 verification (the test rig drifted out of bounds
    /// during an unrelated screenshot session and "RETURN TO PLAY AREA" appeared on its
    /// own), but that was luck, not a real test. This teleports the rig out on purpose.</summary>
    public class PlayAreaBoundsPlayModeTests
    {
        const string SceneName = "Dogfight";
        const string RigName = "PlayerAircraft (Phase1 Test Rig)";

        [UnityTest]
        public IEnumerator OutsideBounds_PushesBackTowardCentre_AndShowsWarning()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            GameObject rig = GameObject.Find(RigName);
            Assert.IsNotNull(rig);
            Assert.IsNotNull(rig.GetComponent<PlayAreaBounds>(), "Test rig is missing PlayAreaBounds.");

            var rb = rig.GetComponent<Rigidbody>();
            // Well past the 2500m half-size boundary. MUST go through rb.position/rotation,
            // not transform.position/rotation -- the rig is a non-kinematic Rigidbody with
            // interpolation on, so the physics engine drives the Transform every FixedUpdate
            // and silently overwrites a direct Transform edit on the very next physics step
            // (confirmed directly: an earlier version of this test set transform.position and
            // traced the rig sitting at the original spawn point on every subsequent tick,
            // never actually moving). Also zero velocity and align rotation to the outward
            // direction -- a teleport with the OLD rotation still pointing elsewhere sends
            // thrust off in a mismatched direction and touches off a chaotic transient (high
            // AoA, big lift/drag swings) that can fling the rig back in-bounds within a
            // couple of ticks, before this test ever gets to check anything. Isolate the one
            // thing actually under test: does PlayAreaBounds react to being outside the
            // boundary.
            rb.position = new Vector3(4000f, 400f, 0f);
            rb.rotation = Quaternion.LookRotation(Vector3.right);
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            yield return null; // let PlayAreaBounds.Awake() resolve its MessageLog reference

            float vxBefore = rb.linearVelocity.x;
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            var message = GameObject.Find("Message")?.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(message, "MessageLog didn't create its 'Message' text object.");
            Assert.AreEqual("RETURN TO PLAY AREA", message.text);
            Assert.Greater(message.color.a, 0.9f, "Warning message should be fully visible while out of bounds.");

            float vxAfter = rb.linearVelocity.x;
            Assert.Less(vxAfter, vxBefore,
                $"Push-back force should have added a negative (toward-centre) X velocity " +
                $"component within 2 physics steps -- was {vxBefore:F1}, now {vxAfter:F1}.");
        }
    }
}
