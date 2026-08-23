using UnityEngine;
using UnityEngine.UI;
using AstroAces.Flight;

namespace AstroAces.UI
{
    /// <summary>
    /// DESIGN.md Sec 7.1: two markers, "exactly the War Thunder split between 'where the guns
    /// point' and 'where you asked to go'." The gunnery reticle sits at
    /// aircraft.position + aircraft.forward * cfg.crosshairDistance -- nose-fixed, so it is
    /// always dead centre and genuinely shows where rounds will go, matching
    /// Projectile.Launch's muzzle-forward direction. The smaller aim marker projects
    /// AircraftAimController.DesiredDirection at the same distance (only the direction
    /// matters for where a point lands on screen; the exact distance along the ray does not,
    /// besides needing to stay in front of the camera).
    /// </summary>
    [RequireComponent(typeof(AircraftAimController))]
    public class CrosshairController : MonoBehaviour
    {
        [SerializeField] AircraftConfig cfg;

        const float GunnerySize = 64f;
        const float AimMarkerSize = 28f;

        AircraftAimController aim;
        Camera cam;
        RectTransform canvasRect;

        RectTransform gunneryRect;
        RawImage gunneryImage;
        RectTransform aimRect;
        RawImage aimImage;

        void Awake()
        {
            aim = GetComponent<AircraftAimController>();

            Canvas canvas = HudCanvasUtility.CreateOverlayCanvas("HUD Canvas (Crosshair)");
            canvasRect = canvas.GetComponent<RectTransform>();

            Texture2D reticle = CrosshairTexture.Create();
            gunneryRect = CreateMarker(canvas.transform, "Gunnery Reticle", reticle, GunnerySize, out gunneryImage);
            aimRect = CreateMarker(canvas.transform, "Aim Marker", reticle, AimMarkerSize, out aimImage);
        }

        static RectTransform CreateMarker(Transform parent, string name, Texture2D texture, float size, out RawImage image)
        {
            var go = new GameObject(name, typeof(RawImage));
            go.transform.SetParent(parent, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);

            image = go.GetComponent<RawImage>();
            image.texture = texture;
            image.raycastTarget = false;

            return rect;
        }

        void Update()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }

            PositionMarker(gunneryRect, gunneryImage, transform.position + transform.forward * cfg.crosshairDistance);
            PositionMarker(aimRect, aimImage, transform.position + aim.DesiredDirection * cfg.crosshairDistance);
        }

        void PositionMarker(RectTransform rect, RawImage image, Vector3 worldPoint)
        {
            Vector3 screenPoint = cam.WorldToScreenPoint(worldPoint);

            // Behind the camera -- DESIGN.md Sec 7.1: "hide the reticle when its screen point
            // is behind the camera."
            if (screenPoint.z < 0f)
            {
                image.enabled = false;
                return;
            }

            image.enabled = true;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);
            rect.anchoredPosition = localPoint;
        }
    }
}
