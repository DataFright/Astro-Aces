using UnityEngine;
using UnityEngine.UI;

namespace AstroAces.UI
{
    /// <summary>
    /// Shared helper: every real HUD element (HudController, CrosshairController,
    /// MessageLog) needs its own Screen Space Overlay canvas, generated at runtime like
    /// everything else in the HUD -- none of them should depend on a hand-authored scene
    /// Canvas that could drift out of sync with the code that reads it.
    /// </summary>
    internal static class HudCanvasUtility
    {
        public static Canvas CreateOverlayCanvas(string name, int sortOrder = 0)
        {
            var go = new GameObject(name, typeof(Canvas), typeof(CanvasScaler));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            return canvas;
        }
    }
}
