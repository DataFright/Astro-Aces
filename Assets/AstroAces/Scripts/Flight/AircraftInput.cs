using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroAces.Flight
{
    /// <summary>
    /// Reads the New Input System devices and exposes them as plain properties. Stores
    /// nothing else -- no state, no mutation of other components. This project is New
    /// Input System only (see DESIGN.md §11): UnityEngine.Input.* throws at runtime, so
    /// everything here goes through Keyboard.current / Mouse.current, both null-guarded
    /// since either device can legitimately be absent.
    ///
    /// Locks and hides the cursor while this component is enabled, since mouse-look needs
    /// raw delta rather than a cursor pinned at the screen edge. Unity's Editor always lets
    /// Escape force-release a locked cursor regardless of game logic, so there is no way to
    /// get stuck even before Phase 11's pause menu exists.
    ///
    /// AA-006: `CursorLockMode.Locked` does not reliably keep the OS cursor pinned to the
    /// exact window centre, especially inside the Unity EDITOR's Game view (confirmed by
    /// the user -- a one-frame "ignore the first delta" fix did not resolve it, meaning the
    /// corruption isn't a single startup kick, it's a real position mismatch that every
    /// subsequent delta reading inherits until something re-syncs it -- "I have to keep
    /// restabilizing"). Fixed the standard way every Unity mouselook controller that
    /// doesn't trust `CursorLockMode` alone uses: explicitly `WarpCursorPosition` the
    /// cursor to centre, both (a) synchronously the instant locking engages, in
    /// `SetLocked`, so the position discontinuity from wherever the cursor was during free
    /// movement never reaches a delta read at all, and (b) every single frame thereafter in
    /// `Update`, so it structurally cannot drift again regardless of what the Editor's own
    /// lock implementation does under the hood.
    ///
    /// AA-007: `Time.timeScale = 0` (TempPauseToggle's pause) does NOT stop `Update()` --
    /// only `FixedUpdate` and time-scaled effects respect it. Without an explicit gate here,
    /// mouse movement kept changing `MouseDelta` (and therefore `AircraftAimController`'s
    /// `DesiredDirection`) while "paused," which is why the aim marker kept drifting even
    /// with the game frozen. `GamePaused` is how `TempPauseToggle` tells this component to
    /// go inert -- every property reports a neutral value while paused, so nothing downstream
    /// needs its own pause check.
    /// </summary>
    public class AircraftInput : MonoBehaviour
    {
        public Vector2 MouseDelta { get; private set; }
        public bool ThrottleUpHeld { get; private set; }      // W
        public bool ThrottleDownPressed { get; private set; } // S, edge-triggered -- see DESIGN.md §2.11
        public float ScrollNotches { get; private set; }      // mouse wheel, +1 per notch up
        public float RollAxis { get; private set; }           // A = -1 (right wing up), D = +1 (left wing up)
        public float PitchAxis { get; private set; }          // E = +1 (nose up), Q = -1 (nose down)
        public bool FireHeld { get; private set; }             // left mouse, continuous
        public bool FreeLookHeld { get; private set; }         // right mouse
        public bool AirbrakeToggled { get; private set; }      // F, edge-triggered
        public bool ZoomToggled { get; private set; }          // Caps Lock, edge-triggered
        public bool PausePressed { get; private set; }         // Esc, edge-triggered

        /// <summary>Set by TempPauseToggle. While true, every property above reads as
        /// neutral/zero and the cursor is left alone (TempPauseToggle owns cursor state
        /// during pause via SetLocked).</summary>
        public static bool GamePaused { get; set; }

        bool locked;

        // AA-006: SetLocked's warp is read-then-warp-safe for the frame it happens on, but
        // real hardware testing (BUGS.md AA-006, 2026-08-22) showed the warp's own cursor
        // movement can still surface as a real Mouse.delta reading up to two Update() frames
        // LATER -- size and direction matching wherever the real cursor was before locking
        // engaged, landing anywhere up to the full 55 deg aim cone on one bad frame. A
        // fully-automated test never reproduced this (no real hardware mouse device behind
        // it); three real Play Mode runs with per-frame telemetry did, consistently, always
        // on the third captured tick. Discard delta for a few frames after every lock
        // transition (not just the one frame the original fix attempt discarded) so that
        // echo is gone before any reading is trusted again.
        const int LockSettleFrames = 3;
        int settleFramesRemaining;

        void OnEnable() => SetLocked(true);
        void OnDisable() => SetLocked(false);

        public void SetLocked(bool value)
        {
            locked = value;
            Cursor.lockState = value ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !value;

            if (value)
            {
                settleFramesRemaining = LockSettleFrames;

                // Warp synchronously, right here, not just in next Update()'s per-frame
                // recentre below. The cursor's position the instant BEFORE this call is
                // whatever it was doing during free/unlocked movement (resting somewhere
                // arbitrary before Play, or wherever the player clicked during pause) --
                // that is a genuine position discontinuity, and the very next delta read
                // would otherwise be computed against that stale, unrelated position.
                if (Mouse.current != null)
                    Mouse.current.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));
            }
        }

        void Update()
        {
            if (GamePaused)
            {
                MouseDelta = Vector2.zero;
                ThrottleUpHeld = false;
                ThrottleDownPressed = false;
                ScrollNotches = 0f;
                RollAxis = 0f;
                PitchAxis = 0f;
                FireHeld = false;
                FreeLookHeld = false;
                AirbrakeToggled = false;
                ZoomToggled = false;
                PausePressed = false;
                return;
            }

            Keyboard kb = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (mouse != null)
            {
                // Input System delta is already per-frame -- never multiply by Time.deltaTime.
                MouseDelta = mouse.delta.ReadValue();
                ScrollNotches = mouse.scroll.ReadValue().y / 120f;   // Windows reports +-120 per notch
                FireHeld = mouse.leftButton.isPressed;
                FreeLookHeld = mouse.rightButton.isPressed;

                // AA-006: force the cursor back to dead centre every frame rather than
                // trusting CursorLockMode.Locked to do it. Read delta FIRST, warp AFTER --
                // warping itself can generate a delta on some backends, so warping before
                // the read would corrupt this frame's own sample.
                if (locked)
                    mouse.WarpCursorPosition(new Vector2(Screen.width / 2f, Screen.height / 2f));
            }
            else
            {
                MouseDelta = Vector2.zero;
                ScrollNotches = 0f;
                FireHeld = false;
                FreeLookHeld = false;
            }

            if (settleFramesRemaining > 0)
            {
                settleFramesRemaining--;
                MouseDelta = Vector2.zero;
            }

            if (kb != null)
            {
                ThrottleUpHeld = kb.wKey.isPressed;
                ThrottleDownPressed = kb.sKey.wasPressedThisFrame;
                RollAxis = (kb.dKey.isPressed ? 1f : 0f) - (kb.aKey.isPressed ? 1f : 0f);
                PitchAxis = (kb.eKey.isPressed ? 1f : 0f) - (kb.qKey.isPressed ? 1f : 0f);
                AirbrakeToggled = kb.fKey.wasPressedThisFrame;
                ZoomToggled = kb.capsLockKey.wasPressedThisFrame;
                PausePressed = kb.escapeKey.wasPressedThisFrame;
            }
            else
            {
                ThrottleUpHeld = false;
                ThrottleDownPressed = false;
                RollAxis = 0f;
                PitchAxis = 0f;
                AirbrakeToggled = false;
                ZoomToggled = false;
                PausePressed = false;
            }
        }
    }
}
