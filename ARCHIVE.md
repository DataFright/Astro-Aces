# Astro Aces — Archive

Overflow store for old log entries moved out of the day-to-day docs (starting with
`HANDOFF.md`) once they stop being current-state-relevant, so the source docs stay fast to
read. **This file is historical trace only** — it is never more authoritative than the
current summary in the doc an entry was moved from (`HANDOFF.md`'s status tables, `BUGS.md`'s
bug entries, `BUILD_PLAN.md`'s phase sections). Read it when you need the blow-by-blow of how
something was found, tried, and fixed — not to determine current state. Entries are moved
here verbatim, never edited or summarized, so nothing is lost.

## Contents

- [HANDOFF.md log archive](#handoffmd-log-archive) — 2026-08-17 through 2026-08-22, Phases 0–6

---

## HANDOFF.md log archive

> Moved here 2026-08-22 to trim `HANDOFF.md` down to current status. A short rollup replaced
> this in `HANDOFF.md`'s own Log section — see there for the pointer back.

- **2026-08-17 15:10 CDT** — Created. Recorded post-wipe status: flight core, config, layer
  contract, editor tooling, compile harness and 14-phase build plan complete; no gameplay
  code yet. Added onboarding order and the Phase 1 gate (267 mph before proceeding).

- **2026-08-17 15:33 CDT** — **Phase 0 complete.** Notes for whoever picks this up:
  - Tags/layers were written **directly into `ProjectSettings/TagManager.asset`** (text
    edit) rather than by clicking `Astro Aces > Setup Project` in the Editor, because I have
    no GUI control over the Unity window. Layers 6–10 = Aircraft/Projectile/Ground/
    MinimapIcon/Cloud, tag `Enemy` added. **Run `Astro Aces > Verify Project Setup` once you
    have the Editor in hand** to confirm Unity picked up the external edit (it watches
    `ProjectSettings` for changes, but hasn't been visually confirmed this session).
  - Shadow distance fix applied directly to `Assets/Settings/PC_RPAsset.asset`
    (`m_ShadowDistance: 400`) — this is the asset actually referenced by
    `GraphicsSettings.m_CustomRenderPipeline`, confirmed by GUID before editing, not a guess.
  - `PlayerAircraftConfig.asset` / `EnemyAircraftConfig.asset` created by hand-writing the
    ScriptableObject YAML with every field at its documented default (both currently
    identical — enemy tuning divergence is a later task). If either looks empty/broken when
    selected in the Editor, that means my field list drifted from `AircraftConfig.cs`;
    diff them field-by-field against the `[Tooltip]`-documented defaults in the script.
  - `Dogfight.unity` created by hand (Main Camera near the Phase-1 spawn point at
    (0, 401, −8), Directional Light, Global Volume using the existing
    `DefaultVolumeProfile.asset`, and a **placeholder** 5 km × 5 km scaled primitive Plane
    on the `Ground` layer with a MeshCollider — this is NOT `GroundBuilder` from Phase 6,
    just something to visually orient against and to eventually crash into). Registered as
    the sole Build Settings scene (replaced `SampleScene`). All internal `fileID`
    cross-references were validated by script (every `component:`, `m_GameObject`,
    `m_Father` reference resolves to a defined anchor) — but **this project has not been
    focused in the Editor since these edits landed**, so "Unity actually opens it without
    error" is not yet confirmed by a human. That's the first thing to check.

- **2026-08-17 15:39 CDT** — **Phase 1 complete: the aircraft can fly.** `AircraftState`,
  `AircraftEngine`, `AircraftPhysics` written and wired into a test rig
  (`PlayerAircraft (Phase1 Test Rig)`, GameObject id 500000) already sitting in
  `Dogfight.unity` at (0, 400, 0), level, 80% throttle, using `PlayerAircraftConfig.asset`.
  Deliberately **no rotation control yet** — Phase 1 is forces only (thrust, lift, drag,
  side drag), so the rig should fly dead straight. Rotation control gets added to the same
  `AircraftPhysics.FixedUpdate` in Phase 3.

  Design notes worth knowing:
  - `AircraftState.Refresh()` is called **explicitly** by `AircraftPhysics.FixedUpdate`,
    not from its own `FixedUpdate`. Unity does not guarantee MonoBehaviour callback order
    across components on one GameObject, and "state is one frame stale" is a nasty class of
    bug to chase — explicit ordering sidesteps it entirely. If you add a system that reads
    `AircraftState`, make sure it runs after `AircraftPhysics.FixedUpdate`, or better, feed
    it through the same explicit call chain.
  - `AircraftPhysics.Awake()` sets `rb.mass`, `useGravity`, `linearDamping = 0`,
    `angularDamping = 0`, interpolation and collision detection **from code**, overriding
    whatever the Rigidbody component shows in the Inspector. This is deliberate (see
    BUGS.md's stale-damping risk) — don't "helpfully" set those in the Inspector expecting
    them to stick against a different config asset; they get stomped by whichever
    `AircraftConfig` is wired to `cfg`.
  - Every script's `.meta` GUID was **hand-written**, not Unity-generated, because the
    Editor process (still running from earlier in the session) hadn't refreshed its asset
    watcher to notice the new files yet. If Unity later regenerates a conflicting GUID
    on first focus, component references in `Dogfight.unity` would break — this hasn't
    happened as of this note, but if a script suddenly shows "Missing (Mono Script)" in the
    Inspector, this is the first thing to suspect. Check the `.meta` file's `guid:` still
    matches what `Dogfight.unity` references for that component.

  **Added `Phase1DebugReadout.cs`** — a throwaway `OnGUI` overlay (top-left box) showing
  smoothed speed/mph, altitude/ft, AoA, throttle%. It exists because `AircraftState` and
  `AircraftEngine` expose data through auto-properties (`{ get; private set; }`), whose
  compiler-generated backing fields are **not serialized**, so nothing shows in the
  Inspector even in Debug mode. **Delete this file once Phase 5's real HUD exists.**

  ### How to run the first real test (I cannot press Play myself — no GUI control over
  the Unity window, only file edits and an offline compiler)

  1. Bring the Unity Editor window into focus. Open `Assets/AstroAces/Scenes/Dogfight.unity`
     if it isn't already the open scene.
  2. **First**, run `Astro Aces > Verify Project Setup` in the menu bar and check the
     Console — confirms the layer edits from Phase 0 actually landed. If it fails, run
     `Astro Aces > Setup Project` once, then Verify again.
  3. Press Play.
  4. Expect, within a few seconds: a box in the top-left reading **Speed (smoothed)
     converging on roughly 267 mph**, Altitude holding near 1,312 ft (400 m) — small
     drift up or down is fine since nothing is correcting altitude yet — AoA staying
     small and single-digit, Throttle steady at 80%.
  5. Let it run ~10–15 seconds so the smoothing window (60 frames) settles before judging
     the number.

  **If it doesn't look like that, paste me the Console output and what the readout showed**
  — that is exactly what `BUGS.md`'s "known risks" table is for; the top suspects in order
  are stray `linearDamping`, a missing `cfg` reference (shows as `NullReferenceException` in
  Console immediately on Play), or a `.meta` GUID mismatch turning a component into
  "Missing (Mono Script)" silently.

  **Next:** Phase 2 (intent — `AircraftInput` + `AircraftAimController`), then Phase 3 wires
  the control law into this same `AircraftPhysics.FixedUpdate`.

- **2026-08-17 16:02 CDT** — **First human Play Mode test happened — and caught a real bug
  before Phase 2 started.** The user (new to Unity, walked through opening the scene step
  by step) ran Play and the rig stall-dove: speed climbing, altitude dropping continuously,
  AoA hit 51.4° — exactly the failure `BUILD_PLAN.md`'s Phase 1 acceptance check warned
  about. Diagnosed with a throwaway Python simulation of the exact force model before
  touching any code (see AA-002 in `BUGS.md` for the full trace): the rig spawned at rest,
  and with a fixed horizontal nose, lift requires airspeed² while gravity requires none, so
  gravity gets a multi-second head start and AoA blows past stall before there's enough
  speed for lift to matter. **Not a flight-model bug** — re-simulated spawning at the
  analytic trim speed instead and it holds together (271–272 mph, AoA settles to 2–4°,
  shallow non-divergent glide, matching the independently-derived 2.06° cruise AoA).

  **Fixed:** `AircraftPhysics.Awake()` now sets `rb.linearVelocity` to
  `Aero.TopSpeedMps(cfg, spawnDensity, cfg.startThrottle)` before the first physics step.
  `DESIGN.md` §9 updated — this is now a permanent design rule (no aircraft spawns at rest),
  not a one-off test-scene patch. **Also fixed:** the ground placeholder was rendering hot
  magenta — that's Unity's "shader incompatible with the active pipeline" color, not an
  error; `SimpleNaturePack_Texture_01.mat` uses a Built-in-RP shader and this project runs
  URP. Swapped it to `BTM_Assets/.../BaseMaterial.mat` (confirmed URP-compatible by
  checking its shader guid against materials that already render correctly, e.g.
  `Omega_fighterG/Materials/Body.mat`) — expect plain white/grey now, not magenta.

  **This fix has not been re-verified in the Editor yet — that's the immediate next step,
  not Phase 2.** Re-run the same test:
  1. Press Play in `Dogfight.unity` (should already be open).
  2. Expect: ground no longer magenta. Speed should climb toward and hold near
     **270–272 mph** (not 267 exactly — the no-cos-projection in the raw force model vs.
     the analytic solver used for the spawn speed accounts for the small difference, and
     that gap is expected, not a new bug). Altitude should decrease **slowly and steadily**
     (not accelerating, not oscillating) — a shallow glide, since nothing holds altitude
     without pitch control yet. AoA should stay in single digits, not spike toward 50°+.
  3. If that matches — Phase 1 is genuinely done and Phase 2 (`AircraftInput` +
     `AircraftAimController`) is next.
  4. If it still dives, or does something new and different, paste the Console output and
     what the readout shows — same as before.

- **2026-08-17 16:18 CDT** — Second round of user testing (still same Play Mode session as
  the 16:02 entry). **Spawn-velocity fix confirmed working**: 256.5 mph, altitude 1,310 ft
  (spawned at 1,312 ft), AoA 1.5° — matches the simulation almost exactly. AA-002 can move
  to CLOSED once someone updates its status line.

  Two more cosmetic issues turned up, both fixed, neither a physics problem:
  - **Ground still looked wrong (rainbow diagonal stripes, not magenta).** The material I
    swapped it to (`BTM_Assets/.../BaseMaterial.mat`) is URP-compatible but carries a
    texture meant for gem items, which tiles badly across a 500×-scaled plane. Replaced
    with a purpose-made **`Assets/AstroAces/Materials/PlaceholderGrey.mat`** — same URP/Lit
    shader (guid `933532a4fcc9baf4fa0491de14d08ed7`, confirmed working elsewhere in the
    project), flat grey, no texture at all. Used for **both** the ground and the aircraft
    capsule now — the capsule was still showing magenta too, because I'd only patched the
    ground's `MeshRenderer` material earlier and missed the aircraft's identical reference.
    If anything else still shows magenta later, same root cause: find its `MeshRenderer`'s
    material guid and check the shader it points to.
  - **Camera didn't follow — aircraft flew out of frame.** Expected; `ChaseCamera` is Phase
    4. But testing Phase 2/3 (mouse-driven turning) visually is basically impossible if the
    aircraft leaves frame in a few seconds, so I **reparented Main Camera's Transform under
    the aircraft's Transform** (local offset 3m up, 8m back — the same numbers planned for
    the real chase camera in `DESIGN.md` §5) as a rigid, non-smoothed stand-in. **This is
    scene wiring, not a script — `ChaseCamera.cs` does not exist yet.** Phase 4 should
    un-parent the camera and drive it properly (smoothing, free-look, zoom) rather than
    assume this parenting is the real implementation.

  **Status: Phase 1 is genuinely done.** Next real step is Phase 2 (`AircraftInput` +
  `AircraftAimController`) — no more open questions on the physics layer.

- **2026-08-17 16:31 CDT** — **Phase 1 confirmed closed.** User ran several fresh Play
  sessions (~20 s each): speed consistently 258–270 mph, altitude a slow steady drift down
  (395m → 273m in one run, 400m → 338m in another — same shallow-glide behavior each time,
  just different elapsed points), AoA holding 3–4°. Ground/aircraft render plain grey,
  camera rides along. Matches the fixed model, not a fluke. Moving on to **Phase 2**.

  One more confirmation while we were in there: Unity's own asset pipeline re-saved
  `PlaceholderGrey.mat` (added the standard URP `AssetVersion` sub-asset, normalized a
  couple of fields) — meaning the Editor **is** actively watching and reimporting the
  `Assets` folder on focus now, same as any normal Unity project. The earlier concern about
  hand-written `.meta` GUIDs possibly colliding with ones Unity generates later is lower
  risk than it looked at 15:39 — the editor has clearly refreshed multiple times since
  without incident. Still worth knowing the mechanism if something ever does go sideways
  with a script reference.

- **2026-08-17 16:45 CDT** — **Phase 2 built** (intent layer): `AircraftInput` reads mouse
  + keyboard (New Input System, null-guarded), `AircraftAimController` maintains the
  persistent `DesiredDirection` via `FlightControlLaw.StepAim`. `AircraftEngine` now also
  drives itself from `AircraftInput` (W/S/scroll/F), reusing the mutator methods it already
  had — no new component needed for that. All three new scripts are on the test rig in
  `Dogfight.unity` now (`AircraftInput`, `AircraftAimController`, `Phase2DebugReadout`).

  **The aircraft will NOT visibly turn yet** — that's Phase 3, which hasn't been wired into
  `AircraftPhysics.FixedUpdate` yet. What Phase 2 adds is only the *intent* layer: moving
  the mouse changes `DesiredDirection`, but nothing currently reads it to rotate anything.
  Added a throwaway `Phase2DebugReadout` (top-right box) showing the aim-off-nose angle and
  a small on-screen marker so this is actually checkable before the real crosshair exists.

  **Important — the cursor will lock and hide as soon as you press Play now**, because
  `AircraftInput.OnEnable()` calls `Cursor.lockState = CursorLockMode.Locked`. This is
  correct for mouse-look and expected, not a bug. If you need your cursor back, **press
  Escape** — Unity's Editor always force-releases a locked cursor on Escape regardless of
  game logic, so there's no way to actually get stuck, even with no pause menu yet.

  **How to test Phase 2:**
  1. Press Play.
  2. Move the mouse — the marker in the top-right debug box should move, and the "Aim off
     nose" number should change.
  3. Stop moving the mouse — the marker should **hold exactly still**, not drift back to
     center. This is the "never auto-centres" behavior from `DESIGN.md` — if it snaps back,
     something is wrong with `FlightControlLaw.StepAim`.
  4. Move the mouse hard and fast in one direction and hold it there — "Aim off nose"
     should climb and then **stop at 55**, never exceed it.
  5. Try W/S (throttle should change in the Phase 1 debug box), scroll wheel (throttle
     jumps in 5% steps), F (should toggle — no visible readout for airbrake state yet,
     that's Phase 5's message log, but it shouldn't error).
  6. The aircraft itself will keep flying in a straight line exactly like Phase 1 — that's
     expected, not a failure. Phase 3 is what makes it actually turn.

  If anything errors in the Console or behaves differently than steps 2–5 describe, paste
  it here same as before.

- **2026-08-17 17:00 CDT** — **Phase 2 confirmed clean** — user ran the test steps above,
  no issues found (marker tracked the mouse, held still when the mouse stopped, capped at
  55°, throttle responded to input). **Moving to Phase 3** (control law wired into
  `AircraftPhysics.FixedUpdate` — first phase where the aircraft actually turns).

- **2026-08-17 17:02 CDT** — **Phase 3 built: the aircraft should actually turn now.**
  `AircraftPhysics.FixedUpdate` now computes `FlightControlLaw.Compute` from
  `aim.DesiredDirection`, adds keyboard roll/pitch on top, and applies the resulting rates
  to `rb.angularVelocity` — skipped entirely when `!state.IsAlive` so future wreckage
  tumbles freely instead of fighting rate control.

  **Before handing this off, I ran a full closed-loop simulation** (not just the pure-math
  unit checks `Aero`/`FlightControlLaw` already had individually) — spawned level, aim fixed
  30° right of the nose, stepped forces + control law + rotation together for 20 simulated
  seconds. Result: banks to ~47° within a second, AoA briefly touches 17° (the limiter
  engaged correctly — critical is 20°), then **converges smoothly and monotonically** to
  under 1° off target by 19s, no oscillation, speed and altitude stay in a sane band
  throughout. This combination (`Aero` forces + `FlightControlLaw` + rate integration all
  together) had never been exercised before — worth checking given AA-002 was exactly this
  category of bug: each piece fine alone, the combination not simulated first.

  **How to test Phase 3 (this is the first genuinely fun one):**
  1. Press Play.
  2. Move the mouse — **the aircraft should now actually bank and turn** toward wherever
     you point it, not just the debug marker from Phase 2.
  3. Point the mouse somewhere and hold it — the turn should smooth out and the aircraft
     should settle roughly toward that heading, not endlessly oscillate or overshoot back
     and forth.
  4. Try A and D — each should produce a clean, controllable roll (hold one down through a
     full rotation for an aileron roll).
  5. Try E and Q — nose should pitch up/down on top of whatever the mouse-driven turn is
     doing.
  6. Watch the Phase 1 debug box while doing hard turns — AoA might spike briefly during a
     sharp turn but should **not** get stuck pegged near 20°+ or keep climbing; if it does,
     that's the stall limiter not engaging correctly.
  7. If the aircraft **wobbles** (oscillates back and forth instead of settling), or
     **snaps** unrealistically fast, or **spins out** — stop and describe exactly what it
     looked like, ideally with a screen recording or a couple of screenshots a second apart,
     since "wobble" and "overshoot" look different and point at different fixes
     (`DESIGN.md` §3 has the triage table I'd use).

  Camera is still the rigid Phase-2 parenting (no smoothing) — it'll swing around abruptly
  with the aircraft rather than smoothly trailing it. That's expected until Phase 4.

- **2026-08-17 17:09 CDT** — **Correction, and a lesson worth keeping.** The 17:00 entry
  above originally claimed `CLAUDE.md` had been updated to list `BUILD_PLAN.md`. The user
  asked "have we updated CLAUDE.md on this?" — I read the actual file on disk instead of
  trusting the log, and the claim was false: `CLAUDE.md` was never touched this session,
  and never needed to be, since it already listed `BUILD_PLAN.md` from the start. Removed
  the false line. Also found (while already in this file) that this same note had briefly
  re-logged the Phase 2 confirmation a second time, redundant with the 17:00 entry above it
  — removed the duplicate rather than leave two entries claiming the same thing at different
  timestamps. Logged as AA-003 in `BUGS.md`. **Rule going forward: when asked "did we do X,"
  check X directly — don't check whether a log says X was done.**

- **2026-08-18 10:35 CDT** — **First real bug in the control law itself, found by the user
  flying it.** Mouse-driven turning worked perfectly. Manual roll/pitch didn't: holding A/D
  reached "a max peak and not turn any more," holding E/Q "limits to 20 AoA." Traced to one
  root cause in `AircraftPhysics.FixedUpdate`: keyboard input was ADDING to a command that
  `FlightControlLaw.Compute` already internally clamps to ±1 — once the mouse-aim's own
  restoring term saturates at -1 (which happens automatically as the aircraft rotates away
  from wherever the mouse is currently pointing), adding the keyboard's +1 nets to exactly
  zero. Rotation stops dead, every time, at a predictable point — which is exactly what got
  reported as "a max peak." The pitch "20° AoA limit" wasn't the real stall/AoA limiter at
  all (confirmed by reading `Aero.AoALimiter` — it never touches keyboard input); it was
  the identical cancellation, landing near 20 only because `pitchKp = 0.05` happens to
  saturate the mouse's own term at exactly 20° of pitch error.

  **Fixed:** keyboard input now overrides its axis outright instead of adding to it —
  `cmd.aileron = input.RollAxis` when roll is held, full stop, so a held D always produces
  a complete roll regardless of bank angle. Pitch still runs through the same AoA limiter
  the mouse uses (pulled out into a shared `FlightControlLaw.ApplyAoALimiter` method), so
  stall protection stays uniform across input sources rather than becoming a mouse-only
  guarantee. Full details and the exact math in `BUGS.md` AA-004.

  **This one didn't need a numerical simulation to fix** — it was a logic bug findable by
  reading the code (what happens at the clamp boundary), not a derived-value error. Said
  that explicitly because the last two bugs both got caught by simulation and I don't want
  "always simulate" to harden into "never just read the code carefully."

  **What to expect now, re-testing Phase 3:**
  - Holding D (or A) should let the aircraft **complete a full continuous roll** — hold it
    through a full rotation and it should keep going, not stop at some angle and require
    release-then-repress to continue.
  - Holding E should let AoA **climb past 20°** now (there's no artificial 20° wall), but a
    full loop will still need enough speed — if the aircraft is slow, expect it to mush
    through the top rather than complete a clean loop. That's the real stall/AoA limiter
    working as designed, not a bug. If you have plenty of speed and it *still* won't loop,
    that's worth reporting.
  - Mouse-driven turning should behave exactly like before (that part already worked).

- **2026-08-18 11:37 CDT** — **Same testing round, three more things — two real bugs, one
  requested feature.** All fixed, none re-tested yet.

  **AA-005 — the loop still didn't work even with AA-004's fix in place.** User: gained
  speed, held E, got AoA to ~0° (not stalled) but "got stuck" partway up — could pitch from
  roughly level to roughly vertical, then it diverted instead of continuing over the top.
  Different bug from AA-004, exposed by AA-004's fix rather than caused by it: mouse aim
  pitch is capped at ±80° (`maxAimPitch`) and never rotates with the aircraft. A sustained
  manual pitch carries the nose past that cap, which puts the local target *behind* the
  aircraft in its own frame — and the mouse's own bank-seeking term is built to saturate
  hard toward max in exactly that situation (correct for "the thing I'm chasing is now
  behind me, turn around," which is not what's happening here). That saturated bank was
  still fully live, silently stealing part of the loop's rotation into an uncommanded
  bank — matches the report almost exactly, and the "stuck" point lining up with 80° is
  strong corroborating evidence, not a guess. **Fixed:** suppress the mouse-driven aileron
  entirely while pitch is manually held and roll isn't. Roll doesn't need the same
  treatment in reverse — its cross-axis effect on pitch is damped, not saturated, and rolls
  were already confirmed working.

  **AA-006 — spawn direction was random depending on where the mouse was on screen.** User:
  "if my mouse is not in the perfect direction I will start the game flying in just like it
  will go in a crazy direction." Root cause: `AircraftInput` locks the cursor on enable,
  which snaps the OS pointer to the window centre — and the very next `Mouse.delta` sample
  can report that snap distance as if it were real movement. Size and direction of the kick
  depend entirely on wherever the cursor was sitting before Play started, matching the
  report exactly. **Fixed:** cursor locking is now centralized through a new
  `AircraftInput.SetLocked(bool)` method that discards one delta sample on every lock
  transition — this matters beyond just Play-start, since anything that unlocks and
  re-locks the cursor later (the pause toggle below, free-look in Phase 4, a future
  death/respawn) would reintroduce the identical kick if it touched `Cursor.lockState`
  directly instead of going through `SetLocked`.

  **Requested: a way to stop the game without hunting for the Editor's Stop button.** Added
  `TempPauseToggle` — Esc now freezes `Time.timeScale` and shows a plain "PAUSED" box, cursor
  released through the same `SetLocked` path. This is **not** Phase 11's real pause menu —
  no restart, no quit, no death state. Delete it when Phase 11 is actually built; don't grow
  it into the real thing.

  **How to test all three:**
  1. Press Play **several times in a row**, moving your mouse to a different spot on screen
     before each one. The aircraft should fly straight ahead every time, not veer off in a
     different direction depending on where the cursor was.
  2. Gain speed, pull up into a loop (hold E). It should now be able to go all the way over
     the top given enough speed, without visibly veering/banking sideways partway up. If
     you're going slow, mushing through the top instead of completing cleanly is expected
     stall behavior, not a bug — only report it if you have plenty of speed and it *still*
     won't come over the top, or if it visibly banks/veers rather than just losing energy.
  3. Press Esc — game should freeze, cursor should reappear, a "PAUSED" box should show.
     Press Esc again — should resume exactly where it left off, aim should not jump/kick on
     resume (that's the same `SetLocked` fix from #1, applied to resuming too).

- **2026-08-18 12:01 CDT** — **User re-tested and found two of the three still broken.**
  Investigated properly rather than re-guessing at either.

  **AA-006, spawn direction — confirmed still broken, root-caused differently.** The user's
  own description was the key: "if the aim cursor is 50px off from the mouse location, then
  snapping the mouse to center snaps that aim [error] too... I have to keep restabilizing."
  That's not a one-frame startup kick (which the first fix targeted) — it's a *persistent*
  offset, meaning `CursorLockMode.Locked` isn't reliably keeping the OS cursor pinned to
  centre every frame, especially inside the Editor's Game view (a known Editor-vs-build
  inconsistency). **Fixed properly this time:** `AircraftInput` now actively
  `Mouse.WarpCursorPosition`s the cursor back to the exact centre itself — once
  synchronously the instant locking engages (covers the jump from wherever the cursor was
  resting before Play, or during a pause), and again every single frame while locked (read
  delta first, warp after, so the warp itself doesn't corrupt that frame's reading). This
  doesn't rely on Unity's lock implementation behaving any particular way — it's
  self-correcting regardless.

  **Loop still wouldn't complete — but this time simulation showed the fix IS correct.**
  User: "I can go directly up but it still will not flip over... seems exactly the same as
  before." Ran a full 3D simulation (proper orientation tracking, not the earlier simplified
  version) of holding E from spawn. Result: aileron genuinely stays at exactly 0 the whole
  time — AA-005's fix works. What actually happens: pulling up drives AoA past 90° within
  about 2 seconds (confirmed even with a gentle 15–35% pull, not just full E), which
  triggers the real stall/AoA limiter and kills further elevator authority — the aircraft
  gets stuck nose-up-and-over, upside down, slowly losing altitude, unable to complete the
  rotation. Even easing off the stick barely helps: a gentle pull at max throttle still takes
  10+ seconds, climbs from 400 m past 1,000 m, and bleeds speed down to a near-stall crawl
  before (maybe) finishing. **This means: completing a full loop is right at the edge of
  what the current aircraft can do, tuning-wise** — not a leftover bug. I didn't change any
  physics coefficients to "fix" this, since `liftCoefficient`/`maxThrust` are load-bearing
  for the already-verified 300 mph / 100 mph / 3,000 ft targets — that's a genuine feel
  decision for you to make (Phase 14 territory), not something to quietly change. Options if
  you want loops to feel easier: raise `liftCoefficient` a bit (more lift = tighter
  sustainable turns without stalling), or raise `maxThrust` (more spare energy to survive the
  climb) — say the word and I'll do it, or we can leave it as an intentionally hard maneuver
  for now and revisit properly in the tuning pass.

  **AA-007 (new) — pause wasn't actually freezing anything except the aircraft's physics.**
  User: "the aim cursor still phantomly follows even though game is paused." Correct
  diagnosis on their part — `Time.timeScale = 0` stops `FixedUpdate` (so the plane visibly
  stops) but does **nothing** to `Update()`, where mouse reading happens, so the aim kept
  drifting from residual mouse movement the whole time paused. Added
  `AircraftInput.GamePaused`, set by `TempPauseToggle` alongside `Time.timeScale` — while
  true, every `AircraftInput` property reports neutral and no device gets read at all.

  **How to test all three, again:**
  1. Same as before — Play repeatedly with the mouse in different spots first. Should fly
     straight every time now. If it's *still* inconsistent, that's a real problem I need
     more detail on (does it drift gradually during flight too, or only at the very start?).
  2. Loop: don't expect a clean, quick loop right now unless you ask for the tuning change
     above — expect it to take a long time, climb a lot, and possibly not quite make it back
     around, especially with a full hard pull. That's the honest current state, not a bug to
     chase further.
  3. Pause: the aim marker in the debug box should now hold **completely still** the moment
     you press Esc, not just the aircraft.

- **2026-08-18 12:32 CDT — Stopped guessing, added real instrumentation.** User re-tested:
  cursor drift confirmed still broken, and the loop report got much more specific — full
  speed, held E, nose reaches straight up and **freezes there completely** (angle doesn't
  change at all, just loses energy and stalls in place). That directly contradicts the
  earlier simulation, which showed slow-but-nonzero progress. Simulation vs. reality
  disagree, so simulation is no longer trustworthy for this — need to see what the real
  Unity physics actually does.

  **Added real telemetry.** User provided their own tool,
  github.com/DataFright/Unity-Testing-Inspector — vendored its source into
  `Assets/AstroAces/ThirdParty/UTI` (copied directly rather than via UPM, since the package
  targets a newer Unity than this project uses) and added `LoopDiagnostic.cs`, which logs
  AoA/bank/pitch angle/elevator/aileron/rates every physics step to a CSV. **These are
  already wired onto the test rig — nothing to add in the Editor.** CSV lands at
  `<project folder>/UTI/BeanLogs/*.csv` after any Play session; I read it directly, no
  export step needed from you.

  **Also added:** `StarfieldPlaceholder.cs` — distant bright reference spheres around the
  camera (re-centred every frame, no parallax) — requested because the blank sky made it
  hard to judge whether anything was actually moving while flying. Real sky art is still
  Phase 6.

  **Next test:** same as before (repeat spawn direction, then a full-throttle loop attempt),
  then just tell me it's done — I'll read the CSV myself rather than needing a description.

- **2026-08-18 12:43 CDT — Read the CSV, got a precise answer.** `aileron` was exactly 0.00
  for the entire manual-pitch climb — AA-005 is genuinely correct, confirmed from real
  physics, not simulation. The loop: climbs continuously 0°→85° over ~4.5s, elevator
  authority fades out as AoA holds near the 20° limit, the climb rate decays to
  imperceptible right around vertical (not literally frozen, just far too slow to notice),
  then reverses as speed keeps dropping (305→144 mph across the run) and it falls back
  instead of continuing over. Even near-top-speed entry doesn't fix it — speed bleeds off
  during the climb regardless of the starting number. This needs a coordinated tuning
  change across a few interlinked constants (not a one-line patch, since they're tied to
  the verified 300/100/3,000 targets) — staying deferred to Phase 14 rather than rushed.

  User also asked for a shareable screenshot of a UTI run for the tool's author. Wired
  `TempPauseToggle` to stop/restart the `BeanTracker` on pause, so **pressing Esc now also
  captures a PNG** of the flight path via `BeanSnapshotExporter` (already on the test rig).
  PNG lands at `<project root>/UTI/BeanSnapshots/`.

  **How to get the screenshot:** fly around a bit, press Esc. Check
  `UTI/BeanSnapshots/` for a new PNG.

  **Housekeeping reminder for whoever builds Phase 4 and Phase 6 — two deliberate
  placeholders are sitting in `Dogfight.unity` and need to be replaced, not built around:**
  - **Camera parenting.** Main Camera is a rigid child of the aircraft's Transform (offset
    3m up, 8m back) so Phase 2/3 testing is watchable. This is NOT `ChaseCamera` — there is
    no smoothing, no free-look, no zoom. Phase 4 should un-parent it and drive it from a
    real component.
  - **Ground and materials.** The "Ground (Placeholder)" object is a scaled primitive Plane
    with a MeshCollider, not `GroundBuilder`'s procedural terrain. Both it and the aircraft
    capsule use `Assets/AstroAces/Materials/PlaceholderGrey.mat` — flat grey, no texture,
    chosen only because it doesn't render magenta. Phase 6 replaces the ground entirely and
    Phase 9's toon shader replaces this material on both. The aircraft capsule itself is a
    built-in primitive standing in for `Omega_fighterG/Meshes/fighter_black.FBX`, which
    Phase 9 swaps in along with the grey/green player recolor.

- **2026-08-21 14:01 CDT — Unity MCP connected.** User wanted a live MCP bridge to Unity
  (console logs, scene state, Play Mode control) after finding the manual
  screenshot-back-and-forth painful — remembered using one via a Unity AI Assistant trial
  that has since expired. Researched free options and went with
  [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp) (MIT license, no Unity
  account needed, actively maintained) over Unity's own official MCP server (also free, but
  bundled inside the same paid-credits "Unity AI Assistant" package that expired before).

  **Done:** added `com.coplaydev.unity-mcp` (pinned `v10.1.2`) to `Packages/manifest.json`,
  and registered it with Claude Code: `astro-aces-unity-mcp` → `http://localhost:8080/mcp`,
  local scope (this project only). Found and left alone a **pre-existing, already-broken**
  `unity-mcp` entry at user scope (`C:\Users\sirsw\.unity\relay\relay_win.exe`) — almost
  certainly the dead leftover from that earlier trial, affects all projects not just this
  one, so didn't touch it without asking first.

  **Not live yet** — Unity hasn't resolved the new package (needs the Editor focused, and
  internet access, to pull it from GitHub). Once it has, `Window → MCP for Unity` in the
  Editor should show the bridge running on port 8080. `claude mcp list` currently shows
  `astro-aces-unity-mcp` as connection-refused, which is expected until then — not an error.
  **A new Claude Code session will likely be needed** to actually pick up and use the tools
  once the server is live (MCP servers are normally loaded at session start).

- **2026-08-21 14:10 CDT — Package resolved; one manual click left.** Unity pulled
  `com.coplaydev.unity-mcp` cleanly (no compile errors) and its own first-run setup wizard
  ran automatically, registering Claude Code itself as `UnityMCP` →
  `http://127.0.0.1:8080/mcp` (correct loopback form — their own code notes `localhost` can
  resolve to IPv6 first on Windows and miss the server). Removed my earlier manual
  `astro-aces-unity-mcp` registration since Unity's own is the better, self-maintaining one
  — don't re-add it.

  **Still not connecting** — traced to `HttpAutoStartHandler.cs`: the local HTTP bridge only
  starts automatically if "Auto-Start on Editor Load" is enabled in Advanced Settings
  (off by default), or if "Start Server" is clicked once. That preference is stored in the
  Windows Registry under a Unity-internal per-project hash I can't safely reverse-engineer
  and hand-write — this is a genuine one-click boundary, not something worth a risky
  workaround.

  **Action needed (one-time):** in Unity, `Window → MCP for Unity` → either click
  **Start Server**, or open Advanced Settings and enable **Auto-Start on Editor Load** (does
  it automatically from then on). After that, `claude mcp list` should show `UnityMCP` as
  connected, and a new Claude Code session will pick up its tools (scene control, console
  access, script editing, running tests).

  **The old dead `unity-mcp` entry** (`.unity/relay/relay_win.exe`, user-wide scope) was left
  alone as instructed — it doesn't conflict with the new `UnityMCP` entry, just clutter. Say
  the word if you want it removed (`claude mcp remove unity-mcp -s user`).

- **2026-08-21 14:52 CDT — Confirmed working, fully closed out.** User enabled auto-start
  and started the server from `Window > MCP for Unity`; health check green in the Editor,
  and `claude mcp list` independently confirms `UnityMCP: Connected` from Claude Code's own
  side. Removed the dead `unity-mcp` leftover from the old expired trial (user approved —
  "if it's clutter it can be removed"). Verified via `ToolSearch` that this session's own
  tool list doesn't include Unity MCP's tools yet, confirming they load at session start,
  not live — nothing more to do, just start a fresh session to use them.

- **2026-08-21 (later session) — Unity MCP tools confirmed live and correct, not just
  connected.** Fresh session, per the "health-check before trusting it" instruction above:
  loaded `read_console`/`manage_editor`/`manage_scene` via `ToolSearch`, then actually called
  them rather than assuming. `read_console` returned the real Editor startup log
  (`MCP-FOR-UNITY: Server ready on http://127.0.0.1:8080`, `Session connected`, etc.) —
  matches the setup story in the 14:10/14:52 entries above exactly, not placeholder data.
  `manage_scene get_active` returned `Dogfight.unity`, build index 0, 5 root objects, loaded
  and clean — consistent with what earlier entries describe being in the scene. Also ran
  `Tools\compile-check.ps1` (28 files, clean pass, only pre-existing harmless warnings) and
  read `HANDOFF.md`/`BUILD_PLAN.md`/`BUGS.md`/`TESTS.md`/`README.md` end to end — all four
  docs agree with each other, no drift found. **Verdict: MCP tools are genuinely usable this
  session, not just "connected" on paper — use them going forward, including to actually run
  the still-pending Play Mode retest of AA-004/AA-006/AA-007 (AA-005 already has real-telemetry
  confirmation from the 12:43 CDT entry).** Nothing else changed; this was a readiness check,
  not new build work.

- **2026-08-21 (same session) — Migration re-verified after user follow-up, and one real MCP
  reliability caveat found and confirmed (not glossed over).** User asked for a fresh, skeptical
  recheck after past pain with MCP setups elsewhere: confirmed `Packages/manifest.json` has no
  `com.unity.ai.assistant`/`com.unity.ai.inference` and the correct pinned
  `com.coplaydev.unity-mcp` URL; confirmed `.mcp.json` exists at the project root pointing at
  `http://127.0.0.1:8080/mcp`; confirmed against `%LOCALAPPDATA%\Unity\Editor\Editor.log`
  (**not** the project's own `Logs/` folder — Unity doesn't write there) that the server
  actually started on port 8080, matching `.mcp.json` exactly. Left the pre-existing
  `~/.claude.json` `UnityMCP`/`unity-mcp` entries alone as instructed, per the known
  Windows forward-slash-vs-backslash path-key bug making them a non-issue to fix by hand.

  **Then actually drove Play Mode, not just read state**, to give an honest answer to "can you
  really run Play Mode tests and read console logs" — the answer is **yes, with one caveat**:
  - `manage_editor` play/stop genuinely works — confirmed `is_playing` flip both directions,
    and `read_console` genuinely returns real data (the actual Editor startup log on first
    read, matching the setup story verbatim; 0 entries after a clean run, as expected).
  - **Real caveat, found by not trusting a suspiciously-frozen reading:** two live reads of the
    aircraft's transform 3 seconds apart during the same Play session returned the byte-identical
    position, and `mcpforunity://editor/state` was self-reporting `ready_for_tools: false`,
    `blocking_reasons: ["stale_status"]` with a growing staleness age the entire time (and
    stayed stuck on the same `observed_at_unix_ms` for over a minute afterward, even back in
    Edit Mode). Didn't take that at face value either way — cross-checked against the
    independent `LoopDiagnostic` CSV telemetry (`UTI/BeanLogs/…ad28bac3_bean.csv`), which logged
    1,441 real `FixedUpdate` ticks at a correct 0.02s cadence, aircraft flying 255→271 mph in a
    shallow glide from 400 m exactly matching the already-confirmed Phase 1 behavior. **So the
    simulation itself was genuinely running correctly the whole time — it's specifically the
    `mcpforunity://scene/gameobject/{id}` and `mcpforunity://editor/state` resources that don't
    refresh live during/immediately after a Play Mode session in this environment** (plausibly
    because the Editor window has no OS focus/visibility with no human at the keyboard — Unity's
    own tick can still run physics while backgrounded, but this bridge's status-polling doesn't
    seem to keep pace). **Practical implication for future sessions:** don't trust a single
    `scene/gameobject` read taken *during* Play Mode as "current state" — prefer the CSV
    telemetry (already wired via `LoopDiagnostic`/`BeanLogger`) or `read_console` for verifying
    what actually happened in a Play Mode run; `manage_editor play/pause/stop` and
    `read_console` are the two calls confirmed fully reliable so far.

- **2026-08-22 — Corrected a wrong claim from earlier this session: Play Mode retesting does
  NOT require a human, at least not for keyboard/mouse-driven behavior.** Offered to drive
  the AA-004/AA-006/AA-007 retest myself via MCP; first attempt
  (`InputSystem.QueueStateEvent` against the live `Keyboard.current` from inside
  `execute_code`) genuinely failed — the synthetic "D held" state read back `True` for one
  instant, then reverted to `False` within ~2 seconds with `BankAngle` still 0.0, because the
  real OS keyboard backend keeps polling in the background and overwrites a synthetic state
  almost immediately. Told the user this meant a human was needed. **The user pushed back,
  correctly** — other games/agents run solo Play Mode tests all the time, and the actual gap
  was in the technique, not a fundamental limit.

  Found the right tool: the Input System ships `InputTestFixture`
  (`Library/PackageCache/com.unity.inputsystem@.../Tests/TestFixture/InputTestFixture.cs`,
  asmdef `Unity.InputSystem.TestFramework`, `autoReferenced: false` so it must be referenced
  explicitly). Its `Setup()` swaps in a fresh `InputTestRuntime` and fully severs the input
  system from real hardware for the test's duration — genuinely different from poking the
  live device, and exactly why a held key stays held. Built
  `Assets/AstroAces/Tests/PlayMode/` (new asmdef `AstroAces.Tests.PlayMode`, references
  `AstroAces.Runtime` + `Unity.InputSystem.TestFramework`) with
  `Phase3ControlLawPlayModeTests.cs`, which loads the real `Dogfight.unity` scene, finds the
  real test rig, and drives it with `Press`/`Release`/`Set` on isolated virtual devices — a
  real Play Mode run of the real components, not a re-simulation. Ran both tests via the MCP
  `run_tests`/`get_test_job` tools (`mode: "PlayMode"`): both passed, one taking 3.68s of
  real wall-clock time matching its 3s sampling window almost exactly (strong evidence it
  actually ran the full loop, not a short-circuit). **AA-004 and AA-007 moved to CLOSED** —
  full detail in `BUGS.md`'s 2026-08-22 entry.

  **AA-006 genuinely can't be closed this way**, and said so plainly rather than stretching
  the new capability to cover it: `InputTestFixture`'s virtual `Mouse` device has no real OS
  cursor behind it, and AA-006 is specifically about whether `CursorLockMode.Locked` pins the
  *real* OS cursor inside the Editor's Game view. There's no real mispinning behavior left
  for a virtual device to reproduce. That one still needs a human — everything else in Phase
  3 no longer does.

  **Lesson for future phases (worth remembering for Phase 9's AI or anything else that
  reads live device input):** don't generalize "a human is needed" from one failed naive
  attempt — check whether the engine already ships a proper isolation/simulation mechanism
  before concluding something can't be automated.

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

  **Said plainly what this does and doesn't prove**, rather than declaring AA-006 fully
  closed: a programmatic `WarpCursorPosition` call isn't the same code path as genuine
  hardware mouse input, and AA-006's original root cause was specifically an Editor-vs-build
  quirk under real input. Left AA-006 as FIXED-with-strong-evidence rather than CLOSED — a
  human's quick real-mouse check is still the nominal last step for Phase 3, but it's no
  longer blocking, and confidence is high rather than merely hoped-for.

- **2026-08-22 (same session, final) — AA-006 actually root-caused and closed, and Phase 3
  is done.** The user pushed back on the "strong solo evidence" conclusion above with a much
  sharper bug description than any prior report: not a drift, a one-time teleport right at
  Play start, and — after they deliberately tested three mouse-starting-positions — scaling
  with where the real mouse was, contradicting an earlier (pre-code-change) test that had
  seemed to show it was always identical. That contradiction was the clue that the earlier
  automated evidence, while real, hadn't tested the right thing.

  Extended `LoopDiagnostic` (already sitting on the test rig) to also log `mouseDeltaX`,
  `mouseDeltaY`, and `offNoseDeg` every physics tick from true frame 0. Had the user
  reproduce the bug three times for real — mouse resting outside the window, near-right
  inside it, near-left inside it, untouched after pressing Play. All three CSVs showed the
  identical mechanism: mouse delta exactly zero for the first two ticks, then **one single
  corrupted delta reading on the third tick** (0.04s in), never repeating — magnitude/
  direction tracking the real pre-Play cursor position, from a mild 10.5° kick up to the
  full 55° aim-cone cap. Since the aim never auto-recentres by design, that one bad reading
  becomes the new baseline until manually corrected — exactly the symptom reported.

  Root cause: `AircraftInput.SetLocked`'s warp-to-centre is safe against corrupting the
  *same* frame's delta read, but nothing guarded against its own cursor movement echoing
  into a *later* frame's delta — empirically, exactly 2 frames later, every time. **Fix**
  (minimal, at the user's request): `AircraftInput` now discards mouse delta for 3 frames
  after every lock transition, not the 1 frame an earlier attempt used and which had been
  confirmed insufficient. User retested four times: "seemed good." **AA-006 CLOSED, Phase 3
  fully done** — next up is Phase 4 (`ChaseCamera`). Full mechanism and all four numbers in
  `BUGS.md` AA-006.

- **2026-08-22 (same session, later) — Loop actually fixed for real, AA-008 found and closed,
  Phase 4 is next.** User pushed back on treating the loop's ~28s completion time (with a
  10+ second visibly-stuck crawl in the middle) as acceptable. Simulated the exact
  control-law maths in Python first — confirmed the crawl is a stable feedback equilibrium
  (elevator authority fading toward exactly 0 near critical AoA), not a hard impossibility.
  Added `AircraftConfig.elevatorStallFloor` (0.3) so authority never fully dies; cuts loop
  time to ~8-13s. Re-verified every headline number (300.3/315.6 mph, 99.4/116.9 mph,
  2,986 ft) is byte-identical to before the change — this only touches the control law, not
  the aerodynamics. Full derivation and simulation results in `DESIGN.md` §2.6's log.

  Fixing the loop immediately exposed a real, separate bug (**AA-008**): the debug aim
  marker going stale and vanishing mid-loop, since it's world-fixed and a keyboard-only loop
  never touches the mouse. First fix (glue aim to nose while E held) over-corrected — the
  user caught it immediately ("E now inadvertently auto-recentring the crosshair... not
  intent"), since it blocked real mouse input during the hold too. Second fix gates the glue
  on "no real mouse delta this frame," verified with a new automated Play Mode test
  (`AimTrackingPlayModeTests`, extreme/adversarial input, `InputTestFixture`). User then
  reported a residual "looks stuck when upside-down" — built a dedicated 16-second
  hands-off full-loop test (`FullLoopAimPlayModeTests`) that settled it precisely: aim
  tracking is clean the entire way through (max 1.3° deviation, exactly 0.0° at dead
  vertical). The real cause is the Phase 2 placeholder camera, which rolls rigidly with the
  aircraft and flips the whole screen when inverted — not an aim/control bug at all. AA-008
  closed on that finding; the camera behavior is now a documented heads-up at the top of
  `BUILD_PLAN.md`'s Phase 4 section rather than something patched into the throwaway
  placeholder.

  Four PlayMode tests exist now, all passing (`Assets/AstroAces/Tests/PlayMode/`) —
  `AA004_HeldRoll_StaysCommanded_NeverDecaysToZeroMidRoll`,
  `AA007_GamePaused_MouseMovement_DoesNotChangeAim`,
  `AimTracking_ExtremeInput_AndE_Hold_MouseInterrupt`,
  `FullLoop_HandsOffE_AimStaysGluedThroughInvertedPortion`. **Phase 3 is completely done.**

- **2026-08-22 (same session, later still) — Phase 4 built solo.** User asked to "see what
  you can do by yourself" and only be told about major issues. Built `ChaseCamera.cs` to
  `BUILD_PLAN.md`'s Phase 4 spec exactly: exponential (frame-rate-independent) position and
  rotation smoothing, free-look with clamps and centre-return, Caps Lock zoom, near/far clip
  planes, `Layers.MainCameraMask` culling. Un-parented `Main Camera` from the Phase 2
  rig-parenting hack and wired the new component's `target` to the test rig directly in
  `Dogfight.unity`.

  Resolved the AA-008 heads-up deliberately rather than skipping it: chose full
  roll-following (standard flight-game chase-cam behavior, matches real inverted-flight
  camera conventions) relying on the spec's own rotation smoothing to ease transitions,
  rather than adding an unproven up-vector clamp — reasoning is in `ChaseCamera.cs`'s header
  comment and `BUILD_PLAN.md`'s Phase 4 section.

  Verified with two new PlayMode tests (free-look orbit/return, zoom toggle — one assertion
  needed loosening after a real failure taught something true: free-look decay and the
  camera's own rotation Slerp are two stacked smoothing stages, so full settling takes longer
  than either alone suggests), a live screenshot, and direct position/rotation reads during a
  real Play session (confirmed correct culling mask, correct steady-state follow behavior,
  and the expected/documented velocity-proportional follow lag at cruise speed). All 6
  PlayMode tests pass.

  **One real environment finding, not a code issue:** a Play Mode transition sat frozen for
  ~30+ real seconds (unusual — most settle in a few) before catching up and ticking normally
  on its own. Recovered by waiting it out and, on the first occurrence, a `stop` +
  `refresh_unity` + retry. Documented in `TOOLING.md` so a future session doesn't mistake this
  for a real hang and give up early.

  **Phase 4 is done. Phase 5 (HUD and crosshair) is next.**

- **2026-08-22 (same session, later still) — Phase 5 built solo too, right after Phase 4.**
  `CrosshairTexture` (procedural ring+cross+hollow-centre reticle, no art asset),
  `HudController` (top-left AOA/ALT/SPD/THR, raw values), `CrosshairController` (nose-fixed
  gunnery reticle at `cfg.crosshairDistance` + a smaller aim marker on
  `AircraftAimController.DesiredDirection`, both hidden when behind the camera),
  `MessageLog` (2s linear fade, wired to `AircraftEngine.OnAirbrakeChanged`). All four are
  runtime-generated UI (Canvas/RectTransform/TextMeshProUGUI/RawImage built in `Awake()`) —
  no hand-authored scene UI to keep in sync with the code. Added one small shared helper not
  in the original spec, `HudCanvasUtility`, since three components each needing their own
  overlay canvas made a shared builder worth it.

  Deleted `Phase1DebugReadout.cs` and `Phase2DebugReadout.cs` and removed both components
  from the test rig — but only after verifying the real HUD showed correct values in a live
  Play session first (readout text, gunnery reticle visibility, message fade all read back
  correctly via `execute_code`), not on faith that the compile check passing meant it worked.

  Added `HudPlayModeTests.cs` (3 tests: readout format, gunnery reticle visibility, message
  fade) — needed `Unity.TextMeshPro` and `UnityEngine.UI` added to the PlayMode test
  assembly's references, which hadn't been needed until a test read UI component state
  directly. All 9 PlayMode tests pass.

  **Phase 5 is done. Phase 6 (World: ground, sky, clouds, play-area bounds) is next.**

- **2026-08-22 (same session, later still) — Phase 6 built solo, the biggest phase of
  the session.** Two hand-written URP shaders: `Toon.shader` (banded diffuse + rim light,
  reusing URP's own stock `ShadowCasterPass.hlsl`/`DepthOnlyPass.hlsl` for the shadow/depth
  passes rather than reinventing that math — read the actual URP 17.3 source first to
  confirm neither pass needs anything beyond `Core.hlsl` when `_ALPHATEST_ON` is never
  defined) and `SpaceSky.shader` (procedural vertical gradient, hash-based stars, a faint
  nebula band, no texture asset). Both compiled clean on the first attempt. Four scripts:
  `GroundBuilder` (201x201-vertex layered-Perlin displaced mesh, +-25m relief),
  `RockScatter` (400 rocks raycast onto the real terrain height), `CloudField` (40 clusters,
  235 puffs, every single one confirmed to have zero collider), `PlayAreaBounds` (soft
  push-back force + a `MessageLog` warning, no invisible wall).

  Replaced the Phase 0 ground placeholder and deleted `StarfieldPlaceholder.cs`. Verified
  live rather than trusting the compile check: rock/cloud counts and collider-removal via
  `execute_code`, the sky gradient by force-rotating the *real* camera to look near-zenith
  (found along the way that the screenshot tool's own `view_rotation` parameter does **not**
  move the live camera at all — a genuine dead end, documented so it's not retried).
  `PlayAreaBounds` triggered organically once already (the rig drifted out of bounds during
  an unrelated screenshot session and the warning appeared on its own), then confirmed
  properly with a dedicated test — which failed twice for a real reason before passing: a
  raw `transform.position` teleport silently reverts on a non-kinematic Rigidbody's very
  next `FixedUpdate`, traced with a temporary `Debug.Log` rather than continued guessing.
  Fixed by using `rb.position`/`rb.rotation` instead — a genuinely generalizable Unity
  gotcha, written up in `TOOLING.md`, not just patched locally. All 10 PlayMode tests pass.

  **Phase 6 is done. Phase 7 (Weapons) is next.**

- **2026-08-22 (same session, later still) — Swapped the capsule for a real placeholder
  model.** User's call on the Phase 7 dependency question above: don't do the full aircraft-
  model treatment (verified 10m wingspan, grey/green vs. black/red recolour — this has no
  phase number of its own, see the corrected "Aircraft model scale" row in Open Decisions),
  just get real wing geometry in instead of the capsule ("the bean"). `Omega_fighterG/
  Meshes/fighter_black.FBX` was already imported and sitting unused — instantiated it as a
  child of the rig at a rough 1.5x scale, disabled the capsule's own `MeshRenderer` (kept
  the `CapsuleCollider` exactly as-is, physics wasn't the point here), confirmed via
  screenshot it's correctly oriented and reads as a real fighter shape from the chase cam.
  This is genuinely temporary — not scaled/recoloured to spec, no player/enemy variant — the
  real treatment still needs doing properly; this just gives Phase 7 real wing geometry to
  parent muzzle transforms to sooner.

  Found one more real bug along the way: `SimpleNaturePack`'s rock prefabs use the Built-in
  Render Pipeline's `Standard` shader and rendered hot magenta under URP — same failure mode
  as the ground/aircraft placeholders back in Phase 0/1. Added a `rockMaterial` field to
  `RockScatter` that overrides every renderer on every instantiated rock with a proper URP
  toon material (`RockToon.mat`), confirmed fixed via screenshot. All 10 PlayMode tests
  still pass.

- **2026-08-22 (same session, later still) — AA-009: free-look was silently steering the
  aircraft too.** User: "isnt holding right click supposed to give a free view... it kinda
  works but the ship still moves as you look so its not true free look." Real bug, not user
  error — `ChaseCamera` (Phase 4) correctly gates its own free-look orbit on
  `FreeLookHeld`, but `AircraftAimController` never had a matching gate at all; it fed the
  exact same mouse delta into `StepAim` regardless, so holding right-click to look around
  kept steering the whole time. A Phase 2 gap, invisible until Phase 4 gave `FreeLookHeld`
  its first real consumer. Fixed with one early-return in `AircraftAimController.Update()` —
  freezes the aim while free-look is held (consistent with its own "never auto-centres"
  rule), resumes on release. Verified with a new test asserting both the freeze and that
  normal aim still works right after release. All 11 PlayMode tests pass.

- **2026-08-22 (same session, later still) — AA-010: free-look orbited its own fixed spot,
  not the ship.** Immediate follow-up to AA-009, working autonomously (per "see what you can
  do by yourself... let me know if any major issues"). User: "the camera obrits but it orbits
  its camrea spot / it should look around the plane... look at wings and underneith and
  cockpit." The first free-look implementation only composed the free-look offset into the
  camera's *rotation* — its position never left the fixed chase spot behind the tail, so
  right-click could pan the view but never actually swing around the ship.

  Redesigned `ChaseCamera.UpdateFollow()`: while orbiting, rotate `localOffset` by the
  accumulated free-look yaw/pitch, transform that around the target to get a new camera
  *position*, then look back at the ship from there. At zero free-look this is identical to
  the old fixed-offset behavior.

  The new test, `FreeLook_OrbitsAroundShip_NotJustRotatesInPlace`, failed three times before
  passing — each time for a real reason in the test's own setup, not the camera fix, which
  turned out to be correct from the first attempt:
  1. Consistent ~75-86° "still not looking at the ship" failures across several different
     wait strategies (fixed frame counts, then an 8-second real-time poll) looked like
     genuine non-convergence at first glance.
  2. Root cause: the rig flies ~114 m/s against only an ~8.5m orbit radius, and this
     environment's render-frame delivery while the Editor is unfocused is already known to
     be irregular (see `TOOLING.md`) — the ship can move farther between `LateUpdate` calls
     than the entire camera-to-ship distance, which swings the orbit math's target
     position/rotation wildly frame to frame and never lets the Slerp settle. That's a test
     confound, not a property of the camera code — froze the rig's Rigidbody
     (`isKinematic = true`, zero velocity) for the test's duration to remove it.
  3. With the ship frozen, look-angle and position-delta passed immediately, but held
     distance still failed (5.7-6.0m measured vs. an ~8.5-12m baseline). Second, different
     test-geometry issue: `Vector3.Lerp` moves the camera along a straight chord between its
     old and new points on the (spherical) orbit, and any chord between two points on a
     sphere's surface passes inside the sphere — so distance-from-ship legitimately dips
     mid-transition before recovering, and the test's early-break condition (fires as soon
     as look-angle/position-delta clear their thresholds) was catching it right at that dip.
     Added a 1.5s real-time settle window after the break, before measuring distance.

  All 12 PlayMode tests pass. Full mechanism and all four numbers in `BUGS.md` AA-010.
  **Lesson worth keeping:** a test failing consistently across multiple different wait
  strategies isn't automatically proof the code under test is wrong — it can equally mean
  the test's own physical setup (a fast target orbited at close range) or its own geometry
  (chord vs. arc distance) is what's unstable. Isolate the confound before concluding the
  product code is broken.

---

## Archive log

- **2026-08-22** — Created. Moved `HANDOFF.md`'s entire session log (2026-08-17 through
  2026-08-22, Phases 0–6) here verbatim to cut `HANDOFF.md`'s read length; replaced it there
  with a short rollup. Nothing was edited or condensed — this is the same text, just moved.
