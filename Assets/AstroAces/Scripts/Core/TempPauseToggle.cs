using UnityEngine;
using UnityEngine.InputSystem;
using AstroAces.Flight;
using UTI;

namespace AstroAces.Core
{
    /// <summary>
    /// TEMPORARY stopgap for Esc, requested before Phase 11's real pause menu exists --
    /// without this there was no way to stop the simulation short of hunting for the
    /// Unity Editor's own Stop button while the aircraft keeps flying. Toggles
    /// Time.timeScale, hands cursor control back through AircraftInput.SetLocked, and sets
    /// AircraftInput.GamePaused (see AA-007 in BUGS.md) -- Time.timeScale alone does NOT
    /// stop Update(), so without that flag the aim kept drifting from residual mouse
    /// movement even with the aircraft's physics frozen.
    ///
    /// Reads Escape directly rather than through AircraftInput.PausePressed to avoid a
    /// MonoBehaviour execution-order dependency between two components on one GameObject
    /// (see AircraftState's Refresh() comment for the general reasoning).
    ///
    /// Also stops/restarts the UTI BeanTracker (if one is on this GameObject) on
    /// pause/resume, purely so pausing doubles as "capture a snapshot of the flight path so
    /// far" -- BeanSnapshotExporter's captureOnStopTracking fires off StopTracking(), which
    /// otherwise nothing in this project ever calls.
    ///
    /// Delete this file once Phase 11's GameStateController exists -- that owns the real
    /// Flying/Paused/Dead state machine, restart, and quit.
    /// </summary>
    [RequireComponent(typeof(AircraftInput))]
    public class TempPauseToggle : MonoBehaviour
    {
        AircraftInput input;
        BeanTracker tracker;
        bool paused;

        void Awake()
        {
            input = GetComponent<AircraftInput>();
            tracker = GetComponent<BeanTracker>();   // optional -- fine if absent
        }

        void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

            paused = !paused;
            Time.timeScale = paused ? 0f : 1f;
            input.SetLocked(!paused);
            AircraftInput.GamePaused = paused;

            if (tracker != null)
            {
                if (paused) tracker.StopTracking();
                else tracker.StartTracking();
            }
        }

        void OnGUI()
        {
            if (!paused) return;
            var style = new GUIStyle(GUI.skin.box)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };
            GUI.Box(new Rect(Screen.width / 2f - 220, Screen.height / 2f - 70, 440, 140),
                    "PAUSED\n\nPress Esc to resume", style);
        }
    }
}
