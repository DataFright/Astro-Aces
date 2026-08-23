# Astro Aces — Build Plan

Executable task list for building the foundation. Written to be followed in order by an
implementer who has read `DESIGN.md` chapters 1–3 and 11.

**Already done and committed — do not rewrite these:**
`Aero.cs`, `FlightControlLaw.cs`, `AircraftConfig.cs`, `Core/Layers.cs`,
`Editor/ProjectSetup.cs`, both `.asmdef` files, `Tools/compile-check.ps1`.
They contain the derived maths and the sign conventions. Call them; do not reimplement them.

---

## Rules — non-negotiable

1. **Run the compile check after every file you write:**
   ```bash
   powershell -ExecutionPolicy Bypass -File "Tools\compile-check.ps1"
   ```
   ~5 seconds, no Unity focus needed. Never hand over code that has not passed it.
2. **New Input System only.** `UnityEngine.Input.anything` throws. Use `Keyboard.current`,
   `Mouse.current`, and null-guard both.
3. **C# 9.** No file-scoped namespaces, no global usings.
4. `rb.linearVelocity`, not `rb.velocity`.
5. **No clamps on speed or altitude.** If the aircraft goes too fast, change a coefficient.
6. **Never inline a sign flip on angular velocity.** Use `BodyRates.FromUnity` / `ToUnity`.
7. Layer numbers come from `Core.Layers`. Never write a bare layer index or LayerMask.
8. Aerodynamic maths belongs in `Aero`. If you need a new formula, add it there as a static
   pure function so a test can reach it.
9. When a value in `DESIGN.md` changes, update `DESIGN.md` in the same session, with a log
   line at the bottom.

---

## Phase 0 — Project setup

**0.1** In Unity: menu **Astro Aces > Setup Project (Tags, Layers, Settings)**, then
**Astro Aces > Verify Project Setup**. Console must report layers verified.

**0.2** Create the config asset: **Assets > Create > Astro Aces > Aircraft Config** at
`Assets/AstroAces/Config/PlayerAircraftConfig.asset`. Defaults are already correctly tuned —
**do not hand-enter values.** Duplicate it as `EnemyAircraftConfig.asset`.

**0.3** New scene `Assets/AstroAces/Scenes/Dogfight.unity`, set as scene 0 in Build Settings.

**Acceptance:** layers 6–10 named, both config assets exist, scene opens.

---

## Phase 1 — Aircraft flies — **done, see HANDOFF.md and BUGS.md AA-002**

Order matters: get it flying on physics alone before adding a controller on top.

One addition beyond what's specced below: `AircraftPhysics.Awake()` also sets
`rb.linearVelocity` to the analytic trim speed (`Aero.TopSpeedMps` at spawn
altitude/throttle) before the first physics step. Not optional — an aircraft spawned at
rest free-falls into a stall before it has enough airspeed for lift to matter. Full
diagnosis in `BUGS.md` AA-002; the rule itself is now in `DESIGN.md` §9.

### 1.1 `Scripts/Flight/AircraftState.cs`
Shared per-frame state so nothing recomputes it. Component on the aircraft root.

```
public class AircraftState : MonoBehaviour
    public Rigidbody Body           { get; }
    public Vector3   LocalVelocity  { get; }   // InverseTransformDirection(linearVelocity)
    public float     AirspeedMps    { get; }
    public float     AltitudeMeters { get; }   // transform.position.y, ground plane is y = 0
    public float     Density        { get; }   // Aero.DensityAt(cfg, altitude)
    public float     AngleOfAttack  { get; }   // Aero.AngleOfAttack(LocalVelocity)
    public float     SideSlip       { get; }
    public float     BankAngle      { get; }   // FlightControlLaw.BankAngle(transform, previous)
    public BodyRates Rates          { get; }
    public bool      IsAlive        { get; set; }
```
Refresh once in `FixedUpdate`, before anything reads it. Cache `BankAngle` in a field and
pass the previous value in as the fallback.

### 1.2 `Scripts/Flight/AircraftEngine.cs`
Throttle only. `Throttle` is 0…`cfg.maxThrottle`, starts at `cfg.startThrottle`.
`public bool AirbrakesOn` toggled by `ToggleAirbrakes()`, which fires
`event Action<bool> OnAirbrakeChanged` for the HUD message.

### 1.3 `Scripts/Flight/AircraftPhysics.cs`
`FixedUpdate`, after `AircraftState` refresh:
```
rb.AddForce(Aero.ThrustForce(cfg, transform, engine.Throttle, state.Density));
rb.AddForce(Aero.AerodynamicForce(cfg, transform, rb.linearVelocity, state.Density, engine.AirbrakesOn));
```
Then apply rates (see 3.3). Rigidbody setup: mass `cfg.massKg`, **useGravity on**,
`linearDamping = 0`, `angularDamping = 0`, interpolation **Interpolate**, collision
detection **ContinuousDynamic**.

> `linearDamping` must be 0. Unity's own damping on top of our drag model would silently
> lower top speed below 300 mph and no test would say why.

**Acceptance:** with no controller, spawned at 400 m at 80% throttle pointing level, the
aircraft holds roughly level flight and settles near **267 mph**. If it sinks or accelerates
without limit, stop and fix Phase 1 — do not proceed.

---

## Phase 2 — Intent

### 2.1 `Scripts/Flight/AircraftInput.cs` — **done, see note**
Reads devices, stores nothing else. Public read-only properties: `MouseDelta`,
`ThrottleUpHeld` (W, held) + `ThrottleDownPressed` (S, edge-triggered — DESIGN.md §2.11's
~1% trim step is discrete, not a held rate, so this needed two properties, not one
combined `ThrottleAxis`), `ScrollNotches` (`scroll.y / 120f`), `RollAxis` (A/D),
`PitchAxis` (E/Q), `FireHeld` (renamed from `FirePressed` — the design brief says holding
it fires continuously, so "held" is the accurate name), `FreeLookHeld`, `AirbrakeToggled`,
`ZoomToggled`, `PausePressed`.

Mouse delta is **already per-frame — do not multiply by `Time.deltaTime`.**
Locking/unlocking the cursor is centralized in `public void SetLocked(bool)` (not
`OnEnable`/`OnDisable` touching `Cursor.lockState` directly, as first written) — see
`BUGS.md` AA-006. **Fixed twice.** First attempt: `CursorLockMode.Locked` snaps the OS
cursor to centre, and the next `Mouse.delta` sample can report that snap as real movement —
tried discarding one delta sample per lock transition. User tested it and confirmed it
didn't work; the real symptom was persistent drift, not a one-time kick, since
`CursorLockMode.Locked` doesn't reliably pin the cursor every frame (especially in the
Editor's Game view). **Current fix:** `SetLocked` actively `Mouse.WarpCursorPosition`s to
centre synchronously the instant locking engages, and `Update()` does the same every frame
thereafter (read delta first, warp after). `TempPauseToggle` routes pause/resume through
`SetLocked` too, for the same reason — resuming re-locks the cursor.

Also added `public static bool GamePaused` (set by `TempPauseToggle`) — `Time.timeScale = 0`
does not stop `Update()`, so without this gate the aim kept drifting from mouse movement
even while "paused" (`BUGS.md` AA-007). While true, every property reports neutral and no
device is read.

Also wired `AircraftEngine.Update()` to read this and drive its own `ApplyThrottleAxis` /
`ApplyThrottleFineStep` / `ApplyThrottleNotch` / `ToggleAirbrakes` — not a separate
component, since `AircraftEngine` already owned all of those mutators and null-guards for
"no `AircraftInput` sibling" exactly like `AircraftAimController` does.

### 2.2 `Scripts/Flight/AircraftAimController.cs` — **done, confirmed in Play Mode**
```
public Vector3 DesiredDirection { get; private set; }
void Update() => DesiredDirection =
    FlightControlLaw.StepAim(ref aimYaw, ref aimPitch, input.MouseDelta, transform, cfg);
```
Initialise `aimYaw`/`aimPitch` from the spawn heading so the aim starts on the nose.
Expose `SetDesiredDirection(Vector3)` so the AI can drive the same component. Implemented
with a null-guard on `AircraftInput` (absent = AI-driven, `Update()` is a no-op) so the
same component serves both roles per DESIGN.md's "same flight model, different pilot" rule.

**Acceptance:** aim marker moves with the mouse, stays put when the mouse stops, never
leaves the 55° cone. **No real crosshair exists until Phase 5**, so this was verified with
a throwaway `Phase2DebugReadout.cs` (delete alongside `Phase1DebugReadout.cs` once Phase 5
lands) showing the aim-off-nose angle and a screen-space marker.

---

## Phase 3 — Control law — **DONE. AA-004/AA-005/AA-006/AA-007 all CLOSED, AA-006 confirmed by the user's own retest 2026-08-22**

Implemented exactly as specced below in `AircraftPhysics.FixedUpdate`, after the force
block, guarded by `if (!state.IsAlive) return;` before any of it runs. The mouse-driven part
(3.1) worked correctly on first Play Mode test — turning, banking, settling toward the aim
all matched expectations. The manual override part (3.2) took two rounds to get right: see
the correction in that section and `BUGS.md` AA-004 (roll/pitch couldn't complete) and
AA-005 (loop still veered after AA-004, a second independent bug). Also fixed AA-006 while
in the same round — spawn direction was randomly kicked by cursor position, unrelated to
the control law itself but found in the same Play Mode session. All three need a second
Play Mode pass.

Before handing this to the user, simulated the **full closed loop** (forces + control law +
rate integration + orientation, not just the pieces in isolation) in Python: aircraft
spawned level at trim speed, aim fixed 30° right of the initial nose. Result: banks to
~47° within 1 s, AoA briefly touches 17° (under the 20° critical limit — the AoA limiter
engaged correctly), then converges **monotonically, no oscillation**, to under 1° off the
target by 19 s, holding 235–263 mph and 1,150–1,430 ft throughout. This is the first time
`Aero` and `FlightControlLaw` were exercised together in a feedback loop rather than as
independently-tested pure functions — worth doing given `BUGS.md` AA-002 was exactly this
category of bug (each piece correct in isolation, wrong combination not simulated first).

### 3.1 Wire it
In `AircraftPhysics.FixedUpdate`, after forces:
```
Vector3 localTarget = transform.InverseTransformDirection(aim.DesiredDirection);
ControlCommand cmd  = FlightControlLaw.Compute(localTarget, state.BankAngle, state.Rates,
                                               state.AngleOfAttack, state.SideSlip, cfg);
```

### 3.2 Manual keyboard override — **corrected twice, see BUGS.md AA-004 and AA-005**
**Not additive — override.** The original spec here said "add on top of the computed
command," but `FlightControlLaw.Compute` already clamps its own aileron/elevator to ±1
internally, and once that internal term saturates in one direction, adding the keyboard's
±1 on top nets to zero at the clamp boundary — the aircraft would hit an invisible wall
partway through a roll or loop instead of completing it. Confirmed by the user's first
Phase 3 test and root-caused by inspection, not simulation.
```
if (input.RollAxis != 0f)
{
    cmd.aileron = input.RollAxis;                 // full override, no limiter on this axis
}
else if (input.PitchAxis != 0f)
{
    cmd.aileron = 0f;                             // AA-005: stop the mouse aim-seeking bank
}                                                  // from stealing rotation during a loop

if (input.PitchAxis != 0f)
    cmd.elevator = FlightControlLaw.ApplyAoALimiter(input.PitchAxis, state.AngleOfAttack, cmd.aoaLimitFactor);
```
Pitch still passes through the same AoA limiter the mouse term uses (now a shared public
method) — stall protection has to apply uniformly to every input source, not just the
mouse, or holding E lets the player force a stall the mouse never could. This is what makes
manual aileron rolls possible; a loop additionally requires enough speed to stay under the
AoA limit through the maneuver, same as real War Thunder Air Arcade. Do not gate either
behind a mode.

The `else if` matters: mouse aim is capped at ±`maxAimPitch` (80°) and never rotates with
the aircraft, so a sustained manual pitch past that cap puts the local target *behind* the
aircraft, which saturates the mouse's bank-seeking term toward max — correct for "chase a
target that's now behind me," wrong here since nothing asked to turn. Left live, it silently
diverted loops into an uncommanded bank (AA-005). Manual roll doesn't need the reverse
suppression — pitch's cross-axis interference during a roll is damped, not saturated.

### 3.3 Apply rates
```
float sf = Aero.SpeedFactor(cfg, state.AirspeedMps);
BodyRates next = FlightControlLaw.StepRates(state.Rates, cmd, sf, Time.fixedDeltaTime, cfg);
rb.angularVelocity = transform.TransformDirection(next.ToUnity());
```
**Skip this line entirely when `!state.IsAlive`** so wreckage tumbles on real physics.

**Acceptance:** the aircraft turns toward the mouse by banking, holds a turn without the
nose dropping badly, does not wobble, and A/D produce a clean aileron roll. Pulling hard at
low speed mushes rather than snapping into a spin.

---

## Phase 4 — Camera — **DONE, 2026-08-22**

`Scripts/UI/ChaseCamera.cs`, `LateUpdate`. Offset (0, 3, −8) in aircraft space, position
`Lerp` ≈ 8/s, rotation `Slerp` ≈ 10/s. Free-look accumulates yaw/pitch from mouse delta
while `FreeLookHeld` (±120° / ±70°), returning to zero over ~0.25 s on release. Caps Lock
toggles FOV 60 ↔ 24. Camera: near 0.3, far **12,000**, culling mask `Layers.MainCameraMask`.

Built as specified above, exactly. Un-parented `Main Camera` from the Phase 2 placeholder
rig-parenting hack, added `ChaseCamera`, wired `target` to the test rig. Both position and
rotation smoothing use exponential decay (`1 - e^(-rate * dt)`) rather than a naive
`rate * dt` Lerp, so the follow rate is genuinely frame-rate independent. Verified with two
new PlayMode tests (`ChaseCameraPlayModeTests.cs` — free-look orbits and returns to centre,
Caps Lock toggles FOV) plus a live screenshot sanity check. All 6 PlayMode tests pass.

**One known, expected characteristic, not a bug:** at cruise speed (~255 mph) the position
lag settles to roughly 15-23 m behind the nominal 8 m offset — the normal steady-state lag of
exponential smoothing chasing a constant-velocity target (lag ≈ speed / lerp rate). This is a
faithful result of the literal spec numbers above, not a deviation. If it reads as too loose
in practice, `positionLerpPerSecond` is the knob — a Phase 14 feel call, not fixed here
without evidence it's actually a problem.

> **Heads-up from AA-008 (2026-08-22, see `BUGS.md`):** the Phase 2 placeholder camera rolls
> exactly with the aircraft (rigid parent, no independent orientation), so the player's whole
> screen flips upside-down during any maneuver that inverts the aircraft — a loop, most
> obviously. Confirmed by real telemetry that this is a **camera** problem, not an aim/control
> problem (the crosshair's underlying target direction stayed within 1.3° of the nose through
> the entire inverted portion of a full loop). Whatever `ChaseCamera`'s `Slerp`-based rotation
> ends up doing, decide **deliberately** how much of the aircraft's roll it should actually
> follow — smoothing alone won't fix a screen-flip if the target rotation itself still fully
> inverts.

**Decision made (2026-08-22, see `ChaseCamera.cs`'s header comment for the full reasoning):**
full roll-following, including full inversion — that's standard, expected chase-cam behavior
in a flight game, and the actual problem was the *lack* of smoothing in Phase 2's placeholder
(an instant snap), not the inversion itself. Relying on the spec's own
`rotationSlerpPerSecond ≈ 10/s` to ease the transition rather than adding an up-vector clamp
with no evidence one was still needed. Revisit if real testing after this still finds it
disorienting.

**Two more bugs found and closed after this phase shipped, both in free-look (see `BUGS.md`
for full detail):** **AA-009** — `AircraftAimController` had no gate on `FreeLookHeld` at all,
so holding right-click to orbit the camera also kept steering the aircraft the whole time;
fixed with an early-return that freezes the aim while free-look is held. **AA-010** — the
free-look implementation above only ever composed the free-look offset into the camera's
*rotation*, never its *position* — right-click could pan the view but the camera never
actually left its fixed spot behind the tail, so you could never see the wings, underside, or
cockpit. `UpdateFollow()` now rotates `localOffset` by the accumulated free-look yaw/pitch,
transforms that around the target to get a genuinely new camera position, and looks back at
the ship from there — reduces to the original fixed-offset behavior at zero free-look.

---

## Phase 5 — HUD and crosshair — **DONE, 2026-08-22**

`Scripts/UI/CrosshairTexture.cs` — static factory building the reticle `Texture2D` in code
(circle + cross + hollow centre, α 0.75). No art asset.

Built as specified below, exactly, plus one shared helper not in the original spec:
`Scripts/UI/HudCanvasUtility.cs`, since `HudController`, `CrosshairController` and
`MessageLog` each need their own Screen Space Overlay canvas and duplicating that setup code
three times wasn't worth it. All UI is generated at runtime (`GameObject`s + `RectTransform`s
built in `Awake()`) — no hand-authored Canvas/prefab in the scene, matching the "no art asset"
spirit for the whole HUD, not just the crosshair texture. Retired `Phase1DebugReadout.cs` and
`Phase2DebugReadout.cs` (deleted, per their own "delete after Phase 5" doc comments) and
removed them from the test rig. Verified with 3 new PlayMode tests
(`HudPlayModeTests.cs`) plus live Play Mode reads of the actual rendered text/marker state —
all 9 PlayMode tests pass.

`Scripts/UI/HudController.cs` — Screen Space Overlay canvas.
Top-left `TextMeshProUGUI`, updated in `Update`:
```
AOA  {aoa,5:0.0}°
ALT  {altitude * Aero.MetersToFeet,6:0} ft
SPD  {airspeed * Aero.MpsToMph,5:0} mph
THR  {throttle * 100f,3:0}%
```
`Scripts/UI/CrosshairController.cs` — gunnery reticle at
`aircraft.position + aircraft.forward * cfg.crosshairDistance` via `WorldToScreenPoint`;
smaller aim marker on `DesiredDirection`. Hide either when its `z < 0`.

`Scripts/UI/MessageLog.cs` — centre-low transient text, 2 s fade. Subscribe to
`OnAirbrakeChanged` → "AIRBRAKES DOWN" / "AIRBRAKES UP".

---

## Phase 6 — World — **DONE, 2026-08-22**

Built as specified below, exactly, all six parts. `Toon.shader`'s ShadowCaster/DepthOnly
passes reuse URP's own stock HLSL (`ShadowCasterPass.hlsl`/`DepthOnlyPass.hlsl`) rather than
writing that math by hand — both compile out their only alpha-test-gated code entirely since
this shader never defines `_ALPHATEST_ON`, so `Core.hlsl` alone is enough to plug into them.
Verified the whole phase live: 400 rocks scattered onto the real displaced terrain via
raycast, 40 cloud clusters (235 puffs, confirmed zero colliders on any of them), the sky
gradient checked by directly forcing the camera to look near-zenith (screenshotting a
"straight up" *view_rotation* parameter does NOT actually rotate the live camera — had to set
`transform.rotation` directly, screenshot, then restore `ChaseCamera`), and
`PlayAreaBounds` confirmed both organically (the rig drifted out during an unrelated
screenshot session and the warning appeared on its own) and with a dedicated test. Retired
`StarfieldPlaceholder.cs`, per its own "delete once Phase 6's real sky exists" doc comment.

### 6.1 `Shaders/Toon.shader`
Hand-written URP shader. Passes: `UniversalForward`, `ShadowCaster`, `DepthOnly`.
Banded diffuse (2–3 steps), `_BaseColor`, `_ShadowTint`, `_RimColor`, `_RimPower`.
**Not Shader Graph** — unreadable as text and unmergeable.

### 6.2 `Shaders/SpaceSky.shader`
Skybox. Vertical gradient deep purple → near-black blue, procedural hash stars, faint
nebula band. Assign in Lighting settings.

### 6.3 `Scripts/World/GroundBuilder.cs`
Generates a subdivided plane, 5 km × 5 km, ~200×200 quads, layered `Mathf.PerlinNoise`
displacement of **±25 m**. Adds `MeshCollider`, layer `Layers.Ground`, toon material in a
dark rocky purple-grey. `[ContextMenu("Rebuild")]` so it can be regenerated in-editor.

### 6.4 `Scripts/World/RockScatter.cs`
Scatters `SimpleNaturePack` `Rock_01`–`Rock_05` prefabs, scale 20–80×, random yaw, sampled
onto the ground height by raycast. Seeded `System.Random` so layout is reproducible.

### 6.5 `Scripts/World/CloudField.cs`
N cloud clusters, each 4–8 jittered spheres, scale 40–120 m, altitude 300–800 m, toon
material, **colliders removed**, layer `Layers.Cloud`, slow drift.

### 6.6 `Scripts/World/PlayAreaBounds.cs`
5 km × 5 km. Outside it: warning message plus a gentle force back toward centre. **No
invisible wall.**

---

## Phase 7 — Weapons

### 7.1 `Scripts/Combat/Projectile.cs`
```
public void Launch(Vector3 position, Vector3 velocity, float damage, GameObject owner)
```
`FixedUpdate`: compute `next = pos + vel * dt`, **`Physics.Raycast` from `pos` toward `next`
over `(next - pos).magnitude`** against `Layers.ProjectileHitMask`, ignoring `owner`.
On hit: apply damage, spawn impact effect, despawn. Otherwise advance. Despawn after
`cfg.projectileLifetime`.

> The swept raycast is a correctness requirement, not an optimisation — see `DESIGN.md` §6.

### 7.2 `Scripts/Combat/ProjectilePool.cs`
Simple pre-allocated pool, 256 rounds. `Get()` / `Return()`.

### 7.3 `Scripts/Combat/AircraftGun.cs`
Accumulator in `Update` (not `FixedUpdate`) at `cfg.fireRate`.
```
Vector3 v = rb.linearVelocity + muzzle.forward * cfg.muzzleSpeed;
```
Muzzle transforms parented under the wings. Tracer material: green for player, red for
enemy. Fires `event Action OnFired` for audio.

---

## Phase 8 — Damage and death

`Scripts/Combat/Health.cs` — `CurrentHealth`, `TakeDamage(float, GameObject source)`,
`event Action<Health> OnDied`. Starts at `cfg.maxHealth`.

`Scripts/Combat/AircraftDeath.cs` — on death: `state.IsAlive = false` (rate control stops,
wreck tumbles), disable gun and control law, spawn explosion, play destruction sound.
Player death → `GameStateController.PlayerDied()`.

`Scripts/Combat/CollisionDamage.cs` — ground contact is instantly lethal; aircraft-to-aircraft
contact is lethal to both.

---

## Phase 9 — Enemy AI

`Scripts/AI/EnemyPilot.cs` — state machine Patrol / Pursue / Attack, values in `DESIGN.md` §8.
Writes only through `aim.SetDesiredDirection(...)` and `engine.Throttle` and
`gun.TriggerHeld` — **no direct transform or Rigidbody access.** The enemy is bound by the
same flight model as the player.

Lead prediction for firing:
```
float t = distance / cfg.muzzleSpeed;
Vector3 aimPoint = target.position + target.velocity * t;
```
Vision: 45° half-angle, 1,500 m, `Physics.Raycast` against `Layers.VisionBlockMask`.
Expose the current state as a public property so the HUD can debug-display it.

---

## Phase 10 — Minimap

`Scripts/UI/MinimapController.cs` — orthographic camera, `RenderTexture` created in code
(512², depth 16), culling mask `Layers.MinimapMask`, ortho size ≈ 1,200 m, follows player
XZ at a fixed height looking down, **north-up**. `RawImage` top-right.
`Scripts/UI/MinimapIcon.cs` — flat quad on `Layers.MinimapIcon`, parented to each aircraft,
counter-rotated to stay flat; player arrow rotates with heading. Player icon green, enemy
icon red.

---

## Phase 11 — Menus

**A stopgap already exists: `Scripts/Core/TempPauseToggle.cs`**, added ahead of schedule
because there was no way to stop the simulation without hunting for the Editor's Stop
button. It reads Escape directly (not through `AircraftInput.PausePressed`, to avoid a
same-GameObject execution-order dependency), toggles `Time.timeScale` 0/1, sets
`AircraftInput.GamePaused` (found the hard way — `timeScale` alone doesn't stop `Update()`,
so the aim kept drifting while "paused"; see `BUGS.md` AA-007), and shows a bare "PAUSED"
`OnGUI` box — no restart/quit buttons, no state machine. **Delete this file** when building
the real thing below; don't extend it. The real `GameStateController` should own pause
state directly rather than going through `AircraftInput.GamePaused` — that flag only exists
because this stopgap needed *something* today.

`Scripts/Core/GameStateController.cs` — states Flying / Paused / Dead.
Esc toggles pause: `Time.timeScale = 0`, cursor unlocked (route through
`AircraftInput.SetLocked`, not `Cursor.lockState` directly — see `BUGS.md` AA-006), menu
shown. Death shows the same menu with a "DESTROYED" header. Restart reloads the active scene
(**reset `Time.timeScale = 1` first** — a classic soft-lock). Quit calls
`Application.Quit()` and logs in-editor.

Menu input must use unscaled time so it still works at `timeScale = 0`.

---

## Phase 12 — Audio (muted)

`Scripts/Core/AudioDirector.cs` — sets `AudioListener.volume = 0` in `Awake`, exposes
`static bool Muted`. `Scripts/Core/SoundBank.cs` — ScriptableObject with named `AudioClip`
fields: `gunfire`, `impact`, `explosion`, `aircraftDestroyed`, `engineLoop`.

Wire the events now; **leave the clip fields empty.** The pack's 50 clips are named
`DM-CGS-01`…`50` with no descriptions, so assigning them requires listening — that is a
separate task, recorded in `HANDOFF.md`.

---

## Phase 13 — Tests

`Assets/AstroAces/Tests/Editor/` with an asmdef referencing `AstroAces.Runtime`,
`UnityEngine.TestRunner`, `UnityEditor.TestRunner`, and the `nunit.framework.dll` precompiled
reference. Write the tests listed in `TESTS.md`. They are the guard on the headline numbers —
a careless coefficient tweak must fail a test, not silently change the game.

---

## Phase 14 — Tuning pass

Only after everything above works. Fly it. Use the `DESIGN.md` §3 triage table — identify
the layer at fault *before* touching a number. Log every value change in `DESIGN.md`.

---

## Log

- **2026-08-17 15:10 CDT** — Created. 14 phases with explicit contracts and per-phase
  acceptance checks, ordered so each phase is verifiable before the next depends on it.

- **2026-08-17 16:45 CDT** — Phase 0 and Phase 1 built and confirmed working in the actual
  Editor (not just the compile check) — see `HANDOFF.md` for the full session trace,
  `BUGS.md` AA-002 for the spawn-velocity fix. Phase 2 built (`AircraftInput`,
  `AircraftAimController`, throttle wiring); sections above updated in place to match what
  was actually implemented, including two small deviations from the original spec
  (`ThrottleAxis` split into two properties, `FirePressed` renamed `FireHeld`) since nothing
  had consumed those names yet. Not yet confirmed in Play Mode — that's next.

- **2026-08-17 17:02 CDT** — Phase 2 confirmed clean by the user (no issues). Phase 3 built
  and verified by closed-loop simulation before handoff (see the Phase 3 section above) —
  the aircraft should now visibly turn toward the mouse for the first time. Not yet
  Play-tested.

- **2026-08-17 17:09 CDT** — **Correction:** the line above previously claimed `CLAUDE.md`
  had been updated to list this doc. Checked `CLAUDE.md` directly against disk when the
  user asked about it — that claim was false. `CLAUDE.md` was never modified this session,
  and didn't need to be: it already listed `BUILD_PLAN.md` in its Documentation Structure
  section from the start. Removed the false claim rather than leave it standing. Logged as
  a bug in `BUGS.md` (AA-003) since a log entry describing an action that didn't happen is
  a real trust problem for anyone relying on these breadcrumbs later.

- **2026-08-18 10:35 CDT** — First Phase 3 Play Mode test: mouse-driven turning worked, but
  manual roll/pitch (3.2) didn't — both capped out partway through instead of completing a
  roll or loop. Root-caused and fixed; see the corrected 3.2 section above and `BUGS.md`
  AA-004. Needs a second Play Mode pass to confirm.

- **2026-08-18 11:37 CDT** — Same round of testing found two more real issues after AA-004
  landed: AA-005 (loop still veered off — a second, independent bug in the same area, mouse
  bank-seeking fighting a manual pitch past the aim cone) and AA-006 (spawn direction kicked
  randomly by cursor position — a cursor-lock first-delta gotcha, unrelated to the control
  law). Both fixed, see `BUGS.md`. Added `TempPauseToggle` as a requested stopgap ahead of
  Phase 11 — noted in that section so it gets deleted, not extended, when Phase 11 is built
  for real. All three fixes await a second Play Mode pass.

- **2026-08-18 12:01 CDT** — User re-tested and found two of the three still broken. AA-006
  ("ignore first delta") demonstrably did not fix the spawn-direction issue — replaced with
  active `WarpCursorPosition` recentering, a different category of fix. The loop still
  wouldn't complete, but a full 3D simulation confirmed AA-005's fix is genuinely correct
  (no uncommanded roll) — the remaining behavior is a tuning/energy-budget characteristic,
  not a control-law bug, flagged for a Phase 14 decision rather than patched blindly. Also
  found and fixed AA-007: pause wasn't actually freezing the aim, only the aircraft's
  physics, because `Time.timeScale` doesn't affect `Update()`. All fixes await a fourth
  Play Mode pass.

- **2026-08-18 12:32 CDT** — Fourth test: cursor drift still broken, and the loop report is
  now much more specific — nose freezes solid at vertical, no slow creep at all, directly
  contradicting the earlier simulation. Stopped trusting simulation alone for these two.
  Vendored the user's own telemetry tool (`Assets/AstroAces/ThirdParty/UTI`) and added
  `LoopDiagnostic.cs`, already wired onto the test rig — logs real per-physics-step
  AoA/bank/elevator/aileron/rates to CSV under `<project root>/UTI/BeanLogs/`. Next step is
  reading that CSV after the next test, not more Python. Also added
  `StarfieldPlaceholder.cs` (distant reference spheres, camera-centred) — requested since
  the blank sky made it hard to judge motion.

- **2026-08-22 (later session) — AA-004 and AA-007 closed by automated Play Mode tests, no
  human input needed.** New `Assets/AstroAces/Tests/PlayMode/` assembly, using the Input
  System's `InputTestFixture` to drive the real `Dogfight.unity` test rig with isolated
  virtual devices. Both tests pass — full detail in `BUGS.md` and `TESTS.md`.

- **2026-08-22 (same session, later) — AA-006 followed up with real-cursor UTI telemetry,
  also solo.** `InputTestFixture` can't help with AA-006 (no real OS cursor behind its
  virtual mouse), so reinstalled UTI fresh (v0.2.2) and built `CursorDriftDiagnostic.cs` to
  log the real cursor position every frame. Two solo runs (one hands-off, one deliberately
  warping the real cursor 170-360px away from centre three times) both show the cursor
  pinned within ~2px of centre almost the entire time. Turned out to be real evidence of the
  wrong thing — see the next entry.

- **2026-08-22 (same session, later still) — AA-006 actually root-caused and closed.** The
  automated evidence above never reproduced the bug (no real hardware mouse device behind
  it). Extended `LoopDiagnostic` to log mouse delta and aim error every tick, then had the
  user reproduce the bug three times for real. All three showed the same thing: a single
  corrupted mouse-delta reading exactly 2 physics ticks after the cursor lock engages,
  scaling with wherever the real cursor was resting before Play — up to the full 55 deg aim
  cone. Fixed with a minimal change (`AircraftInput` now discards mouse delta for 3 frames
  after every lock transition, not 1). User retested four times and confirmed it fixed.
  **Phase 3 is now fully done** — full mechanism and evidence in `BUGS.md` AA-006.

- **2026-08-22 (same session, even later) — Pulled a piece of Phase 14 forward: the loop
  actually works now, and a bug it exposed (AA-008) is closed too.** The user disagreed with
  treating the ~28s, visibly-stuck loop as acceptable. Simulated the exact control-law maths
  in Python first, found the real bottleneck (elevator authority fading all the way to 0),
  and added `AircraftConfig.elevatorStallFloor` (0.3) — cuts loop time to ~8-13s, verified
  numerically that every headline number (300/100/3,000/2.06°) is unchanged. The rest of
  Phase 14's tuning pass is still deferred; this was one targeted, well-evidenced exception.

  Fixing the loop exposed **AA-008**: the aim marker going stale and vanishing during a
  keyboard-only loop, since it's world-fixed and the mouse never moves. Two fix attempts (the
  first one over-corrected and blocked real mouse input, caught immediately by the user);
  the user then reported a residual "stuck when upside-down" symptom, which a dedicated
  16-second full-loop telemetry test traced to the **Phase 2 placeholder camera** (rolls
  rigidly with the aircraft, flips the whole screen when inverted) rather than the aim code
  itself (confirmed clean: max 1.3° deviation, 0.0° at dead vertical). AA-008 closed; the
  camera finding is folded into Phase 4's section above as a heads-up. Two new PlayMode tests
  added, all four now passing. Full story in `BUGS.md` and `DESIGN.md` Sec 2.6.

- **2026-08-22 (same session, final) — Phase 4 built solo, per the user's request to "see
  what you can do by yourself."** `Scripts/UI/ChaseCamera.cs` built to spec: exponential
  position/rotation smoothing (frame-rate independent, not a naive `rate * dt` Lerp),
  free-look with clamps and centre-return, Caps Lock zoom, correct clip planes and culling
  mask. Un-parented `Main Camera` from the Phase 2 rig-parenting hack and wired it up in
  `Dogfight.unity`. Deliberately decided (per the AA-008 heads-up above) to let the camera
  fully follow aircraft roll/inversion, relying on smoothing rather than an unproven up-vector
  clamp — see the Phase 4 section above and `ChaseCamera.cs`'s header comment for the full
  reasoning. Verified with two new PlayMode tests, a live screenshot, and direct position/
  rotation reads during a real Play session — one real environment finding along the way
  (a Play Mode transition that took ~30s longer than usual to start ticking before recovering
  on its own; logged in `TOOLING.md`, not a code bug). All 6 PlayMode tests pass. **Phase 4
  done — Phase 5 (HUD and crosshair) is next.**

- **2026-08-22 (same session, truly final) — Phase 5 built solo too.** `CrosshairTexture`
  (procedural ring+cross+hollow-centre reticle), `HudController` (top-left AOA/ALT/SPD/THR
  readout, raw values — Phase1DebugReadout's smoothing was a Phase-1-only workaround, not
  carried forward), `CrosshairController` (nose-fixed gunnery reticle + smaller
  `DesiredDirection` aim marker, hidden when behind the camera), `MessageLog` (2s linear
  fade, wired to `AircraftEngine.OnAirbrakeChanged`). Added `HudCanvasUtility` as a small
  shared helper, not in the original per-file spec, since three components each needing an
  overlay canvas made a shared builder worth it. Deleted both Phase 1/2 debug overlays and
  removed them from the rig — verified via live Play Mode reads that the real HUD showed
  correct values first, not just a passing compile check. All 9 PlayMode tests pass (3 new).

  **Phase 5 done. Phase 6 (World: ground, sky, clouds, play-area bounds) is next.**

- **2026-08-22 (same session, absolutely final) — Phase 6 built solo, the biggest phase yet.**
  Two hand-written URP shaders (`Toon.shader`: banded diffuse + rim, reusing URP's own
  ShadowCaster/DepthOnly HLSL rather than reinventing it; `SpaceSky.shader`: procedural
  gradient + hash-based stars + a faint nebula band) plus four scripts
  (`GroundBuilder` — 201x201-vertex displaced mesh, `RockScatter` — 400 rocks raycast onto
  the real terrain, `CloudField` — 40 collider-free cluster groups, `PlayAreaBounds` — soft
  push-back + `MessageLog` warning). Both shaders compiled clean on the first attempt after
  reading URP 17.3's actual `ShadowCasterPass.hlsl`/`DepthOnlyPass.hlsl` source first rather
  than guessing at the include contract.

  Replaced the Phase 0 placeholder ground and deleted `StarfieldPlaceholder.cs`. Verified
  live: rock/cloud counts and cloud collider-removal confirmed via `execute_code`, the sky
  gradient confirmed by force-rotating the real camera to look up (the screenshot tool's own
  `view_rotation` parameter does **not** move the live camera — a real dead end briefly
  worth flagging), `PlayAreaBounds` confirmed by a dedicated test after finding and fixing a
  real, generalizable Unity bug in the test itself: `transform.position` silently reverts on
  a non-kinematic Rigidbody's next `FixedUpdate` — must go through `rb.position`. Full
  writeup in `TOOLING.md`. All 10 PlayMode tests pass.

  **Phase 6 done. Phase 7 (Weapons: `Projectile`, `ProjectilePool`, `AircraftGun`) is next.**

- **2026-08-22 (same session, one more) — Swapped the test rig's capsule for a real
  placeholder aircraft mesh** (`Omega_fighterG/Meshes/fighter_black.FBX`, rough 1.5x scale,
  not the full verified-scale/recolour treatment) so Phase 7's muzzle transforms have real
  wing geometry to parent to. Also fixed `SimpleNaturePack`'s rocks rendering hot magenta
  under URP (Built-in-RP `Standard` shader) via a `rockMaterial` override in `RockScatter`.
  Full detail in `HANDOFF.md`. All 10 PlayMode tests still pass.

- **2026-08-22 (same session, later) — AA-009 and AA-010 found and closed, both in free-look
  (see the note added to the Phase 4 section above and `BUGS.md` for full mechanism).**
  AA-009: `AircraftAimController` had no gate on `FreeLookHeld`, so orbiting the camera with
  right-click also kept steering the aircraft. AA-010, immediate follow-up: free-look only
  ever panned the camera's *view* from a fixed spot, never actually moved its *position*
  around the ship. The new orbit test failed three times before passing — not because the
  camera fix was wrong (it wasn't, from the first attempt), but because of two real
  confounds in the test's own setup: the rig's flight speed swinging the orbit math wildly
  between irregular render frames (fixed by freezing the rig's Rigidbody for the test), and
  `Vector3.Lerp` dipping inside the orbit sphere mid-transition (fixed by settling briefly
  past the test's early-break point before measuring distance). All 12 PlayMode tests pass.
