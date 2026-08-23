# Astro Aces — Design

Source of truth for mechanics, exact values, and *why* each value is what it is.
When code and this document disagree, one of them is a bug. Fix both.

---

# 1. Pillars

1. **Believable underneath, arcade on top.** Real forces do the moving; assistance quietly
   keeps the player from fighting the simulation. Never the reverse.
2. **Emergent, not clamped.** Top speed, stall speed and ceiling all fall out of the force
   equations. There is no `Mathf.Clamp` on speed or altitude anywhere in the project, and
   adding one is a design regression.
3. **Manual manoeuvres.** An aileron roll happens because the player rolled. No button
   performs a scripted trick.
4. **Not a spaceship.** Sideways motion is punished hard. Turning means banking.
5. **Light-hearted.** Star Fox tone. Goofy characters come later; nothing in the foundation
   should make that harder.

## 1.1 The three layers

```
   INTENT                CONTROLLER                  PHYSICS
   mouse / AI      ->    bank, pitch, yaw      ->    forces, rates
   "fly there"           "here's how"                "here's what happened"

   AircraftAimController  FlightControlLaw           Aero + AircraftPhysics
   EnemyPilot             (pure static)              (Rigidbody)
```

The layers talk in one direction only. Intent never touches a Rigidbody. Physics never
reads input. The controller does not know whether a human or the AI is flying — which is
why the enemy gets the player's exact flight model for free.

---

# 2. Flight model derivation

Every number below was solved for, then verified numerically before being committed.
Aircraft mass is **1,000 kg** and gravity is Unity default **9.81 m/s²**.

## 2.1 Forces

| Force | Formula | Notes |
| --- | --- | --- |
| Thrust | `forward × throttle × maxThrust × ρ` | Scales with air density |
| Drag | `−v̂ × ρ × dragCoefficient × v²` | × `airbrakeDragMultiplier` when braking |
| Lift | `up × ρ × liftCoefficient × v² × Cl(AoA)` | Along the aircraft's **up** axis |
| Side drag | `−right × ρ × sideSlip × v × sideDragCoefficient` | The anti-spaceship force |
| Gravity | Unity's own | Not applied manually |

`ρ` is air density as a fraction of sea level.

**There is deliberately no induced-drag term.** Lift acts along the aircraft's up axis, so
as angle of attack rises the lift vector tilts backwards and its rearward component *is*
induced drag. That is why a hard turn bleeds energy, and it is one fewer coefficient to
mistune. Do not add an explicit induced-drag coefficient.

## 2.2 Solving for top speed — 300 mph

At top speed thrust equals drag plus the induced component:

```
maxThrust = dragCoefficient · v²  +  m·g·tan(AoA_cruise)
```

Target `v = 134.1 m/s` (300.0 mph). Picking `maxThrust = 15,000 N` gives a thrust-to-weight
of 1.53 — powerful but not silly — and solving for drag gives
**`dragCoefficient = 0.813`**. Verified: **300.3 mph** at 100% throttle, **315.6 mph** at
110%, **266.7 mph** at the 80% spawn throttle.

## 2.3 Solving for stall — 100 mph

Level flight needs `L·cos(AoA) = m·g`. At peak lift (critical AoA, `Cl = 1.43`):

```
liftCoefficient = m·g / (v_stall² · Cl_peak · cos(AoA_crit))
```

With `v_stall = 44.7 m/s` this gives **`liftCoefficient = 3.7`**.
Verified: **99.4 mph** at critical AoA, **116.9 mph** at safe AoA. Because stall protection
holds the aircraft near safe AoA, the speed the player actually feels the aircraft start to
mush is **≈ 105–117 mph** — matching "begins drifting and falling out of the air below
about 100 mph".

Cruise AoA at top speed comes out at **2.06°**, and maximum sustained turn is **6.8 g** —
both sane for a fighter, which is the sanity check that the coefficients are not merely
fitted to two data points.

## 2.4 Solving for the ceiling — 3,000 ft

Air density falls exponentially: `ρ(h) = exp(−h / densityScaleHeight)`, floored at
`minDensity = 0.02`.

The ceiling is **thrust-limited, not lift-limited** — the same as a real jet. Climbing
costs density, which costs thrust, while the induced drag term `m·g·tan(AoA)` does *not*
shrink with density. Eventually thrust can no longer cover drag at any AoA.

> **This is the error worth remembering.** The first derivation assumed thrust and drag
> both scale with `ρ`, so top speed would be altitude-independent and the ceiling would be
> where lift ran out. That predicted 2,978 ft. Simulating it gave **1,837 ft** — because
> induced drag is density-independent and eats the shrinking thrust budget first. The fix
> was `densityScaleHeight = 780` (not 480). Hand-derive, then *always* verify numerically.

Solving numerically for a 3,000 ft ceiling gives **`densityScaleHeight = 780`**.

## 2.5 Verified flight envelope

| Altitude | Density | Top speed | Stall speed |
| --- | --- | --- | --- |
| 0 ft | 1.00 | 300.3 mph | 99.4 mph |
| 820 ft | 0.73 | 296.8 mph | 116.7 mph |
| 1,640 ft | 0.53 | 289.7 mph | 137.0 mph |
| 2,461 ft | 0.38 | 272.4 mph | 160.8 mph |
| 2,953 ft | 0.32 | 241.1 mph | 177.0 mph |
| 3,281 ft | 0.28 | — no level flight — | 188.7 mph |

**Ceiling: 2,986 ft at 100% throttle, 3,232 ft at 110%.** The envelope visibly pinches shut
as the player climbs — top speed falling while stall speed rises — so running out of sky
*feels* like thinning air rather than like hitting a wall. A ballistic zoom-climb can still
carry the aircraft above the ceiling briefly, which is correct and should not be prevented.

## 2.6 Lift curve and stall protection

`Cl(AoA)` is linear to critical, then collapses:

| AoA | Cl | Meaning |
| --- | --- | --- |
| 0° | 0.00 | |
| 14° (`safeAoA`) | 1.00 | Fully controllable |
| 20° (`criticalAoA`) | 1.43 | Peak lift; elevator authority now zero |
| 45° (`postStallAoA`) | 0.35 | Fully stalled, mushing |

Symmetric for negative AoA so inverted flight works.

The AoA limiter fades elevator authority from 1.0 at safe toward a floor at and past
critical (`cfg.elevatorStallFloor`, default **0.3**, not 0.0), **but only on the command
that would make `|AoA|` worse**. Pushing away from a stall always works, so the aircraft is
always recoverable. The player therefore cannot stall by pulling — but can absolutely fall
out of the sky by flying too slowly, which is the intended failure mode.

**Why a floor and not a hard 0** (added 2026-08-22, see the log entry below): with authority
truly hitting 0 at `criticalAoADeg`, a full-throttle full-pull loop took **~28 simulated
seconds** — the aircraft settles into a stable equilibrium right at the edge of the fade
band (residual authority ~10-15%) and crawls there for 10+ seconds before gravity/speed
dynamics eventually let it complete. To a real player that's indistinguishable from
"broken" — see `BUGS.md`'s loop entries. The floor is deliberately a **control-law** knob,
not an **aerodynamic** one: `Aero.LiftCurve` still fully collapses to `postStallLift` at
`postStallAoADeg` (45°) exactly as before, and nothing about `liftCoefficient`,
`dragCoefficient`, `safeAoADeg` or `criticalAoADeg` changed — none of the verified
300/100/3,000/2.06° headline numbers touch `AoALimiter` at all, confirmed unchanged
numerically after this change (300.3 / 99.4 / 2,986 exactly, same as before).

## 2.7 Rotation: rate control, not torque

Rotation is driven by setting angular velocity, approached under an acceleration cap:

```
targetRate = command × maxRate × speedFactor
accel      = clamp((targetRate − currentRate) × damping, ±authority × speedFactor)
newRate    = currentRate + accel · dt
```

**Why not `AddRelativeTorque`:** torque results depend on the Rigidbody's inertia tensor,
which Unity computes from collider shape. A collider tweak would then silently retune the
whole handling model. Rate control is deterministic, cannot oscillate, honours `maxRate`
exactly, and is testable as pure maths. On death the aircraft stops overwriting angular
velocity so the wreck tumbles under real physics.

| Axis | Max rate | Authority | Damping | Response |
| --- | --- | --- | --- | --- |
| Roll | 200 °/s | 1400 °/s² | 6.0 /s | τ ≈ 0.17 s — snappy, rolls feel crisp |
| Pitch | 60 °/s | 400 °/s² | 5.0 /s | τ ≈ 0.20 s |
| Yaw | 25 °/s | 150 °/s² | 4.0 /s | Deliberately weak — turns are flown with bank |

## 2.8 Sign conventions

Unity's local angular velocity disagrees with pilot intuition on two of three axes. Getting
this wrong produces an aircraft that rolls *away* from the mouse, which reads as broken
physics when it is one minus sign. Convert **once**, in `BodyRates`, and never inline it.

| Pilot term | Unity local angular velocity |
| --- | --- |
| roll right (right wing down) | `−z` |
| pitch up (nose rising) | `−x` |
| yaw right (nose right) | `+y` |

Bank angle is **positive when the right wing is down**, computed against
`Cross(Vector3.up, forward)` and negated. It is degenerate with the nose straight up or
down, so the previous value is held through the singularity.

## 2.9 Speed-based authority

```
speedFactor = max( InverseLerp(20 m/s, 70 m/s, airspeed), 0.35 )
```

At stall speed the aircraft still has ≈ 49% authority; at a dead stop, 35%. The floor is
what keeps a low-speed recovery possible instead of hopeless.

## 2.10 Control law summary

| Step | Rule |
| --- | --- |
| Errors | `horizontalError = atan2(localTarget.x, z)`, `verticalError = atan2(localTarget.y, z)` |
| Bank | `desiredBank = clamp(horizontalError × 2.5, ±80°)` — horizontal error commands **bank**, never yaw |
| Aileron | `rollError × 0.025 − rollRate × 0.004`, clamped ±1 |
| Elevator | `(verticalError × 1.0 + |bank| × 0.12) × 0.05 − pitchRate × 0.008`, clamped ±1 |
| Roll gate | Elevator × `max(cos(rollError), 0.35)` — bank first, then pull |
| AoA limiter | Applied to elevator, worsening direction only |
| Rudder | `horizontalError × 0.01 − yawRate × 0.01 + sideSlip × 0.10`, clamped ±1 |

`turnCompensationStrength = 0.12` adds back-pressure proportional to bank so the nose does
not drop in turns. It deliberately recovers *most* but not all of the lost altitude —
altitude holding stays the player's job.

## 2.11 Airbrakes and throttle

Airbrakes multiply drag by **2.2**. Verified bleed time from top speed to stall:
**18.5 s clean, 8.4 s with airbrakes** — noticeably faster, nothing like an instant stop.

Throttle spawns at **80%**, ranges **0–110%**, moves at **0.6/sec** on W, and steps **0.05**
per mouse-wheel notch. S uses a **0.01** fine step for the small trims the brief calls for.

## 2.12 Manual override — replace, don't add

Keyboard roll/pitch (A/D, E/Q) **override** their axis's computed command rather than
adding to it. The first version added them, per the original plan — that broke both manual
maneuvers it exists to enable, found by the user actually flying it (AA-004, AA-005 in
`BUGS.md`):

- **Roll:** `Compute` clamps its own aileron term to ±1 internally. As the aircraft rolls
  away from wherever the mouse is currently pointing, that term grows and saturates at the
  clamp. Add the keyboard's own ±1 on top of an already-saturated opposite value and it
  nets to zero — the roll stops dead at a repeatable point instead of completing. Fix:
  `cmd.aileron = input.RollAxis` outright when roll is held. No limiter applies to roll, so
  this is a clean full override.
- **Pitch:** still routes through `ApplyAoALimiter` even under manual control — stall
  protection (§2.6) is a blanket promise, not a mouse-only one; holding E must not let the
  player force a stall the mouse never could.
- **Cross-axis, pitch → aileron:** mouse aim is capped at ±`maxAimPitch` (80°) and never
  rotates with the aircraft. A manual pitch (a loop) carries the nose past that cap, so the
  local target ends up *behind* the aircraft. `horizontalError`'s forward-component floor
  (§2.10) then saturates the bank term toward ±`maxBankAngle` — correct behavior for "chase
  a target that's now behind me," wrong here, since the player never asked to turn; it's a
  geometry artifact of the fixed aim cone. Left alone, that stolen bank silently diverts
  part of the loop's rotation, which reads as "climbs fine, then visibly veers instead of
  continuing over the top" — exactly what got reported. Fix: while pitch is held and roll
  isn't, `cmd.aileron = 0`. Manual roll has no equivalent problem (pitch's cross-axis
  interference is *damped* by `rollAlignmentFloor`, not saturated), so only pitch suppresses
  the other axis.

A loop is therefore achievable, but — same as real War Thunder Air Arcade — needs enough
speed to stay under the AoA limit through the maneuver. Holding E at low energy will mush
rather than complete a clean loop. That is the intended stall model working, not a bug.

**Confirmed by a full 3D closed-loop simulation, not assumed:** with the current
`liftCoefficient`/`maxThrust`/`dragCoefficient`, a loop is right at the edge of the
aircraft's energy budget even flown well. At spawn speed (~256 mph) and even a gentle
15–35% pull at 110% throttle, a loop takes 10+ seconds, climbs 400 m → 1,000+ m, and bleeds
speed down to a near-stall crawl before (maybe) completing. Holding full elevator overshoots
AoA past 90° within ~2 seconds and stalls out inverted. This is a tuning/feel characteristic
of the current thrust-to-weight and lift coefficient, not a control-law defect — the control
law was verified separately to behave correctly (aileron stays exactly 0 during a pitch-only
maneuver; the bank angle reading flipping to ±180° partway through is a harmless coordinate
artifact of pitching straight through vertical with level wings, not an actual roll). If
loops should feel easier/snappier, that's a Phase 14 tuning decision — raising
`liftCoefficient` or `maxThrust` — not something to change unilaterally here, since both are
load-bearing for the verified 300/100/3,000 top-speed/stall/ceiling targets in §2.

---

# 3. Triage — read before changing a number

| Symptom | Layer at fault | Change |
| --- | --- | --- |
| Wobbles / oscillates | Controller | Raise `rollDamping`/`pitchDamping`, or lower `rollKp`/`pitchKp` |
| Turns like a spaceship | Physics | Raise `sideDragCoefficient`, raise `bankGain`, lower `yawAuthority` |
| Sluggish turns | Controller | Raise authority or Kp |
| Snaps too aggressively | Controller | Lower authority/Kp, raise damping |
| Loses altitude in turns | Physics/assist | Raise `liftCoefficient` or `turnCompensationStrength` |
| Unflyable when slow | Assist | Raise `minimumArcadeAuthority` |
| Mouse feels wrong | **Controller, not input** | Fix the control law before touching sensitivity |
| Aircraft chases a ghost direction | Intent | Aim cone clamp is not writing back into `aimYaw`/`aimPitch` |
| Rolls away from the mouse | Physics | A sign convention — see 2.8 |

---

# 4. Controls

| Input | Action |
| --- | --- |
| Mouse move | Rotate the persistent desired flight direction. **Never auto-centres.** |
| Mouse wheel | Throttle ±5% per notch |
| Left click | Fire (hold = continuous, 20/sec) |
| Hold right click | Free-look — moves the camera only, never the flight direction |
| W / S | Throttle up / down (S in ~1% fine steps) |
| A / D | Roll left / right (A raises the right wing, D raises the left) |
| E / Q | Pitch up / down |
| F | Toggle airbrakes (shows an on-screen state message) |
| Caps Lock | Toggle 2.5× zoom (FOV 60 → 24) |
| Esc | Pause / restart / quit menu (Phase 11). **Stopgap now:** `TempPauseToggle` freezes `Time.timeScale` and shows "PAUSED" — no restart/quit buttons yet. |

Mouse aim is world-referenced and clamped to a **55° cone** around the nose, so a fast flick
cannot leave the aim behind the aircraft. The clamp is written back into the stored yaw and
pitch — without that, aim error accumulates invisibly and the aircraft chases a direction
the player cannot see.

Keyboard roll/pitch **override** the mouse-driven control law on their axis rather than
adding to it (§2.12) — this is what makes manual aileron rolls and loops possible without a
scripted-manoeuvre button. An earlier additive version looked right on paper but silently
capped both maneuvers partway through; see `BUGS.md` AA-004/AA-005.

---

# 5. Camera

Third-person chase, **6 m back and 2 m up** (tightened from 8/3 on 2026-08-23 — see the log),
smoothed follow (position lerp ≈ 12/s, rotation ≈ 10/s) so it trails the aircraft rather than
sticking rigidly to it. Runs in `LateUpdate`; the Rigidbody uses interpolation, so anything
earlier judders.

- **Free-look:** holding right click orbits the camera around the aircraft up to ±120°
  yaw / ±70° pitch — the camera's **position** swings around the aircraft to a new vantage
  point (not just a look-direction pan from the fixed chase spot; see `BUGS.md` AA-010, where
  the first implementation got this distinction wrong). The orbit distance is **4.5 m** —
  deliberately tighter than the chase distance, not the same (see the 2026-08-23 log entry):
  free-look's job is close inspection of the ship, not just looking around from the chase
  spot. Releasing returns it over ~0.25 s. Flight direction is untouched (see `BUGS.md` AA-009
  for the matching mistake on that side).
- **Zoom:** Caps Lock toggles FOV 60 → 24 (2.5×).
- **Clip planes:** near 0.3, far **12,000** — the play area is 5 km across and the sky must
  not clip.

---

# 6. Weapons and damage

| Value | Setting |
| --- | --- |
| Fire rate | 20 rounds/sec (0.05 s interval, accumulator in `Update`) |
| Muzzle speed | 500 m/s, **added to the aircraft's own velocity** |
| Damage | 5 per hit |
| Health | 30 HP → 6 hits to kill |
| Lifetime | 2 s ≈ 1,260 m of reach |
| Crosshair distance | 500 m |

**Projectiles must use a swept raycast, not a collider.** At ~630 m/s a round travels 12.6 m
per physics tick and would tunnel straight through a 10 m aircraft. Each tick, raycast from
the previous position to the new one against `Aircraft | Ground`. This is not an
optimisation, it is a correctness requirement.

Aircraft also collide with each other — a mid-air collision is lethal to both.

---

# 7. HUD

**Top-left:** angle of attack, altitude in feet, speed in mph, throttle %.
**Top-right:** minimap.
**Centre:** crosshair.
**Centre-low:** transient messages (airbrake state, out of bounds).

## 7.1 Crosshair

A **gunnery reticle** projected from the aircraft 500 m along its nose, so it genuinely
shows where rounds go, plus a smaller **aim marker** on the mouse-driven desired direction —
exactly the War Thunder split between "where the guns point" and "where you asked to go".

Circle with a cross and a hollow centre, slightly translucent (α ≈ 0.75). The textures are
**generated procedurally at runtime** so the project needs no crosshair art asset. Hide the
reticle when its screen point is behind the camera.

## 7.2 Minimap

North-up orthographic camera looking straight down, following the player's XZ, rendering to
a `RenderTexture` created in code, shown in a `RawImage`. It culls to the **MinimapIcon**
layer only, so it draws icons rather than the world. Each aircraft carries a small flat
quad on that layer; the player's is an arrow that rotates with heading. Its purpose is
finding the enemy — nothing else needs to appear on it.

---

# 8. Enemy AI

Foundation level, one enemy. The enemy **feeds a desired direction into the same flight
model the player uses** — it does not get its own movement code, and it is therefore bound
by the same stalls, ceilings and energy losses.

| State | Behaviour |
| --- | --- |
| **Patrol** | Random waypoints inside the play area, 300–800 m up. New waypoint on arrival or every ~15 s. |
| **Detect** | Hidden vision cone: **45° half-angle, 1,500 m range**, needs line of sight (raycast against Ground). |
| **Pursue** | Turns toward the player and closes. |
| **Attack** | Fires when within **700 m** and the target is within **8°** of the nose, aiming at a **lead-predicted intercept** rather than at the player directly. |
| **Lost** | Keeps hunting for **8 s** after the player leaves the cone, then back to Patrol. |

---

# 9. World and art

- **Play area:** 5 km × 5 km. Soft boundary — a warning message and a turn-back nudge, not
  an invisible wall.
- **Ground:** a procedurally generated subdivided plane with layered Perlin displacement,
  **±25 m** of relief, on the Ground layer with a MeshCollider. Rocks from
  `SimpleNaturePack` (`Rock_01`–`Rock_05`) scattered and scaled 20–80× for rocky terrain.
- **Sky:** custom skybox shader — vertical gradient from deep purple at the horizon to
  near-black blue at zenith, procedural stars, faint nebula band.
- **Clouds:** clusters of 4–8 jittered low-poly spheres with a flat toon material, **no
  colliders**, at 300–800 m. They must never affect flight.
- **Aircraft:** `Omega_fighterG/Meshes/fighter_black.FBX` for both sides, recoloured —
  player **grey/green**, enemy **black/red**. Scale so the wingspan reads ~10 m.
- **Shading:** one hand-written URP toon shader (banded diffuse + rim light), used for
  aircraft, terrain and clouds. Shader Graph is deliberately avoided — its assets are
  unreadable and unmergeable as text.
- **Spawn:** player at 400 m (1,312 ft), 80% throttle, straight and level, **with initial
  velocity already set to the analytic trim speed** (`Aero.TopSpeedMps` at spawn altitude
  and throttle — ≈267 mph at sea-level-equivalent density for 80%), not spawned at rest.
  This is load-bearing, not cosmetic: lift needs airspeed² to mean anything and gravity
  needs none, so an aircraft that spawns motionless free-falls for a fraction of a second
  before it has enough speed for lift to matter — by which point the angle between its nose
  and its now-bending flight path has already blown past stall. Confirmed by simulation,
  see `BUGS.md` AA-002. Applies to every spawn point, player or enemy — nothing should ever
  spawn motionless in mid-air.

---

# 10. Audio

Wired now, **silent now**. `AudioDirector` sets `AudioListener.volume = 0` at boot and
exposes a single `Muted` flag; unmuting later is a one-line change.

Needed: gunfire, projectile impact, explosion, aircraft destruction, engine loop (pitched
by throttle).

The pack `Casual Game Sounds U6` has 50 clips named `DM-CGS-01`…`50` with no descriptive
names, so which clip is which cannot be determined without listening. Clip assignment is
therefore an explicit **later task**, not a guess — see `HANDOFF.md`.

---

# 11. Verified environment constraints

Confirmed against this machine on 2026-08-17, not assumed. Each of these is a mistake an
unwary implementation will make.

| Constraint | Consequence |
| --- | --- |
| `activeInputHandler: 1` — **New Input System only** | `UnityEngine.Input.GetKey/GetAxis` throws at runtime. Use `Keyboard.current` / `Mouse.current`. |
| **C# 9.0**, netstandard2.1 | No file-scoped namespaces, global usings, records-with-primary-constructors, or `required`. |
| Unity 6 Rigidbody renames | `linearVelocity` (not `velocity`), `linearDamping`, `angularDamping`. |
| `FindObjectOfType` obsolete | Use `FindFirstObjectByType` / `FindObjectsByType(..., FindObjectsSortMode.None)`. |
| Input System mouse delta | Already per-frame. **Do not multiply by `Time.deltaTime`.** |
| Input System scroll | Returns ±120 per notch on Windows. Divide by 120. |
| TextMeshPro | Ships inside `com.unity.ugui` 2.0.0. Available, no package install needed. |
| Default shadow distance 50 m | Far too short for a 5 km map. `ProjectSetup` raises it to 400 m. |
| Fixed timestep 0.02 s (50 Hz) | Fine for flight. Gun timing must use `Update`, not `FixedUpdate`. |
| Tags/layers were empty | Layer **indices are contractual** — see `Core/Layers.cs`. Created by `Astro Aces > Setup Project`. |
| `CursorLockMode.Locked` doesn't reliably pin the OS cursor to centre, especially in the Editor's Game view | Delta computed relative to a drifted "centre" inherits that error persistently, not just on the first frame — a one-frame suppression does **not** fix this (confirmed by testing). `AircraftInput.SetLocked` actively `WarpCursorPosition`s to centre both synchronously on every lock transition and every frame while locked. See `BUGS.md` AA-006 (fix revised once already). |
| `Time.timeScale = 0` does not stop `Update()` | Only `FixedUpdate` and time-scaled effects respect it — input reading in `Update()` keeps running "while paused" unless explicitly gated. `AircraftInput.GamePaused` is that gate. See `BUGS.md` AA-007. |

---

## Log

- **2026-08-23 — §5 camera distances tightened, both retuned separately.** User: "not super
  happy with the camera position, its kinda far back and the hold right click to free view
  too look around and inspect your ship is still too far back to view it proper" — two
  distinct complaints, given two distinct fixes. (1) Default chase offset: 8m back/3m up →
  6m back/2m up, and `positionLerpPerSecond` 8 → 12 (also tightens the documented cruise-speed
  follow lag, `HANDOFF.md`'s Phase 4 entry — lag ≈ speed/lerp-rate, so ~14m → ~9.5m at cruise).
  (2) Free-look orbit: previously reused the chase offset's own magnitude (~8.5m before this
  change), which was never actually right for "inspect the ship up close" — gave it its own
  `freeLookOrbitDistance` field (4.5m) instead of deriving it from the chase distance, so the
  two can be tuned independently going forward. Verified two ways before handoff: a live
  screenshot via Unity MCP showing the ship's wing/canopy detail clearly at the new free-look
  distance (vs. barely resolvable before), and `ChaseCameraPlayModeTests`'s orbit-distance
  assertion, which had been checking the orbit stayed within 30% of the *chase* distance — no
  longer correct once free-look intentionally targets a different distance, so it now checks
  against the new `ChaseCamera.FreeLookOrbitDistance` property instead. All 12 PlayMode tests
  pass. Numbers are a first pass, not a final feel decision — waiting on the user's own
  in-editor read before calling this settled.

- **2026-08-22 (later still) — §5 clarified: free-look's orbit is a real position change, not
  just a look-direction pan.** The first `ChaseCamera` implementation composed the free-look
  offset into rotation only, so right-click could look behind you but the camera never
  actually left its spot behind the tail — user: "it orbits its camrea spot... it should also
  look aroudn the ship." Fixed (`BUGS.md` AA-010) by rotating the camera's *position* around
  the aircraft to a new vantage point at the same distance, then looking back at the ship
  from there. Made the distinction explicit in §5's text since the prose already said
  "orbits" before the fix existed — the ambiguity between "the view orbits" and "the camera
  orbits" is exactly what caused the bug.

- **2026-08-22 (Phase 14 work pulled forward) — Loop actually fixed, not just understood.**
  The user disagreed with treating the "can't complete a loop" behavior as acceptable and
  asked to fix it. Simulated the exact `AoALimiter`/`StepRates` maths in Python (planar loop,
  full elevator held the whole time, matching real play): the **unmodified** current physics
  genuinely does complete a full loop given enough patience — but takes **~28.6 simulated
  seconds**, with a ~10-12s stretch in the middle where the nose crawls forward at a rate no
  real player would wait out. Swept `criticalAoADeg`/`safeAoADeg`/`maxPitchRate`/
  `pitchAuthority`/`maxThrust` individually — none meaningfully shortened it (some made it
  worse or introduced wild tumbling), because the crawl is a stable feedback equilibrium, not
  a simple authority ceiling. Removing the AoA limiter entirely cut the time to ~3.6s but
  produced a 180° AoA snap-tumble, not a controlled loop — confirmed the limiter's fade
  *shape* was never the problem, only its floor of exactly 0.

  **Fix:** `Aero.AoALimiter` now takes an optional `floor` parameter (default 0, so any other
  caller is unaffected) and `AircraftConfig.elevatorStallFloor` (default **0.3**) feeds it at
  the one real call site in `FlightControlLaw.Compute`. Simulated sweep: floor 0.25 → ~12.5s,
  0.30 → ~10s (interpolated), 0.35 → ~8.0s. Picked 0.3 as a reasonable starting point — fast
  enough to feel achievable, not so fast it trivializes the maneuver. See §2.6 above for the
  full reasoning and why this doesn't touch the lift curve or any verified headline number
  (re-verified numerically after the change: 300.3 / 315.6 mph, 99.4 / 116.9 mph, 2,986 ft —
  byte-identical to before). **Not yet confirmed in real Play Mode** — the simulation is a
  simplified planar model (no bank, no real singularity handling), so the exact real-game
  loop time may differ; awaiting the user's test.

- **2026-08-17 15:10 CDT** — Created. Derived and numerically verified the full flight
  model: top speed 300.3 mph, stall 99.4 mph, ceiling 2,986 ft, all emergent from
  coefficients rather than clamped. Corrected an initial hand-derivation error in the
  ceiling (assumed lift-limited, is actually thrust-limited because induced drag does not
  scale with density) — `densityScaleHeight` moved 480 → 780. Chose rate control over
  torque control and documented why. Recorded verified Unity 6.3 environment constraints.

- **2026-08-17 16:02 CDT** — §9 Spawn updated: initial velocity must be set to the analytic
  trim speed, not zero. Found via the Phase 1 Play Mode test (first human-run test —
  aircraft nosed into a stall-dive within seconds instead of settling near 267 mph) and
  confirmed by simulation before touching code. See `BUGS.md` AA-002.

- **2026-08-18 11:37 CDT** — Added §2.12 (manual override is a replace, not an add — the
  first Phase 3 Play Mode test found that adding silently capped both rolls and loops
  partway through; see `BUGS.md` AA-004/AA-005) and corrected the now-stale "added on top
  of" line in §4. Added the cursor-lock first-delta gotcha to §11 (AA-006 — spawn direction
  was randomly kicked by wherever the mouse happened to be on screen before Play started).
  Noted the temporary Esc pause stopgap in §4's controls table ahead of the real Phase 11
  menu.

- **2026-08-18 12:01 CDT** — Revised the §11 cursor-lock entry: the one-frame delta
  suppression fix for AA-006 did not survive user testing, replaced with active
  `WarpCursorPosition` recentering (synchronous on lock + every frame). Added §11's
  `Time.timeScale`-doesn't-stop-`Update()` entry (AA-007 — pause wasn't freezing input).
  Added a §2.12 addendum: confirmed by full 3D simulation that a loop is right at the edge
  of the current energy budget even flown well — a tuning characteristic for Phase 14, not
  a control-law bug (which was separately verified correct).
