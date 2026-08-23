# Astro Aces — Bugs

One entry per bug, kept until proven closed. Every entry names the test that proves it.

**Status key:** `OPEN` · `IN PROGRESS` · `FIXED (unverified)` · `CLOSED`

---

## Open

### AA-012 — Unity Play Mode simulation doesn't tick while the Editor window is unfocused (tooling/environment, not game code)
**Status:** OPEN · found 2026-08-22/23, across multiple sessions this week (informally noted in
`TOOLING.md` before this ticket existed) · formally ticketed 2026-08-23 at the user's explicit
request ("is there a bug for this? a tracked... make a ticket") · reproduced fresh, with
timestamps, in this same session

**This is an environment/tooling problem, not a bug in `AstroAces.Runtime` game code** —
tracked here as a ticket per the user's explicit request, even though `TOOLING.md` is this
project's normal home for tool/environment issues (see that doc's own 2026-08-23 entry for
the original, less formal writeup). Both places should be kept in sync if this changes.

**Symptom:** after `manage_editor action:"play"` reports success, `Time.frameCount`/`Time.time`
(read live via `execute_code`) can stay **completely frozen** — not slow, literally
unchanging — for 30+ real seconds, sometimes longer, while `Time.realtimeSinceStartup` (which
tracks real wall-clock time regardless of simulation state) keeps climbing normally the whole
time. `mcpforunity://editor/state` agrees: `play_mode.is_playing: true`,
`play_mode.is_changing: true`, `activity.phase: "playmode_transition"`, `editor.is_focused:
false`, for the entire stuck duration. The Editor process itself is alive and responsive
(`execute_code` calls return promptly with correct results for anything not simulation-time
dependent) — specifically the Play Mode player loop isn't advancing.

**Fresh reproduction, this session, with real timestamps (not reused from an earlier
session):**
| Real wall-clock time since `play` requested | `Time.frameCount` | `Time.realtimeSinceStartup` |
| --- | --- | --- |
| 0s (play requested) | — | 8.83s |
| ~4.9s (`editor/state` read) | 2 | — |
| ~19.7s | 2 | 21.63s |
| ~35.4s | 2 | 37.33s |

`Time.frameCount` never moved off `2` across the entire 35+ second window, while
`realtimeSinceStartup` advanced by roughly the same amount as real elapsed time — the Editor
is doing *something* every second, just not advancing the Play Mode simulation.

**Not a total freeze — confirmed separately, same session:** in an earlier reproduction
today, the rendered Game view and physics state *were* slowly progressing despite
`Time.frameCount` reading stuck (AOA ticked 0.0°→0.1° across two screenshots taken 10 real
seconds apart, while `Time.frameCount` read identically both times). So the simulation isn't
always fully halted — it can also run at a tiny fraction of normal speed while unfocused,
which is arguably worse for diagnosis than a clean freeze, since `Time.frameCount` alone can't
be trusted to distinguish "stalled" from "running at ~1% speed."

**Suspected root cause (not confirmed):** Unity Editor throttling/deprioritizing the Player
Loop when the Editor window has no OS focus and isn't the active window — a documented general
Unity behavior (relevant setting: `Application.runInBackground` / Editor's own background
throttling), plausibly worse in this specific environment because the Editor is never actually
focused by a human during most autonomous sessions. **Not yet tested**: whether OS-level
window focus (bringing the Unity Editor window to the foreground) fixes it, because doing so
requires real desktop mouse/window control that isn't available in this session — see below.

**What's blocked and why:** the user asked to reproduce this live, then move the mouse to the
Unity Editor's Game view and left-click, to test whether focusing the window unsticks the
simulation. Checked what's actually available this session: `mcp__Claude_Browser__computer`
and `mcp__claude-in-chrome__computer` both control a browser surface (this app's sandboxed
Browser pane, or a real Chrome tab respectively) — neither can reach a native desktop
application window like the Unity Editor. There is no general OS-level mouse/window
automation tool available. **This specific test needs a human to actually click into the
Unity Editor**, which is the same "no GUI control over the Unity window" limitation noted
repeatedly throughout this project's history (e.g. `HANDOFF.md`'s 2026-08-17 15:39 entry).

**Documented workaround (already in `TOOLING.md`), not a fix:** `stop` Play Mode, `refresh_unity`
(mode `force`), retry `play`. Has recovered every time it's been needed so far, sometimes
needing a second attempt. Recovery time itself is unpredictable while unfocused (confirmed
30+ seconds on multiple occasions).
**Verified:** reproduced multiple times across at least two sessions; the specific numbers in
the table above are from a fresh reproduction with real timestamps, not carried over from
memory.
**Guarded by:** nothing automated — this is Editor/environment behavior outside project code,
not something a PlayMode test can assert against.
**Lesson:** don't trust `Time.frameCount` alone as evidence of "nothing is happening" — it can
read frozen while the underlying simulation is genuinely (if very slowly) progressing. Cross-
check with a real observable (a screenshot's HUD values, `Time.realtimeSinceStartup`, or
console output) before concluding total stall vs. severe throttling.

### AA-011 — Free-look camera drifts/leaves the ship off-frame during flight, not just while dragging the mouse
**Status:** OPEN · found 2026-08-23 by the user, immediately after the 2026-08-23 distance
retune (see `DESIGN.md` §5's log and `HANDOFF.md`) · root-caused and reproduced by direct
inspection + a controlled live repro on the real `ChaseCamera`/rig objects via Unity MCP,
after two prior attempts (one by a previous session, one earlier this session) each declared
this "fixed" without actually reproducing the failure — see the Lesson at the bottom.

User provided four screenshots: a standard chase view (called "pretty far back but not too
bad" — not the bug), and three free-look shots where the ship is pushed toward or off the
edge of frame — one nearly 50%+ off-screen. Direct quote: "the more we focus the more we
should see the craft in the center of the screen," matching this project's own free-look
design intent (`DESIGN.md` §5: free-look exists to inspect the ship up close) — instead the
opposite happens.

**Root cause**, in `ChaseCamera.UpdateFollow()`'s orbiting branch:
```csharp
Vector3 orbitOffset = freeLookOffset * (localOffset.normalized * freeLookOrbitDistance);
Vector3 desiredPosition = target.TransformPoint(orbitOffset);

Vector3 toTarget = target.position - desiredPosition;   // <- bug: desiredPosition, not the
desiredRotation = ... Quaternion.LookRotation(toTarget.normalized, target.up) ...           //    camera's actual position
```
`desiredRotation` aims from `desiredPosition` — the spot the camera is still travelling
*toward* — not from `transform.position`, the camera's real current position. Position is
then separately Lerped toward `desiredPosition` on the next two lines. The two only agree
once the camera has fully caught up. While the ship is in motion (every real flight — the
camera is chasing an offset that itself continuously moves with the ship), `desiredPosition`
never stops advancing, so the camera's real position permanently lags behind it — meaning the
look direction is *permanently* computed from the wrong point, not just during an active mouse
drag. This is a steady-state error, not a transient one.

**Replication (reproduced live, not just theorized):** froze the test rig's Rigidbody
(`isKinematic = true`, same isolation technique as AA-010's test) and drove its `position`
directly at a constant 114 m/s (documented cruise speed) in a straight line for 2 simulated
seconds, while holding `ChaseCamera`'s private `freeLookYawDeg`/`freeLookPitchDeg` fields at a
**fixed** value the whole time (no simulated mouse movement at all, isolating "just flying
with free-look held steady" from "actively dragging the mouse") — stepped the exact current
`UpdateFollow()` formulas by hand (`Time.deltaTime` reads as ~0 in this environment's Play
Mode while unfocused; see `TOOLING.md`'s 2026-08-23 entry, so the real per-frame callback
can't be relied on to tick fast enough to observe this directly — stepped the identical
formulas manually against the live objects instead, then disabled the component briefly so
its own next real (if slow) `LateUpdate` tick didn't stomp the reproduced pose before the
screenshot could be taken).
- **Yaw held at 60°, ship flying straight:** ship settles at 18.8% across / 63.7% up the
  frame (dead centre is 50%/50%) and **stays there indefinitely**, not just briefly —
  confirmed both numerically (`Camera.WorldToScreenPoint`) and visually
  (`Assets/Screenshots/screenshot-20260823-112112.png`, taken on the real live camera/rig).
- **Pitch axis is worse:** sweeping pitch alone (yaw fixed at 0) while flying: pitch 30°
  already pushes the ship to 104.6% up the frame (just past the top edge), pitch 50° to
  140.3% (well above the frame — `Assets/Screenshots/screenshot-20260823-112201.png` shows
  only a sliver of the ship's nose at the very top), pitch 70° to 206.9%. This matches the
  user's most extreme screenshot ("looking up at the top of the plane... leaving the
  window") almost exactly.
- Full numeric sweep (yaw 0→120° over 1s, ship stationary) did **not** show the effect
  clearly — the steady-state version (ship in motion, free-look angle held constant) is the
  cleaner, more universally-reproducible trigger, and matches the always-in-flight context of
  every one of the user's screenshots (200-280 mph in all four).

**Not yet fixed** — documenting first, per explicit user instruction, before any resolution
work. Direction discussed but not applied: compute the look-at rotation from the camera's
actual current position (after the position Lerp updates it) and the ship's actual current
position, not from either one's "desired"/goal value — see `DESIGN.md`'s eventual log entry
once this is actually resolved and verified, not just proposed.
**Verified:** reproduced live on the real `ChaseCamera`/rig objects (not a re-simulation) with
both a numeric screen-space measurement and an actual screenshot for two independent axes
(yaw, pitch). Not yet fixed, so nothing to verify a fix against yet.
**Guarded by:** no test yet — this needs a PlayMode test that checks framing *during* motion
with free-look held steady, not just the final settled state (see the Lesson below for why
the existing `ChaseCameraPlayModeTests` didn't catch this).
**Lesson:** this bug was declared fixed twice before being actually reproduced — once by
tightening the free-look distance (which changed a number but never touched the rotation
computation), and once mid-session by re-reading the code and reasoning about the mechanism
without first proving it with reproduction evidence. Both times, verification consisted of
letting free-look settle to a static angle and checking the *final* state — which structurally
cannot see this bug, since the error only exists while `desiredPosition` keeps moving, i.e.
during any real, continuous flight. Reproduce first, with evidence, before declaring root
cause understood or a fix correct — a plausible mechanism read from the code is not the same
as a demonstrated one.

### AA-010 — Free-look orbited the camera's own spot, not around the ship
**Status:** CLOSED · found 2026-08-22 by the user, immediately after AA-009's fix ("the camera
obrits but it orbits its camrea spot / it should look around the plane... for players who want
to see their ship in action look at wings and underneith and cockpit") · root-caused by inspection

The first free-look implementation composed `freeLookOffset` into `ChaseCamera`'s rotation
only — the camera's position never left its fixed chase spot behind the tail, so right-click
just panned the view from that one spot (could look behind you, but never see the wings,
underside, or cockpit). DESIGN.md Sec 5's actual intent is a real orbit around the aircraft.

**Fix:** `UpdateFollow()` now branches on whether free-look is active. While orbiting, it
rotates `localOffset` by the accumulated free-look yaw/pitch (`Quaternion.Euler(pitch, yaw,
0) * localOffset`), transforms that into world space around the target
(`target.TransformPoint(orbitOffset)`) to get the camera's new position, then looks back at
the aircraft from there (`Quaternion.LookRotation`). At zero free-look this reduces to
exactly the prior fixed-offset behavior. Position and rotation are still smoothed the same
way as before (exponential Lerp/Slerp).

**Verified:** new test `ChaseCameraPlayModeTests.FreeLook_OrbitsAroundShip_NotJustRotatesInPlace`
— asserts the camera's actual position moves (>3m), that it ends up looking back at the ship
(<15° off), and that it holds roughly the original orbit distance (within 30%). Took three
debugging passes to get the test itself right, not the fix:
1. First run failed ~75-86° off across several different wait strategies (fixed frame counts,
   real-time polling up to 8s) — looked like a genuine non-convergence bug at first.
2. Root cause of the *test* failure: the rig flies at ~114 m/s against only an ~8.5m orbit
   radius, and this environment's render-frame delivery while the Editor is unfocused is
   already known to be irregular (TOOLING.md) — the ship could move farther between
   `LateUpdate` calls than the entire camera-to-ship distance, swinging the orbit math's
   target position/rotation wildly frame to frame and never letting the Slerp settle. Froze
   the rig's Rigidbody (`isKinematic = true`, zero velocity) for the duration of this test to
   remove that confound — it isolates the camera math, which is the actual thing under test.
3. With the ship frozen, look-angle and position-delta passed immediately, but distance was
   still off (5.7-6.0m vs an ~8.5-12m baseline). This was a second, genuine test-geometry
   issue, not a product bug: `Vector3.Lerp` moves the camera along a straight chord between
   its old and new points on the (spherical) orbit, and any chord between two points on a
   sphere's surface passes inside the sphere — so distance-from-ship legitimately dips
   mid-transition before recovering. The test's break condition fired as soon as look-angle
   and position-delta cleared their thresholds, which is typically still mid-dip. Added a
   1.5s real-time settle window after the break, before measuring distance.

All 12 PlayMode tests pass with this fix in place.
**Guarded by:** `FreeLook_OrbitsAroundShip_NotJustRotatesInPlace` — see above.
**Lesson:** a test that fails consistently across multiple different wait/polling strategies
isn't automatically evidence of a product bug — it can equally mean the test's own physical
setup (a fast-moving target orbited at close range) or its own math (chord vs. arc distance)
is what's actually unstable. Isolate the confound (freeze what shouldn't matter) before
concluding the code under test is wrong.

### AA-009 — Free-look (right mouse) still steers the aircraft, not just the camera
**Status:** CLOSED · found 2026-08-22 by the user ("it kinda works but the ship still moves
as you look so its not true free look") · root-caused by inspection

DESIGN.md Sec 5 is explicit: holding right mouse orbits the camera "up to ±120° yaw / ±70°
pitch... Flight direction is untouched." `ChaseCamera` (Phase 4) implements the camera side
correctly — it reads `AircraftInput.FreeLookHeld` and only accumulates its own
`freeLookYawDeg`/`freeLookPitchDeg` while held. But `AircraftAimController.Update()` had no
matching gate at all: it reads the exact same `input.MouseDelta` every frame regardless of
`FreeLookHeld` and feeds it straight into `FlightControlLaw.StepAim`, updating
`DesiredDirection` (and therefore steering) the whole time. This gap existed since Phase 2 —
it was invisible until Phase 4 gave `FreeLookHeld` an actual consumer (`ChaseCamera`), since
nothing previously reacted to right-click at all.

**Fix:** `AircraftAimController.Update()` now returns immediately when `input.FreeLookHeld`
is true, before touching `StepAim` — the aim freezes at wherever it currently is (consistent
with the aim's own "never auto-centres" rule elsewhere, not a special-cased reset) and
resumes from there the moment free-look is released.
**Verified:** `AimTrackingPlayModeTests.FreeLook_MouseMovement_DoesNotChangeFlightDirection`
— holds right mouse + injects 20 frames of real mouse delta, asserts `DesiredDirection`
barely moves (<0.5°) while held, then confirms normal mouse-driven aim still works
immediately after release (not just "nothing ever moves it again"). Passed; all 11 PlayMode
tests pass.
**Guarded by:** the test above.

### AA-008 — Crosshair left behind / disappears during a manual loop
**Status:** CLOSED · found 2026-08-22 by the user,
immediately after AA-005's loop-completion-time fix (see DESIGN.md Sec 2.6 log) made the
aircraft's own rotation fast enough for this to become visible

User, after confirming the loop now completes: "that issue with the crosshair getting stuck
still happens as you move past it gets left behind and as the loop finishes it is just
gone." Not a new mechanism — the mouse aim (`AircraftAimController.DesiredDirection`) is
deliberately world-referenced and never auto-centres (DESIGN.md Sec 2.6: "stop moving the
mouse and the aircraft keeps turning until it gets there"). During a keyboard-only loop the
mouse never moves, so the aim stays fixed in world space while the aircraft rotates all the
way around it — it gets left behind, eventually ends up behind the aircraft (invisible to a
forward-looking camera), and stays stale as the target the control law banks/pitches toward
the instant the player releases E and mouse control resumes. Before the loop-time fix this
was easy to miss, since the aircraft barely rotated during the long crawl; now that it
completes at a normal pace the effect is obvious.
**Fix (first attempt):** `AircraftPhysics.FixedUpdate` called
`aim.SetDesiredDirection(transform.forward)` every step manual pitch was held.
**Confirmed broken by the user immediately:** this glued the aim EVERY frame regardless of
concurrent mouse input, so moving the mouse while also holding E did nothing — reported as
"E now inadvertently auto-recentring the crosshair... not intent." A real regression from a
fix that was too broad.

**Fix (second attempt, 2026-08-22, same session):** only glue when the player genuinely
isn't moving the mouse that frame — `if (input.MouseDelta.sqrMagnitude < 0.0001f)
aim.SetDesiredDirection(transform.forward);`. A frame with real mouse delta always wins,
exactly like ordinary mouse-driven aim.
**Verified:** by a new automated Play Mode test,
`AimTrackingPlayModeTests.AimTracking_ExtremeInput_AndE_Hold_MouseInterrupt`
(`Assets/AstroAces/Tests/PlayMode/AimTrackingPlayModeTests.cs`), using `InputTestFixture` —
this is pure game logic (not an OS/hardware quirk like AA-006), so simulated input is
trustworthy here. Covers, in one run: a single huge mouse flick (cone clamp holds at ~55°,
not exceeded), 20 rapid alternating-direction deltas (clamp still holds, no NaN), a ~2s
hands-off E hold (aim stays glued, <2° drift the whole time — AA-008's original fix still
works for the case it was built for), then a mouse nudge while E is STILL held (`DesiredDirection`
measurably changes, confirming the mouse now wins over the glue). Passed. Real telemetry
(mouseDeltaX/Y, offNoseDeg, DesiredDirection, forward) logged the whole run via the rig's
existing `LoopDiagnostic` + `BeanTracker`/`BeanLogger` — one caveat found reading it: the CSV
samples on `FixedUpdate` cadence while `MouseDelta` updates on `Update()` cadence, so the
exact row coincident with a mouse nudge can show `mouseDeltaX=0` even though the change
genuinely propagated (visible one row later as `DesiredDirection` jumping to a new fixed
value) — a sampling-cadence artifact, not a sign of failure; the C# assertion checks
`DesiredDirection` directly and is the trustworthy signal here, not a literal grep for a
nonzero `mouseDeltaX` cell.
**Guarded by:** `AimTracking_ExtremeInput_AndE_Hold_MouseInterrupt` — see above.

**Follow-up, same day — user reported "still stuck" after the second fix, root-caused as a
different problem entirely.** Suspected the vertical/inverted portion of the loop specifically
(user: "may have to do with... being upside down... it wants to flip fast and then its kinda
more fine"), which lines up with `SetDesiredDirection`'s `Atan2`/`Asin` decomposition — the
same class of singularity `FlightControlLaw.BankAngle` already has to guard against near
straight up/down. Built `FullLoopAimPlayModeTests.FullLoop_HandsOffE_AimStaysGluedThroughInvertedPortion`
(new file, kept as a permanent regression test) to check directly: held E hands-off through a
full 16s loop, tracked angle-off-nose every tick. **Result: max deviation 1.3° anywhere in
the whole loop, exactly 0.0° at the tick where pitch crossed 90° (dead vertical).** The
aim-tracking code is not the problem — confirmed clean, not just theorized.

**Real cause: the Phase 2 placeholder camera.** It's rigidly parented to the aircraft with no
independent orientation logic, so it rolls exactly with the plane — when the aircraft goes
inverted mid-loop, the player's entire screen flips upside-down for that stretch, which reads
as "the crosshair is stuck/broken" even though its underlying target direction never moved
more than a degree off the nose. Documented in `BUGS.md`'s known-risks table and flagged in
`BUILD_PLAN.md`'s Phase 4 section — not a bug to fix here, since the placeholder camera is
already scheduled for replacement in Phase 4 (`ChaseCamera`), and patching the throwaway
placeholder further isn't worth it.

---

AA-006's third fix attempt was confirmed by the user's own retest (four runs, 2026-08-22)
and is CLOSED alongside AA-004, AA-005 and AA-007. See Closed below.

---

## Closed

### AA-007 — Pause didn't actually freeze aim/input, only physics
**Status:** CLOSED · found 2026-08-18 by the user
· root-caused directly (a well-known Unity fact, not something needing investigation):
`Time.timeScale = 0` stops `FixedUpdate` but does **not** stop `Update()`

User: "menu is there but the aim cursor still phantomly follows even though game is
paused... aim cursor should pause and mouse cursor should come back for selection." The
mouse cursor DID come back correctly (`TempPauseToggle` already released it via
`AircraftInput.SetLocked(false)`). The aim didn't freeze because `TempPauseToggle` only
sets `Time.timeScale = 0`, which halts `AircraftPhysics.FixedUpdate` (so the aircraft's
actual flight visibly stops) — but `AircraftInput.Update()` and
`AircraftAimController.Update()` are ordinary `Update()` methods, which Unity keeps calling
every frame regardless of `timeScale`. With the cursor now free and visible for the pause
menu, any residual mouse movement (even just moving toward a future menu button) kept
changing `MouseDelta`, which kept changing `DesiredDirection` — invisible in the frozen
Game view itself, but directly visible through `Phase2DebugReadout`'s aim marker, and would
have caused a snap the instant physics resumed and started reading the drifted
`DesiredDirection`.
**Fix:** added `AircraftInput.GamePaused` (static, set by `TempPauseToggle` alongside
`Time.timeScale`) — while true, `AircraftInput.Update()` reports every property as neutral
and returns before touching any device, so nothing downstream (aim, throttle, roll/pitch)
can drift while paused. Single choke point rather than gating every consumer separately.
**Verified:** by inspection at the time — `Time.timeScale` not affecting `Update()` is
documented Unity behavior. **Now also verified by an automated Play Mode test**
(2026-08-22): `AA007_GamePaused_MouseMovement_DoesNotChangeAim` in
`Assets/AstroAces/Tests/PlayMode/Phase3ControlLawPlayModeTests.cs` sets
`AircraftInput.GamePaused = true`, injects five frames of large synthetic mouse delta via
`InputTestFixture`, and asserts both `AircraftInput.MouseDelta` stays `Vector2.zero` and
`AircraftAimController.DesiredDirection` never changes. Passed. This is a real Play Mode run
with a genuinely isolated virtual mouse device (see the file's header comment for why that's
different from, and more trustworthy than, a raw `InputSystem.QueueStateEvent` against the
live device), not a simulation of the behavior.
**Guarded by:** `AA007_GamePaused_MouseMovement_DoesNotChangeAim` — see above.
**Lesson:** `Time.timeScale = 0` is a common but incomplete pause primitive — it silently
does nothing to `Update()`-driven input reading. Any future pause/freeze logic (Phase 11's
real menu) needs the same explicit "stop reading input" step, not just the timescale zero.

### AA-006 — Spawn direction randomly kicked by wherever the mouse was on screen
**Status:** CLOSED — confirmed by the user's own retest (four runs, 2026-08-22), **fix
revised twice** · found 2026-08-18 by the user · first two fix attempts each tested by the
user and confirmed NOT to work; root-caused a third time, precisely, from real per-frame
telemetry

User: "when i start the game my current start mouse location effects my flight angle or
direction... it will go in a crazy direction so i have to readjust." First theory: locking
the cursor snaps the OS pointer to centre, and the very next `Mouse.delta` sample reports
that snap as real movement — a one-frame spurious kick. **Fix attempt 1** (discard one
delta sample after every lock transition) **did not work** — the user re-tested and
confirmed it, and pushed back with a clearer description: this isn't a one-frame startup
kick, it's an ongoing offset between where the cursor actually is and where the code assumes
"centre" is, that has to be fought continuously ("restabilize"), not just once at spawn.

That description points at a different mechanism: `CursorLockMode.Locked` does not reliably
keep the OS cursor pinned to the exact window centre every frame, especially inside the
Unity EDITOR's Game view (a long-documented Editor-vs-build inconsistency) — it can drift,
and once it has, every subsequent delta computed relative to a wrong "centre" inherits that
error, not just the first one. A single discarded frame does nothing against a persistent
drift.

**Fix (revised):** stopped trying to filter corrupted deltas after the fact and instead
prevent the drift from being possible at all — the standard pattern used by Unity
mouselook controllers that don't trust `CursorLockMode` alone. `AircraftInput.SetLocked`
now calls `Mouse.current.WarpCursorPosition` to the exact screen centre **synchronously,
the instant locking engages** (covers the position discontinuity from wherever the cursor
was resting during free/unlocked movement — before Play, or during a pause), and `Update()`
does the same **every single frame** thereafter, right after reading that frame's delta
(order matters: read first, warp after, since warping itself can generate a delta on some
backends and warping before the read would corrupt that frame's own sample). This
structurally cannot drift, regardless of what the Editor's own lock implementation does
under the hood — no guessing about exactly how many frames of corruption to suppress.
**Verified:** not by a human yet, but now backed by strong automated evidence gathered
solo (2026-08-22) — see below. `InputTestFixture` (used for AA-004/AA-007) genuinely can't
help here, since its virtual Mouse has no real OS cursor to mispin — but the user's own UTI
toolkit does, because it can *observe* the real cursor rather than needing to fake it.

**2026-08-22 solo evidence (real OS cursor, real Editor Game view, zero human input):**
Reinstalled UTI fresh (v0.2.2, see the dated log entry below for the full story) and built
`CursorDriftDiagnostic.cs` — a `BeanTracker.CustomCapture` adapter (same pattern as
`LoopDiagnostic`) that logs the REAL cursor position every frame via
`Mouse.current.position` (New Input System — this project's `activeInputHandler` is 1,
New-Input-System-only, so UTI's own `BeanMouseTracker`, which reads legacy
`Input.mousePosition`, can't be used directly here; see the script's header comment).

Two solo runs, CSV read directly off disk afterward:
- **Run 1 (33s, hands-off, nobody touching the mouse):** 10,801 samples. `deltaFromCenter`
  held at a constant ~0.5 px (a sub-pixel rounding artifact — screen height is odd, so exact
  centre falls on a half-pixel) for every sample but the very last one, which read a
  large offset that lines up exactly with the moment Play Mode was stopped (device state
  resets on exit) — not in-game drift.
- **Run 2 (extended, three deliberate real-cursor nudges of 170-360 px via
  `Mouse.WarpCursorPosition` from `execute_code`, simulating "the user moved the mouse"):
  11,277 in-game samples, max `deltaFromCenter` 1.8 px. None of the three nudges ever
  showed up as a recorded off-centre sample — the per-frame recentre in `AircraftInput`
  corrected each one faster than this telemetry could catch it uncorrected.

**What this does and doesn't prove, stated plainly rather than oversold:** this is real
evidence the warp-based fix mechanism actively holds the cursor at centre every frame in
this Editor Game view session, including recovering instantly from large synthetic
displacements — genuinely de-risks this bug. What it does NOT prove: that *real hardware*
mouse movement (raw HID deltas from an actual physical mouse) behaves identically to a
programmatic `WarpCursorPosition` call from the same API `AircraftInput` itself uses — the
original AA-006 root cause was specifically about `CursorLockMode`'s Editor-vs-build
inconsistency under real input, a different code path than what was exercised here. Left
this bug's status short of CLOSED for that reason; a human confirming "no restabilizing"
during real mouse movement is still the gold-standard close, but it's no longer the only
signal, and confidence in the fix is now high, not just "hoped."
**Guarded by:** no automated regression test yet (the diagnostic is a one-off CSV capture,
not a `[UnityTest]`) — worth converting into one if AA-006 resurfaces.

**2026-08-22, later same day — root cause actually found, from real hardware, not more
automation.** The solo evidence above was real but incomplete: it never reproduced the bug,
because it never involved a real, OS-focused hardware mouse device. Asked the user to
reproduce it three times for real (mouse resting outside the window, near-right, near-left
before pressing Play, untouched after) with `LoopDiagnostic` now also logging
`mouseDeltaX`/`mouseDeltaY`/`offNoseDeg` every tick from true frame 0 (extended for exactly
this purpose). All three runs show the **identical mechanism**:
- Ticks 0-1: mouse delta 0, aim dead on nose. Correct.
- **Tick 2 (0.04s after Play starts): exactly one burst of nonzero mouse delta**, then zero
  forever after (no drift, no repeat) — magnitude/direction tracking wherever the real
  cursor was resting before Play: (-43, 55) → 10.5 deg off nose when the mouse started
  outside the window; (457, -641) and (-451, -344) → **pegged at the full 55 deg aim cone**
  when it started inside the window (right and left respectively). Since the aim is
  deliberately built to never auto-recentre, that one bad reading becomes the new baseline
  until the player manually moves the mouse back — matching the user's own description
  exactly ("just happens at start... have to recenter to play normal") and their
  observation that it is NOT identical every run (it scales with real cursor position,
  contradicting their own earlier 3-corner test, which the user now attributes to a
  communication mismatch about what was actually being compared).

**Actual mechanism:** `SetLocked`'s warp is read-then-warp-safe for the SAME frame it fires
on, but nothing protected against the warp's own cursor movement surfacing in
`Mouse.current.delta` on a LATER frame — empirically, 2 frames later, consistently, across
all three runs. This is a real, different, more precise finding than either previous AA-006
theory (not a persistent "restabilizing" drift, not a same-frame kick) — a delayed one-time
echo of the lock-engage warp.

**Fix (third attempt, minimal change):** `AircraftInput` now tracks `settleFramesRemaining`,
set to 3 whenever `SetLocked(true)` engages, and forces `MouseDelta = Vector2.zero` in
`Update()` while it's counting down — long enough to clear the measured 2-tick echo with one
frame of margin, without changing the warp mechanism, the per-frame recentring, or anything
else. Compiles clean. **Not yet verified against real hardware** — awaiting the user's
retest with the same 3-corner method, this time checking whether `offNoseDeg` stays at/near
0 through tick 2 and beyond in the CSV.
**Lesson:** the earlier "strong solo automated evidence" entry above was real evidence of
something (the warp mechanism holds the cursor at centre in steady state), but not evidence
this bug was fixed — it never had the one ingredient (real hardware input) needed to
reproduce the actual failure. Said so plainly rather than let a passing automated test stand
in for a bug that specifically requires what automation couldn't provide.

**2026-08-22, same day — CLOSED.** User re-ran the same test (mouse in different spots
before Play, untouched after) four times and confirmed it "seemed good" — no more
spawn-direction kick. This is the bug's third fix attempt and the first one to survive real
retesting; see the log entry above for the fix (a 3-frame mouse-delta settle window after
the cursor lock engages) and the entry before that for the telemetry that actually found the
root cause.
**Lesson:** the user's pushback was correct and specific ("that's not it") — when a fix
that seemed theoretically sound doesn't survive contact with actual testing, the right
response is to trust the empirical result and re-derive from the user's description of the
*persistent* nature of the symptom, not to defend or extend the original theory. A "kick"
and a "drift" are different bugs even though both look like "aim starts wrong."

### AA-005 — Loop veered into an uncommanded bank instead of completing
**Status:** FIXED (unverified — awaiting Play Mode retest) · found 2026-08-18 by the user
testing Phase 3 · root-caused by inspection, cross-referenced against the user's own
description of the failure geometry

User: gained speed, held E to loop. AoA read ~0° (not stalled), but the aircraft "got stuck"
partway up — able to pitch from roughly level to roughly vertical, then diverting instead of
continuing over the top. Not the same bug as AA-004 (that fix was already in place and
roll/pitch commands were no longer being cancelled) — this is a second, independent issue
that AA-004's fix exposed rather than caused.

Root cause: the mouse aim's pitch is clamped to `±maxAimPitch` (80°) and never rotates with
the aircraft (`FlightControlLaw.StepAim`). A sustained manual pitch (the loop) carries the
nose well past that 80° cap. Once it does, the local target direction
(`transform.InverseTransformDirection(aim.DesiredDirection)`) ends up *behind* the aircraft
in its own frame. `horizontalError`'s forward-component floor exists precisely to handle
targets abeam or behind (see the comment in `FlightControlLaw.Compute`) — it saturates the
error toward ±90°, which is correct for "the target I'm chasing is now behind me, bank hard
to come around," but here there is no target to chase; it is a geometry artifact of the aim
cone being exceeded by a manual maneuver. That saturated bank term was still fully live
(nothing in AA-004's fix touched the mouse-driven *aileron* path when only pitch is held),
silently stealing part of the loop's rotation into an unwanted bank — which reads exactly
like "climbs fine, then visibly veers instead of continuing over the top." The ~80–90°
"stuck" point the user described lines up almost exactly with `maxAimPitch`, which is strong
corroborating evidence, not just a plausible story.

**Fix:** `AircraftPhysics.FixedUpdate` now sets `cmd.aileron = 0` whenever pitch is being
manually held and roll isn't — suppressing the mouse's aim-seeking bank while the player is
mid-maneuver on pitch alone, so a loop stays a loop instead of drifting into a bank. Manual
roll does **not** suppress pitch in return: pitch's cross-axis interference during a roll is
*damped* (`rollAlignmentFloor`, multiplies toward a floor of 0.35, never fully removed) not
*saturated* (jumps toward a hard ±90°/±80°), so it doesn't have this failure mode, and the
user already confirmed rolls feel fine as-is — left that path untouched rather than fix
something not reported broken.
**Consequence for gameplay, not a further bug:** completing a full loop still requires
enough speed to stay under the AoA limit throughout (§2.12 in `DESIGN.md`) — this fix
removes the artificial bank-veer, not the real stall model.
**Verified:** by inspection, cross-checked against the user's reported failure angle
matching `maxAimPitch` almost exactly. **Not yet confirmed in Play Mode.**
**Guarded by:** none yet. Worth a Phase 13 test —
`ManualPitchOnly_DoesNotIntroduceBank_WhenNoseExceedsAimCone`.
**Lesson:** a fix (AA-004) can be locally correct and still leave a neighboring bug exposed
— the aileron-cancellation bug was masking this one, since the aircraft never held a large
enough pitch error for long enough to reach the degenerate "target behind" geometry before
AA-004 was fixed. Retest broadly after any control-law fix, not just the specific case that
motivated it.

**Addendum, 2026-08-18 12:01 CDT:** user re-tested after this fix and reported the loop
*still* wouldn't complete ("can go directly up but it still will not flip over... seems
exactly the same as before"). Before assuming the fix failed, ran a full 3D closed-loop
simulation (proper orientation integration via Rodrigues rotation, not the earlier planar
approximation) of held-E at spawn speed. Result: **the fix is confirmed correct** — aileron
genuinely stays at exactly 0 throughout; the bank reading flipping to ±180° partway up is a
harmless coordinate artifact of pitching straight through vertical (nose-up-and-over with
level wings *looks like* a bank flip in any world-horizon-relative bank representation),
not an actual uncommanded roll. The real finding: **even a gentle 15–35% pull at 110%
throttle barely limps through a loop** — 10+ seconds, climbing from 400 m to 1,000+ m,
bleeding speed down to a near-stall crawl before it (maybe) completes. This is an energy
budget characteristic of the current thrust/lift/drag tuning, confirmed by simulation, not
a control-law bug — a real loop needs enough spare energy to survive the top, and the
current numbers put that right at the ragged edge. **Not something to patch blindly** —
belongs to the Phase 14 tuning pass, where changing `liftCoefficient`/`maxThrust` is a
deliberate feel decision, not a bug fix, and those numbers are already load-bearing for the
verified 300/100/3000 top-speed/stall/ceiling targets in `DESIGN.md` §2. Flagged for the
user rather than changed unilaterally.

### AA-004 — Manual roll/pitch stalled out partway through instead of completing rolls/loops
**Status:** CLOSED · found 2026-08-17 by the user
testing Phase 3 · root-caused by reading the code, not by simulation (this was a logic bug,
not a physics derivation error)

User reported: holding A or D reaches "a max peak and not turn any more," and holding E or
Q "limits to 20 AoA." Both looked like separate issues but share one root cause.

`AircraftPhysics.FixedUpdate` was adding keyboard input on top of the mouse-aim's computed
command, then re-clamping to +-1 — per the original design ("manual axes ADD to the
computed command," `BUILD_PLAN.md` 3.2). The problem: `FlightControlLaw.Compute` already
clamps its own aileron/elevator terms to +-1 *internally*, before the keyboard is added. The
mouse-aim's own bank/pitch-restoring command grows as the aircraft rotates away from
wherever the (stationary, if the mouse isn't moving) aim direction points, and once that
restoring term saturates at -1, adding the keyboard's +1 nets to `Clamp(-1 + 1, -1, 1) = 0`
— zero net command, rate decays to zero, rotation stops dead. The pitch "20 AoA" cap wasn't
the actual stall/AoA limiter (confirmed by reading `Aero.AoALimiter` — it never touches
keyboard input at all, only the mouse's own term); it was the same cancellation, landing
near 20 by coincidence because `pitchKp = 0.05` saturates the mouse's raw elevator term at
exactly 20 degrees of pitch error (documented in `AircraftConfig.cs`'s own tooltip).

**Fix:** keyboard input now **overrides** its axis instead of adding to a term that can
already be pinned at the opposite extreme. `cmd.aileron = input.RollAxis` outright when
roll is held (no limiter applies to roll, so this is a clean full override — holding D
always means full aileron, guaranteeing a complete roll). Pitch still routes the raw
keyboard axis through the same `ApplyAoALimiter` gate the mouse term uses (extracted into a
shared public method) — stall protection must stay uniform across input sources, or holding
E would let the player force a stall the mouse could never cause, contradicting DESIGN.md
§2.6's "cannot stall by pulling" promise.
**Consequence for gameplay, not a further bug:** a full loop now requires actual speed/
energy management to stay under the AoA limit through the maneuver, same as real War
Thunder Air Arcade — it will no longer flatly refuse to complete a loop, but holding E with
insufficient speed still won't loop cleanly. That's intended.
**Verified:** correct by inspection at the time — the override is an unconditional
assignment with no feedback path left that could cancel it. **Now also verified by an
automated Play Mode test** (2026-08-22):
`AA004_HeldRoll_StaysCommanded_NeverDecaysToZeroMidRoll` in
`Assets/AstroAces/Tests/PlayMode/Phase3ControlLawPlayModeTests.cs` holds D for 3 real
simulated seconds via `InputTestFixture`'s isolated virtual keyboard and asserts
`state.Rates.rollRight` never drops below 30 deg/s and accumulates over 300 deg of roll —
directly reproducing AA-004's exact failure signature (rate decaying to ~0, rotation
stopping dead) and confirming it doesn't happen. Passed (min rate stayed well clear of the
30 deg/s floor, full rotation accumulated in well under 3 s).
**Guarded by:** `AA004_HeldRoll_StaysCommanded_NeverDecaysToZeroMidRoll` — see above.
**Lesson:** "manual input adds to the computed command" sounds safe in isolation but breaks
the moment the computed term can itself saturate — check what a formula does at its clamp
boundaries, not just in the typical case.

### AA-003 — BUILD_PLAN.md log entry claimed CLAUDE.md was updated; it wasn't
**Status:** CLOSED · found and fixed 2026-08-17 17:09 CDT · found because the user asked
"have we updated CLAUDE.md" and I checked the actual file instead of trusting the log

The 17:02 CDT entry in `BUILD_PLAN.md`'s log said `CLAUDE.md` had been "updated to actually
list this doc in its tracked-docs and workflow sections." Reading `CLAUDE.md` directly off
disk showed no such change: it was byte-for-byte the same as session start, already listed
`BUILD_PLAN.md` in its Documentation Structure section from the beginning, and has no log
section of its own to have been edited in. The claimed action never happened.

**Impact:** low in this instance (the user asked before acting on it), but this is exactly
the failure mode that makes breadcrumb logs dangerous instead of useful — a future session
(or a cheaper executor model) reading that line would have reasonable grounds to believe
`CLAUDE.md` was already handled and skip checking it themselves.

**Fix:** removed the false claim from `BUILD_PLAN.md`'s log, replaced with this entry.
**Verified:** by direct `Read` of `CLAUDE.md` against what the log claimed, not by re-trusting
another log entry.
**Guarded by:** no automated guard possible for prose-log accuracy. The practical guard is
the one that caught it: **verify a log's claims against the actual file before repeating or
building on them**, especially when someone asks "did we do X" — check X, don't check the
note saying X was done.
**Lesson:** this is the same category as AA-001 and AA-002 — trusting a derived/summarized
claim (a hand-derivation, a physics endpoint, a log line) instead of the underlying ground
truth. See [[verify-derivations-numerically]] in memory; the pattern generalizes past
physics to documentation itself.

### AA-002 — Phase 1 test rig stall-dove immediately instead of settling near 267 mph
**Status:** CLOSED · found and fixed 2026-08-17 via first human Play Mode run · **found by
the user**, diagnosed by simulation before touching code, fix confirmed by a second Play
Mode run the same session (256.5 mph, 1,310 ft, AoA 1.5° — matches the simulation)

First real Play Mode test (run by the user, who is new to Unity — screenshots of the Game
view and the `Phase1DebugReadout` overlay) showed speed climbing past 96 mph while altitude
dropped continuously and AoA read **51.4°**, well past `postStallAoADeg` (45°), with no way
to recover since Phase 1 has no rotation control by design. This matched the exact failure
mode `BUILD_PLAN.md`'s own Phase 1 acceptance check warned about ("If it sinks or
accelerates without limit, stop and fix Phase 1"), so it needed a real diagnosis, not
reassurance that it was probably fine.

Reproduced with a throwaway Python simulation of the exact force model (`Aero.ThrustForce` +
`Aero.AerodynamicForce`, no rotation ever applied — orientation stays identity forever, as
Phase 1 intends): spawning at rest at 400 m, 80% throttle, level, the rig hits the ground at
**t = 14 s**. Root cause: lift needs airspeed² to mean anything, gravity needs none, and
with a fixed horizontal nose, the *only* way any AoA (and thus any lift) can exist is a
nonzero vertical velocity — i.e. the aircraft must physically be descending to generate any
lift at all. Starting from zero velocity, gravity gets a multi-second head start over lift
while speed builds from thrust alone: within ~1 s, AoA already exceeds 39°, well past
`criticalAoADeg`, before there is remotely enough airspeed for `postStallLift` (0.35) to
arrest the fall.

**This is a spawn-condition bug, not a flight-model bug.** Re-ran the same simulation
spawning at the analytic trim speed (`Aero.TopSpeedMps` at spawn altitude/throttle, ≈267
mph) instead of at rest: speed holds steady at 271–272 mph, AoA settles to 2.4–3.9°
(matching the 2.06° cruise AoA independently derived in `DESIGN.md` §2.3), and altitude
loss is a shallow, constant, non-divergent glide (~18.5 ft/s) — expected and correct for
"no pitch control yet," not a failure.

**Fix:** `AircraftPhysics.Awake()` now sets `rb.linearVelocity` to the analytic trim speed
before the first `FixedUpdate` runs, using `cfg.startThrottle` rather than
`engine.Throttle` deliberately — `AircraftEngine.Awake()` is what sets `Throttle` from
`cfg.startThrottle` in the first place, and `Awake()` order between components on one
GameObject is unspecified, so reading through `engine` here could race.
**Design consequence, not just a code patch:** `DESIGN.md` §9 now specifies every aircraft
spawns with velocity already set — nothing should ever spawn motionless in mid-air, player
or enemy.
**Verified:** by the two simulations above (`scratchpad/phase1_sim.py`,
`phase1_sim2.py` — not checked into the repo, throwaway). **Not yet verified in the actual
Editor** — the fix needs a second Play Mode pass from the user before this closes for real.
**Guarded by:** none yet. Worth a Phase 13 test once the test assembly exists —
`Spawn_InitialVelocity_MatchesAnalyticTrimSpeed`, and arguably `Spawn_AoA_StaysBelowCritical`
run for a few seconds of simulated `FixedUpdate`.
**Lesson:** I verified the flight model's *trimmed* endpoints exhaustively (§2 of
`DESIGN.md`) but never simulated the *dynamic path* a from-rest launch would actually take
before writing the Phase 1 acceptance criterion — same blind spot as AA-001, different
place it bit. See [[verify-derivations-numerically]] in memory: verify the trajectory, not
just the endpoint.

### AA-001 — Ceiling derivation was wrong by 1,100 ft
**Status:** CLOSED · found and fixed 2026-08-17 · *design defect, never shipped in code*

The first hand-derivation of `densityScaleHeight` assumed the flight ceiling was
lift-limited, and that top speed would be altitude-independent because thrust and drag both
scale with air density. It predicted a 2,978 ft ceiling at `densityScaleHeight = 480`.

Numerical simulation gave **1,837 ft**. The ceiling is actually **thrust-limited**: induced
drag is `m·g·tan(AoA)`, which does *not* fall off with density, while available thrust does.
As the aircraft climbs, the AoA needed to hold altitude rises, induced drag grows, and the
shrinking thrust budget runs out well before lift does.

**Fix:** `densityScaleHeight` 480 → 780; `Aero.TopSpeedMps` rewritten from a divergent
fixed-point iteration to a bisection, with the new `Aero.CanHoldLevelFlight` deciding
feasibility, so it returns 0 above the ceiling instead of a nonsense value (it was returning
6.3 mph).
**Verified:** ceiling 2,986 ft at 100% throttle, 3,232 ft at 110%.
**Guarded by:** `Ceiling_AtFullThrottle_Is3000Feet`, `LevelFlight_AboveCeiling_IsImpossible`
(planned, `TESTS.md`).
**Lesson:** hand-derive, then always verify numerically before committing a tuning value.

---

## Known risks — not yet bugs

Watch for these during Phase 1–14; promote to a numbered bug the moment one is observed.

| Risk | Symptom to watch for | First thing to check |
| --- | --- | --- |
| Rigidbody `linearDamping` left non-zero | Top speed below 300 mph with no explanation | `AircraftPhysics` Rigidbody setup |
| Mouse delta multiplied by `Time.deltaTime` | Aim crawls; sensitivity feels frame-rate dependent | `AircraftInput` |
| Angular-velocity sign inlined somewhere | Aircraft rolls away from the mouse | Search for `angularVelocity` outside `BodyRates` |
| Projectile uses a collider instead of a swept raycast | Rounds pass through the enemy at close range | `Projectile.FixedUpdate` |
| `Time.timeScale` not reset on restart | Game restarts frozen | `GameStateController.Restart` |
| Aim cone clamp not written back to stored angles | Aircraft chases a direction that is not on screen | `FlightControlLaw.StepAim` |
| Cloud spheres keep their colliders | Aircraft explodes on a cloud | `CloudField` |
| Minimap camera culling mask wrong | Minimap shows the whole world, or nothing | `Layers.MinimapMask` |
| Phase 2's placeholder camera rolls exactly with the aircraft (rigid parent, no independent orientation) | Screen flips upside-down during any maneuver that inverts the aircraft (e.g. a loop) — reads as "the crosshair is stuck/broken" even when the underlying aim direction is fine (verified, see AA-008) | Confirm the real `ChaseCamera` (Phase 4) doesn't just add smoothing but also decides how much roll to actually follow |
| Hand-written `.meta` GUID collides with one Unity later generates | A script component shows "Missing (Mono Script)" in the Inspector after the Editor is focused/refreshed | Compare the `guid:` in the script's `.meta` against what `Dogfight.unity` (or the prefab) references for that component — introduced Phase 1, see `HANDOFF.md` 2026-08-17 15:39 entry |

---

## Log

- **2026-08-17 15:10 CDT** — Created. Logged AA-001 (ceiling derivation error, found by
  numerical verification, fixed before any code shipped) and a known-risk table drawn from
  the environment constraints in `DESIGN.md` §11.

- **2026-08-17 15:39 CDT** — Phase 0–1 built (project setup, flight-forces-only test rig).
  No new confirmed bugs. Added one known risk specific to this session's approach:
  hand-written `.meta` GUIDs (necessary because I have no GUI control over the Unity Editor
  to let it generate them normally) could collide with a GUID Unity assigns on next focus.
  Not observed yet — first thing to check if a component ever shows "Missing (Mono Script)".

- **2026-08-17 16:02 CDT** — Logged and fixed AA-002 (Phase 1 rig stall-dove because it
  spawned at rest instead of at flying speed) — found by the user's first Play Mode test,
  diagnosed with a throwaway simulation before any code changed. Status is FIXED but
  unverified pending the user re-running Play Mode. Also swapped the placeholder ground's
  material off a Built-in-RP shader (rendered magenta under URP) onto a confirmed
  URP-compatible one already in the project.

- **2026-08-17 16:18 CDT** — AA-002 CLOSED — user's second Play Mode run matched the
  simulation (256.5 mph, 1,310 ft, AoA 1.5°). Two more cosmetic-only issues found and fixed
  in the same round: the first ground-material swap tiled badly (rainbow stripes) and the
  aircraft capsule had never been fixed at all (still the original broken magenta material)
  — both now use a new plain `PlaceholderGrey.mat`. Also temporarily parented the camera to
  the aircraft so Phase 2/3 testing is visible before `ChaseCamera` exists in Phase 4.

- **2026-08-17 17:09 CDT** — Logged and closed AA-003: a `BUILD_PLAN.md` log entry falsely
  claimed `CLAUDE.md` had been updated (it hadn't been, and didn't need to be — found by
  checking the actual file when the user asked about it, not by trusting the log). Also
  fixed an unrelated formatting bug found while in this file: the AA-001 and AA-002 section
  headers were each accidentally duplicated back-to-back, left over from an earlier edit.

- **2026-08-18 10:35 CDT** — Logged and fixed AA-004: manual roll/pitch couldn't complete a
  full roll or loop, found by the user during the first Phase 3 Play Mode test. Root cause
  was additive keyboard input fighting an already-saturated mouse-aim restoring command down
  to a net-zero result. Fixed by having keyboard input override its axis instead of adding
  to it, with pitch still routed through the shared AoA limiter so stall protection stays
  uniform across input sources. Not yet re-tested in Play Mode.

- **2026-08-18 11:37 CDT** — Same Play Mode round surfaced two more real issues, both logged
  and fixed. AA-005: a loop still didn't complete even after AA-004 — a second, independent
  bug where the mouse's own bank-seeking term saturates once a manual pitch carries the nose
  past the aim's 80° cap, silently diverting the loop into an uncommanded bank. AA-006: spawn
  direction was randomly kicked by wherever the mouse cursor happened to be on screen before
  Play started — a first-frame delta spike from the cursor-lock snap-to-centre. Also added
  `TempPauseToggle` (not a bug fix — a requested stopgap: Esc now freezes `Time.timeScale`
  and shows "PAUSED" so the simulation can be stopped without hunting for the Editor's own
  Stop button; Phase 11 replaces this with the real menu). None of the three re-tested in
  Play Mode yet.

- **2026-08-18 12:43 CDT** — Real telemetry read from the user's first UTI run (23s CSV,
  1167 samples). AA-005 **confirmed correct from live data**, not just simulation: `aileron`
  is exactly 0.00 for the entire manual-pitch climb. The loop finding is now precise: nose
  climbs continuously 0°→85° over ~4.5s, elevator authority (`aoaLimitFactor`) fades from
  1.0 to ~0.17 as AoA holds near the 20° critical limit, climb rate decays to imperceptible
  right around 85° (reads as "frozen," isn't literally zero), then reverses as airspeed
  keeps bleeding (305→144 mph across the attempt) and the aircraft falls back rather than
  continuing over. Confirmed even starting near top speed — "more starting speed" alone
  doesn't fix it, since speed bleeds off during the climb regardless. This needs a
  coordinated tuning change (AoA fade band and/or lift/thrust), not a quick single-value
  patch, since those constants are tied to the verified 300/100/3,000 targets — Phase 14
  territory, deferred rather than rushed. Also wired `TempPauseToggle` to stop/restart the
  `BeanTracker` on pause so Esc doubles as "capture a flight-path snapshot PNG" — requested
  so the user can share proof with the UTI tool's author.

- **2026-08-18 12:32 CDT** — User re-tested again: cursor drift and the pause fix both
  confirmed still broken by direct evidence (bank/AoA frozen the instant the nose reached
  vertical, no slow creep at all — contradicts the earlier simulation). Rather than
  re-guess, vendored the user's own `Unity-Testing-Inspector` toolkit
  (`Assets/AstroAces/ThirdParty/UTI`, github.com/DataFright/Unity-Testing-Inspector — copied
  source directly rather than via UPM git dependency, since the package targets Unity
  6000.5 and this project is 6000.3.21f1) and added `LoopDiagnostic.cs` to log real
  per-`FixedUpdate` telemetry (AoA, bank, pitch angle, elevator/aileron command, rates) to
  CSV under `<project root>/UTI/BeanLogs/`. Also added `StarfieldPlaceholder.cs` — a
  runtime-generated shell of bright distant reference spheres, re-centred on the camera each
  frame — since the user reported the sky being blank made it hard to judge motion at all.
  Next step: read the CSV directly after the user's next loop attempt instead of relying on
  a from-scratch simulation.

- **2026-08-22 (later session) — Solo-runnable Play Mode testing, no human input needed:
  built it, and closed AA-004 and AA-007 with it.** User pushed back hard on the prior
  session's claim that a Play Mode retest of AA-004/AA-006/AA-007 required a human at the
  keyboard, correctly pointing out that automated Play Mode tests without a human are
  standard practice elsewhere. First attempt (`InputSystem.QueueStateEvent` against the
  live `Keyboard.current` from inside `execute_code`) genuinely did not work: the queued
  "D held" state read back `True` for one instant, then reverted to `False` within ~2
  seconds with `BankAngle` still at 0.0 — the real OS keyboard backend keeps polling in the
  background and overwrites a synthetic state almost immediately. That's a real limitation
  of poking the live device, not a general limitation of Unity or MCP.

  Found the actual mechanism: the Input System ships `InputTestFixture`
  (`Unity.InputSystem.TestFramework.asmdef`, present in the installed package,
  `autoReferenced: false` so it must be referenced explicitly) — it fully severs the input
  system from real hardware for the duration of a test (fresh `InputTestRuntime`, devices
  added via `InputSystem.AddDevice<T>()` become genuinely isolated), which is exactly why a
  held key stays held for the whole test instead of getting fought by the real backend.
  Built `Assets/AstroAces/Tests/PlayMode/` (new asmdef `AstroAces.Tests.PlayMode`,
  referencing `AstroAces.Runtime` + `Unity.InputSystem.TestFramework`) with
  `Phase3ControlLawPlayModeTests.cs`: loads the real `Dogfight.unity` scene, finds the real
  test rig, and drives it with `Press`/`Release`/`Set` on virtual devices — this is a real
  Play Mode run of the real components, not a Python re-simulation.

  Ran via the MCP `run_tests`/`get_test_job` tools (mode `PlayMode`) — both tests passed,
  `AA004_HeldRoll_StaysCommanded_NeverDecaysToZeroMidRoll` taking 3.68s of real wall-clock
  time (matching its 3s `FixedUpdate` sampling window almost exactly, strong evidence it
  actually ran the full loop rather than short-circuiting) and
  `AA007_GamePaused_MouseMovement_DoesNotChangeAim` taking 0.33s. **AA-004 and AA-007 moved
  to CLOSED** on this evidence — see their entries above for what each test actually checks.

  **AA-006 stays open and un-closeable by this method, for a real reason, not laziness:**
  it's specifically about whether `CursorLockMode.Locked` pins the real OS cursor inside the
  Editor's Game view. `InputTestFixture` gives the test a fully virtual mouse device with no
  OS cursor behind it at all — there is no real mispinning behavior left to reproduce. This
  one still needs a human running Play Mode with an actual mouse. Said so explicitly rather
  than stretching the new capability to cover something it structurally can't.

  **Lesson, and a correction to record:** the prior session's blanket claim that "I cannot
  press Play myself" / all Play Mode retesting needs a human was too broad — it was true for
  simple state-injection against the live device, but not for input specifically, where a
  purpose-built isolation mechanism already exists in the installed package. Worth
  remembering for future phases (Phase 9 AI, Phase 13 itself): before declaring something
  needs a human, check whether the engine already ships a way to fake it properly, don't
  just retry the naive approach once and generalize from the failure.

- **2026-08-22 (same session, later) — Fresh UTI v0.2.2 install, then used it to gather real
  solo evidence for AA-006.** User asked specifically for UTI (not the InputTestFixture
  approach) for this one: fully deleted the vendored `Assets/AstroAces/ThirdParty/UTI` folder
  and reinstalled from `github.com/DataFright/Unity-Testing-Inspector` tag `v0.2.2` (backed up
  the old folder to scratch space first, out of caution, since this project has no version
  control). Confirmed via diff against the old vendored copy that only `BeanLogger.cs` and
  `BeanMouseTracker.cs` changed (two real bug fixes — BUG-05: `BeanMouseTracker` no longer
  throws every frame when Active Input Handling excludes the legacy Input Manager, degrades to
  one warning instead; BUG-06: `BeanLogger.OutputTargets`'s setter now actually re-opens with
  the new output format instead of silently no-op'ing), plus two new files (`AssemblyInfo.cs`,
  and `UTI.Runtime.asmdef` — the package now ships its own asmdef, so
  `AstroAces.Runtime.asmdef` needed `"UTI.Runtime"` added to its references). Deliberately
  preserved the OLD `.meta` GUIDs for all 14 pre-existing files (confirmed the old ones were
  Unity-autogenerated on first import, not copied from the repo, so a naive overwrite would
  have broken the `BeanTracker`/`BeanLogger`/`BeanSnapshotExporter` components already sitting
  on the test rig into "Missing (Mono Script)") — verified after the swap via
  `find_gameobjects`/the component resource that every one resolved cleanly with real
  property data, not missing.

  **This project's `activeInputHandler` is 1 (New-Input-System-only)**, under which UTI's own
  `BeanMouseTracker` (reads legacy `Input.mousePosition`) can't actually read the mouse — so
  wrote `CursorDriftDiagnostic.cs` instead, a small `BeanTracker.CustomCapture` adapter (same
  pattern `LoopDiagnostic` already uses) that logs the real cursor position via
  `Mouse.current.position` (New Input System) every frame, plus distance from screen centre.

  Ran two solo Play Mode sessions (probe wired up dynamically via `execute_code`, no scene
  edits): one 33s hands-off run (10,801 samples, `deltaFromCenter` pinned at ~0.5px the whole
  time bar the very last sample, which lines up with Play Mode's own device-reset on stop);
  one run injecting three real 170-360px cursor nudges via `Mouse.WarpCursorPosition` from
  `execute_code` to simulate "the user moved the mouse" (11,277 samples, max
  `deltaFromCenter` 1.8px — none of the three nudges ever registered as an off-centre sample,
  meaning the per-frame recentre in `AircraftInput` corrected each one faster than this
  telemetry could catch it uncorrected). Full detail in AA-006's entry above, including the
  honest limit of what a programmatic warp can and can't stand in for versus genuine hardware
  mouse input.

- **2026-08-22 (same session, later still) — Loop actually fixed (AA-005's tuning deferral
  resolved) and a new bug (AA-008, crosshair during a loop) found and closed in the same
  pass.** User disagreed with treating "loop takes forever and looks stuck" as acceptable
  and asked for a real fix. Simulated the exact control-law maths in Python before touching
  code: the unmodified physics genuinely completes a loop, just in ~28s with a 10+ second
  unplayable crawl in the middle. Added `AircraftConfig.elevatorStallFloor` (0.3) so elevator
  authority never fades all the way to 0 — cut simulated loop time to ~8-13s — without
  touching `liftCoefficient`/`dragCoefficient`/`safeAoADeg`/`criticalAoADeg`, so the verified
  300/100/3,000/2.06° headline numbers are byte-identical to before (re-verified numerically,
  not assumed). Full derivation in `DESIGN.md` Sec 2.6.

  User confirmed the loop now completes, immediately found a new, real problem: the debug aim
  marker gets left behind and vanishes during the loop. Logged as **AA-008**, root-caused
  (the mouse aim is deliberately world-fixed and never auto-centres, so it goes stale during
  any maneuver that doesn't touch the mouse), fixed (glue the aim to the nose while pitch is
  held), the first fix attempt immediately confirmed broken by the user (glued
  unconditionally, blocking real mouse input — "not intent"), fixed a second time (glue only
  on frames with no real mouse delta), verified by a new automated Play Mode test using
  `InputTestFixture` covering extreme/adversarial input. User then reported the crosshair
  still looked "stuck" specifically when upside-down — investigated with a dedicated 16s
  hands-off full-loop telemetry test and found the aim tracking is clean throughout (max 1.3°
  deviation, 0.0° at dead vertical) — the real cause is the Phase 2 placeholder camera rolling
  rigidly with the aircraft, flipping the whole screen during the inverted portion. **AA-008
  CLOSED** on that finding; the camera behavior itself is deferred to Phase 4 (already
  scheduled) rather than patched in the placeholder. Full detail across `BUGS.md` AA-008,
  `DESIGN.md` Sec 2.6's log, and the two new PlayMode tests in
  `Assets/AstroAces/Tests/PlayMode/`.

- **2026-08-18 12:01 CDT** — User re-tested all three and reported two genuine problems
  remaining, one of AA-005/AA-006 each: the loop still wouldn't complete, and AA-006's fix
  didn't work at all — plus a third issue in the pause feature (aim kept drifting while
  "paused"). Investigated all three properly rather than re-guessing:
  - **AA-006 revised** (fix attempt 1 confirmed failed by the user) — replaced one-frame
    delta suppression with active `WarpCursorPosition` recentering, both synchronously on
    lock and every frame while locked. Verified `Mouse.WarpCursorPosition` exists in this
    Input System version via the compile harness before committing to it.
  - **AA-005 addendum, not a new bug** — ran a full 3D closed-loop simulation (proper
    orientation integration, not the earlier planar approximation) and confirmed the AA-005
    fix genuinely works (aileron stays at exactly 0). The still-can't-complete-a-loop report
    is a real, separately-confirmed finding: current thrust/lift tuning puts a full loop
    right at the edge of the aircraft's energy budget — flagged for the user rather than
    silently changing load-bearing physics coefficients.
  - **AA-007 (new)** — pause only froze `FixedUpdate` via `Time.timeScale`, but `Update()`
    (where mouse/aim reading happens) ignores `timeScale` entirely, so the aim kept
    updating while "paused." Added an explicit `AircraftInput.GamePaused` gate.
  All three fixes await a fourth Play Mode pass.

- **2026-08-22 (same session, later still) — AA-010 logged and closed: free-look orbited its
  own fixed spot instead of the ship.** Immediate follow-up to AA-009, working autonomously
  (Phase 4/5/6 built solo, user checking in only on major issues). Redesigned
  `ChaseCamera.UpdateFollow()` to actually move the camera's position around the aircraft
  during free-look (previously only the look direction changed, position never left the
  fixed chase spot) and look back at the ship from wherever that orbit lands. New test
  `FreeLook_OrbitsAroundShip_NotJustRotatesInPlace` failed three times in a row before
  passing, but each failure was in the test's own setup, not the fix: the ship's ~114 m/s
  flight speed against an ~8.5m orbit radius made the target position/rotation swing wildly
  between irregular render frames (froze the rig's Rigidbody for the test to remove that
  confound), then a `Vector3.Lerp`-along-a-chord-dips-inside-the-sphere geometry artifact
  made the held-distance check fail right at the test's early-break point (added a short
  real-time settle window after the break). Full detail in BUGS.md AA-010. All 12 PlayMode
  tests pass.
