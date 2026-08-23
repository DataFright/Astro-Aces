using TMPro;
using UnityEngine;
using AstroAces.Flight;

namespace AstroAces.UI
{
    /// <summary>
    /// Top-left flight readout -- DESIGN.md Sec 7 / BUILD_PLAN.md Phase 5. Replaces
    /// Phase1DebugReadout's OnGUI box with a real Screen Space Overlay canvas built in code
    /// (HudCanvasUtility), matching every other HUD element's "no hand-authored scene UI"
    /// approach.
    ///
    /// Deliberately shows the RAW instantaneous values, not Phase1DebugReadout's 60-sample
    /// smoothed speed -- that smoothing existed only to make an otherwise-flickering number
    /// readable during Phase 1's engine-forces-only testing before any rotation control
    /// existed; the real HUD has no reason to hide real fluctuation from the player.
    /// </summary>
    [RequireComponent(typeof(AircraftState))]
    [RequireComponent(typeof(AircraftEngine))]
    public class HudController : MonoBehaviour
    {
        AircraftState state;
        AircraftEngine engine;
        TextMeshProUGUI readout;

        void Awake()
        {
            state = GetComponent<AircraftState>();
            engine = GetComponent<AircraftEngine>();

            Canvas canvas = HudCanvasUtility.CreateOverlayCanvas("HUD Canvas (Flight Readout)");

            var textGO = new GameObject("Readout", typeof(TextMeshProUGUI));
            textGO.transform.SetParent(canvas.transform, false);

            var rect = textGO.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -24f);
            rect.sizeDelta = new Vector2(320f, 140f);

            readout = textGO.GetComponent<TextMeshProUGUI>();
            readout.fontSize = 28f;
            readout.color = Color.white;
            readout.alignment = TextAlignmentOptions.TopLeft;
            readout.raycastTarget = false;
        }

        void Update()
        {
            readout.text =
                $"AOA  {state.AngleOfAttack,5:0.0}°\n" +
                $"ALT  {state.AltitudeMeters * Aero.MetersToFeet,6:0} ft\n" +
                $"SPD  {state.AirspeedMps * Aero.MpsToMph,5:0} mph\n" +
                $"THR  {engine.Throttle * 100f,3:0}%";
        }
    }
}
