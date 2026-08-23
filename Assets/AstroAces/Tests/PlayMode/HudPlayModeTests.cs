using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using AstroAces.Flight;

namespace AstroAces.Tests.PlayMode
{
    /// <summary>Phase 5 regression coverage: the real HUD/crosshair/message log built to
    /// replace Phase1DebugReadout and Phase2DebugReadout (both deleted this phase). No
    /// simulated input needed here -- these are driven directly through the same public
    /// APIs a real caller (AircraftEngine, AircraftAimController) would use, so this is a
    /// plain test class rather than an InputTestFixture.</summary>
    public class HudPlayModeTests
    {
        const string SceneName = "Dogfight";
        const string RigName = "PlayerAircraft (Phase1 Test Rig)";

        GameObject rig;

        IEnumerator LoadRig()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;

            rig = GameObject.Find(RigName);
            Assert.IsNotNull(rig);
        }

        [UnityTest]
        public IEnumerator Hud_ReadoutShowsAllFourFields_InExpectedFormat()
        {
            yield return LoadRig();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            var readout = GameObject.Find("Readout")?.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(readout, "HudController didn't create its 'Readout' text object.");

            string text = readout.text;
            Assert.IsTrue(Regex.IsMatch(text, @"AOA\s+-?\d+\.\d+°"), $"Missing/malformed AOA line: {text}");
            Assert.IsTrue(Regex.IsMatch(text, @"ALT\s+-?\d+ ft"), $"Missing/malformed ALT line: {text}");
            Assert.IsTrue(Regex.IsMatch(text, @"SPD\s+\d+ mph"), $"Missing/malformed SPD line: {text}");
            Assert.IsTrue(Regex.IsMatch(text, @"THR\s+\d+%"), $"Missing/malformed THR line: {text}");
        }

        [UnityTest]
        public IEnumerator Crosshair_GunneryReticle_VisibleDuringForwardFlight()
        {
            yield return LoadRig();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();
            yield return null; // let CrosshairController.Update() run at least once

            var gunnery = GameObject.Find("Gunnery Reticle")?.GetComponent<RawImage>();
            Assert.IsNotNull(gunnery, "CrosshairController didn't create the 'Gunnery Reticle' marker.");
            Assert.IsTrue(gunnery.enabled,
                "Gunnery reticle should be visible while flying straight and level -- it's directly in front of the camera.");
        }

        [UnityTest]
        public IEnumerator MessageLog_ShowsOnAirbrakeToggle_ThenFadesToZero()
        {
            yield return LoadRig();
            for (int i = 0; i < 5; i++) yield return new WaitForFixedUpdate();

            var engine = rig.GetComponent<AircraftEngine>();
            var message = GameObject.Find("Message")?.GetComponent<TextMeshProUGUI>();
            Assert.IsNotNull(message, "MessageLog didn't create its 'Message' text object.");
            Assert.AreEqual(0f, message.color.a, 0.01f, "Message should start invisible before anything happens.");

            engine.ToggleAirbrakes();
            yield return null;

            Assert.AreEqual("AIRBRAKES DOWN", message.text);
            Assert.AreEqual(1f, message.color.a, 0.01f, "Message should be fully visible right after triggering.");

            // Fade is a 2s linear ramp -- 3s later it should be back to fully invisible.
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                yield return new WaitForFixedUpdate();
                elapsed += Time.fixedDeltaTime;
            }

            Assert.AreEqual(0f, message.color.a, 0.02f, "Message should have faded fully to invisible after 3s.");
        }
    }
}
