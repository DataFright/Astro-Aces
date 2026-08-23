using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UTI;

namespace AstroAces.Flight
{
    /// <summary>
    /// TEMPORARY. Built to check AA-006 (BUGS.md) without a human at the mouse: logs the REAL
    /// OS cursor position every frame via a BeanTracker's CustomCapture, the same pattern
    /// LoopDiagnostic already uses for flight telemetry. Reads
    /// <c>Mouse.current.position</c> (New Input System), not UTI's own BeanMouseTracker --
    /// this project's Active Input Handling is New-Input-System-only (ProjectSettings
    /// activeInputHandler: 1, see DESIGN.md Sec 11), under which BeanMouseTracker's legacy
    /// Input.mousePosition read is unavailable (as of UTI v0.2.2 it degrades to a one-time
    /// warning and holds position, rather than throwing every frame like older versions) --
    /// so it would silently produce a flat, useless log here.
    ///
    /// AircraftInput actively Mouse.WarpCursorPosition()s the real OS cursor to screen centre
    /// every Update() while locked (see BUGS.md AA-006). If that's working, deltaFromCenter
    /// should read ~0 every sample. This does NOT reproduce AA-006's original trigger (a human
    /// actively moving the mouse while the lock fights to re-centre it) -- with nobody
    /// touching the mouse, the only motion source is our own warp call, so this specifically
    /// tests "does Mouse.WarpCursorPosition() actually move the real cursor in this Editor's
    /// Game view at all," not "does it keep up with real human movement." A real answer to the
    /// latter still needs a human. Said so explicitly rather than overclaiming what a
    /// hands-off run can prove.
    ///
    /// Delete alongside the other debug readouts once Phase 13's real tests exist.
    /// </summary>
    [RequireComponent(typeof(BeanTracker))]
    public class CursorDriftDiagnostic : MonoBehaviour
    {
        void Awake()
        {
            GetComponent<BeanTracker>().CustomCapture = Capture;
        }

        Dictionary<string, float> Capture(GameObject go)
        {
            Mouse mouse = Mouse.current;
            Vector2 pos = mouse != null ? mouse.position.ReadValue() : Vector2.zero;
            Vector2 center = new Vector2(Screen.width / 2f, Screen.height / 2f);

            return new Dictionary<string, float>
            {
                ["mouseScreenX"] = pos.x,
                ["mouseScreenY"] = pos.y,
                ["screenCenterX"] = center.x,
                ["screenCenterY"] = center.y,
                ["deltaFromCenter"] = Vector2.Distance(pos, center),
                ["cursorLockState"] = (float)Cursor.lockState,
            };
        }
    }
}
