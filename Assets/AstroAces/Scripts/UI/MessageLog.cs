using TMPro;
using UnityEngine;
using AstroAces.Flight;

namespace AstroAces.UI
{
    /// <summary>
    /// Centre-low transient message text, DESIGN.md Sec 7 -- 2 second linear fade from full
    /// opacity. Subscribes to AircraftEngine.OnAirbrakeChanged for "AIRBRAKES DOWN"/
    /// "AIRBRAKES UP"; Show(string) is public so later phases (out-of-bounds warnings,
    /// Phase 6's PlayAreaBounds) can reuse the same log without a second implementation.
    /// </summary>
    [RequireComponent(typeof(AircraftEngine))]
    public class MessageLog : MonoBehaviour
    {
        const float FadeDurationSeconds = 2f;

        AircraftEngine engine;
        TextMeshProUGUI text;
        float fadeRemaining;

        void Awake()
        {
            engine = GetComponent<AircraftEngine>();
            engine.OnAirbrakeChanged += HandleAirbrakeChanged;

            Canvas canvas = HudCanvasUtility.CreateOverlayCanvas("HUD Canvas (Message Log)");

            var textGO = new GameObject("Message", typeof(TextMeshProUGUI));
            textGO.transform.SetParent(canvas.transform, false);

            var rect = textGO.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 90f);
            rect.sizeDelta = new Vector2(700f, 60f);

            text = textGO.GetComponent<TextMeshProUGUI>();
            text.fontSize = 32f;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            SetAlpha(0f);
        }

        void OnDestroy()
        {
            if (engine != null)
                engine.OnAirbrakeChanged -= HandleAirbrakeChanged;
        }

        void HandleAirbrakeChanged(bool on) => Show(on ? "AIRBRAKES DOWN" : "AIRBRAKES UP");

        public void Show(string message)
        {
            text.text = message;
            fadeRemaining = FadeDurationSeconds;
            SetAlpha(1f);
        }

        void Update()
        {
            if (fadeRemaining <= 0f) return;

            fadeRemaining -= Time.deltaTime;
            SetAlpha(Mathf.Clamp01(fadeRemaining / FadeDurationSeconds));
        }

        void SetAlpha(float alpha)
        {
            Color c = text.color;
            c.a = alpha;
            text.color = c;
        }
    }
}
