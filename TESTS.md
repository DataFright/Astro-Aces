# Astro Aces — Tests

Edit Mode tests run without entering Play Mode, because `Aero` and `FlightControlLaw` are
pure static maths with no MonoBehaviour or scene dependency. That is deliberate: it is what
makes the headline flight numbers testable at all.

**Run:** Unity → Window → General → Test Runner → EditMode → Run All.
**Compile check (not a test, but run it first):** `Tools\compile-check.ps1`.

---

## Status

The **EditMode** assembly (pure `Aero`/`FlightControlLaw` maths) is **not created yet** —
that is still Phase 13 of `BUILD_PLAN.md`. The expected values below are already verified
numerically against the design maths, so they are ready to be asserted verbatim.

A **PlayMode** assembly exists ahead of schedule, added 2026-08-22 specifically to close out
AA-004/AA-007 without needing a human at the keyboard — see below.

---

## PlayMode assembly — `Assets/AstroAces/Tests/PlayMode/` (built ahead of Phase 13)

Added to prove, and then use, real solo-runnable Play Mode testing: loads the actual
`Dogfight.unity` scene and drives the real test rig's real components through
`InputTestFixture` — the Input System's own isolated-virtual-device test harness, which
severs the input system from real hardware for the duration of the test so a simulated held
key genuinely stays held (unlike poking `Keyboard.current` directly from `execute_code`,
which the real OS keyboard backend fights and overwrites within a couple of frames — tried
and confirmed this the hard way before finding `InputTestFixture`). Run via the MCP
`run_tests`/`get_test_job` tools, `mode: "PlayMode"`.

| Test | File | Assertion | Guards |
| --- | --- | --- | --- |
| `AA004_HeldRoll_StaysCommanded_NeverDecaysToZeroMidRoll` | `Phase3ControlLawPlayModeTests.cs` | holds D for 3 simulated seconds via a virtual keyboard; `state.Rates.rollRight` never drops below 30 deg/s, cumulative roll exceeds 300 deg | AA-004 (keyboard command cancelling out against a saturated mouse-restoring term) |
| `AA007_GamePaused_MouseMovement_DoesNotChangeAim` | `Phase3ControlLawPlayModeTests.cs` | sets `AircraftInput.GamePaused = true`, injects large synthetic mouse deltas, asserts `MouseDelta` stays zero and `AircraftAimController.DesiredDirection` never changes | AA-007 (`Time.timeScale` not stopping `Update()`, aim drifting while "paused") |
| `AimTracking_ExtremeInput_AndE_Hold_MouseInterrupt` | `AimTrackingPlayModeTests.cs` | a huge single-frame mouse flick and 20 rapid alternating-direction flicks both stay within the 55° cone with no NaN; a ~2s hands-off E-hold keeps the aim within 2° of the nose; a mouse nudge while E is *still* held measurably changes `DesiredDirection` | AA-008 (aim going stale during manual pitch, and the first fix's overcorrection blocking real mouse input during a hold) |
| `FullLoop_HandsOffE_AimStaysGluedThroughInvertedPortion` | `FullLoopAimPlayModeTests.cs` | holds E hands-off through a full 16s loop; angle-off-nose stays under 5° every tick, including exactly at the moment pitch crosses 90° (dead vertical) | AA-008 (verifies the aim-glue survives the loop's singular vertical/inverted portion — it does; the residual "looks stuck" report turned out to be the placeholder camera, not this) |
| `FreeLook_OrbitsWhileHeld_ReturnsToCentreOnRelease` | `ChaseCameraPlayModeTests.cs` | holding right mouse + injecting mouse delta visibly orbits the camera away from the aircraft's own facing; releasing decays it back to within 3° over 3s | Phase 4 (`ChaseCamera` free-look) |
| `Zoom_CapsLockTogglesFieldOfView` | `ChaseCameraPlayModeTests.cs` | starts at 60° FOV; one Caps Lock press toggles to 24°, a second toggles back to 60° | Phase 4 (`ChaseCamera` zoom) |
| `Hud_ReadoutShowsAllFourFields_InExpectedFormat` | `HudPlayModeTests.cs` | the readout text matches `AOA/ALT/SPD/THR` with the units/format DESIGN.md specifies | Phase 5 (`HudController`) |
| `Crosshair_GunneryReticle_VisibleDuringForwardFlight` | `HudPlayModeTests.cs` | the nose-fixed gunnery reticle is enabled while flying straight and level (directly in front of the camera) | Phase 5 (`CrosshairController`) |
| `MessageLog_ShowsOnAirbrakeToggle_ThenFadesToZero` | `HudPlayModeTests.cs` | toggling airbrakes shows "AIRBRAKES DOWN" at full alpha; alpha is back to 0 three seconds later | Phase 5 (`MessageLog`) |
| `OutsideBounds_PushesBackTowardCentre_AndShowsWarning` | `PlayAreaBoundsPlayModeTests.cs` | teleporting (via `rb.position`, not `transform.position` — see `TOOLING.md`) past the 2500m boundary shows "RETURN TO PLAY AREA" and measurably reduces the outward velocity within 2 physics steps | Phase 6 (`PlayAreaBounds`) |
| `FreeLook_MouseMovement_DoesNotChangeFlightDirection` | `AimTrackingPlayModeTests.cs` | holding right mouse + moving the mouse barely changes `DesiredDirection` (<0.5°); normal mouse-driven aim still works immediately after release | AA-009 (free-look silently steering the aircraft too) |
| `FreeLook_OrbitsAroundShip_NotJustRotatesInPlace` | `ChaseCameraPlayModeTests.cs` | freezes the rig's Rigidbody, then holding right mouse + mouse delta moves the camera's actual position (>3m), the camera ends up looking back at the ship (<15° off), and holds roughly the original orbit distance (within 30%, checked after a short settle window past the break condition) | AA-010 (free-look orbited its own fixed spot, camera position never actually moved around the ship) |

All twelve passed 2026-08-22 — see `BUGS.md` AA-004/AA-007/AA-008/AA-009/AA-010 and
`BUILD_PLAN.md`'s Phase 4/5/6 sections for full result detail.

**AA-006** (cursor drift, now CLOSED) needed two rounds of instrumentation to actually pin
down. `CursorDriftDiagnostic.cs` (real cursor position via `Mouse.current.position`, logged
solo) gave real evidence the steady-state warp mechanism works, but never reproduced the bug
— it had no real hardware mouse device behind it. The bug only showed up once
`LoopDiagnostic` was extended to also log `mouseDeltaX`/`mouseDeltaY`/`offNoseDeg` every tick
from true frame 0, and the user reproduced it three times for real: a single corrupted mouse
delta on the third physics tick after the cursor lock engages, scaling with pre-Play cursor
position. Fixed in `AircraftInput` (3-frame settle window after lock) and confirmed by a
fourth real retest. Full mechanism in `BUGS.md` AA-006. Neither diagnostic was built as a
standing `[UnityTest]` — both were one-shot investigations; worth converting the
`LoopDiagnostic` extension into a real regression test in Phase 13 if this area gets touched
again.

---

## Planned coverage

### FlightEnvelopeTests — guards the headline numbers

| Test | Assertion | Tolerance |
| --- | --- | --- |
| `TopSpeed_AtSeaLevel_Is300Mph` | `Aero.TopSpeedMps(cfg, 1f, 1f) × MpsToMph` ≈ 300.3 | ±5 mph |
| `TopSpeed_AtOverdrive_IsAbout315Mph` | throttle 1.1 ≈ 315.6 | ±5 mph |
| `TopSpeed_AtSpawnThrottle_IsAbout267Mph` | throttle 0.8 ≈ 266.7 | ±5 mph |
| `StallSpeed_AtCriticalAoA_Is100Mph` | `Aero.StallSpeedMps(cfg, 1f, criticalAoA)` ≈ 99.4 | ±5 mph |
| `StallSpeed_AtSafeAoA_IsAbout117Mph` | ≈ 116.9 | ±5 mph |
| `Ceiling_AtFullThrottle_Is3000Feet` | `Aero.CeilingMeters(cfg, safeAoA) × MetersToFeet` ≈ 2,986 | ±150 ft |
| `Ceiling_AtOverdrive_IsHigher` | 110% ceiling > 100% ceiling | — |
| `LevelFlight_AboveCeiling_IsImpossible` | `CanHoldLevelFlight` false at 1,100 m | — |
| `CruiseAoA_AtTopSpeed_IsSmall` | ≈ 2.06° | < 4° |

> These are the tests that matter most. Each one fails loudly if someone "just tweaks"
> `liftCoefficient`, `dragCoefficient`, `maxThrust`, `massKg` or `densityScaleHeight`.

### AeroTests — force maths

| Test | Assertion |
| --- | --- |
| `Density_AtSeaLevel_IsOne` | `DensityAt(0)` == 1 |
| `Density_Decreases_WithAltitude` | monotonically decreasing |
| `Density_NeverFallsBelowMinimum` | ≥ `minDensity` at 50 km |
| `AngleOfAttack_NoseAboveFlightPath_IsPositive` | local velocity (0, −1, 10) → positive AoA |
| `AngleOfAttack_IsZero_WhenStationary` | guards the divide-by-zero path |
| `LiftCurve_IsOne_AtSafeAoA` | `Cl(14°)` == 1.0 exactly |
| `LiftCurve_PeaksAtCriticalAoA` | `Cl(20°)` ≈ 1.43, higher than any other sampled AoA |
| `LiftCurve_CollapsesPastStall` | `Cl(45°)` ≈ 0.35 |
| `LiftCurve_IsSymmetric` | `Cl(−x)` == `−Cl(x)` — inverted flight works |
| `AoALimiter_IsOne_BelowSafe` / `IsZero_AtCritical` | stall-protection ramp |
| `SideDrag_OpposesSideslip` | force x-component opposes `localVelocity.x` |
| `SpeedFactor_NeverBelowArcadeFloor` | ≥ `minimumArcadeAuthority` at zero airspeed |

### FlightControlLawTests — the sign conventions

These exist because a sign error here reads as "the physics is broken" and costs hours.

| Test | Assertion |
| --- | --- |
| `TargetToTheRight_CommandsRightBank` | `+x` local target → positive `desiredBankDeg` |
| `TargetAbove_CommandsNoseUp` | `+y` local target → positive elevator |
| `HorizontalError_DoesNotDominateRudder` | rudder ≪ aileron for the same lateral error |
| `BodyRates_RoundTrip` | `FromUnity(ToUnity(r))` == `r` for all three axes |
| `BodyRates_RollRight_IsNegativeUnityZ` | pins the convention in `DESIGN.md` §2.8 |
| `AoALimiter_BlocksWorseningPull_AllowsRecovery` | at critical AoA, nose-up is cut, nose-down is not |
| `StepRates_ConvergesToTarget` | reaches `command × maxRate`, never overshoots |
| `StepRates_RespectsAccelerationCap` | one step never exceeds `authority × dt` |
| `StepAim_DoesNotRecentre` | zero mouse delta leaves the direction unchanged |
| `StepAim_ClampsToCone` | a huge delta leaves the aim ≤ `maxAimConeAngle` off the nose |
| `StepAim_WritesClampBackToStoredAngles` | repeated huge deltas do not accumulate hidden error |

### CombatTests

| Test | Assertion |
| --- | --- |
| `SixHits_DestroyAnAircraft` | 6 × `damagePerHit` ≥ `maxHealth`, 5 × < it |
| `ProjectileInheritsAircraftVelocity` | muzzle velocity is aircraft velocity plus forward speed |
| `ProjectileSpeed_ExceedsAircraftTopSpeed` | rounds always outrun the shooter |
| `ProjectileStep_IsShorterThanAircraft` **or** swept-raycast covers it | tunnelling guard |

---

## Log

- **2026-08-17 15:10 CDT** — Created. Planned coverage defined with expected values taken
  from the verified flight-model derivation; no tests implemented yet (Phase 13).

- **2026-08-22 (later session) — Added the PlayMode assembly ahead of schedule.** Built to
  answer a direct challenge that solo, human-free Play Mode testing should be possible with
  an MCP bridge into Unity — it is, via the Input System's `InputTestFixture`, not via poking
  the live device. Two tests added and passing, closing AA-004 and AA-007. Full story in
  `BUGS.md`'s 2026-08-22 log entry. The planned EditMode assembly (`FlightEnvelopeTests`,
  `AeroTests`, `FlightControlLawTests`, `CombatTests` below) is still Phase 13, unchanged.

- **2026-08-22 (same session, later still) — Two more PlayMode tests, both closing AA-008.**
  Added while fixing and verifying the loop-completion tuning change and the crosshair
  staleness bug it exposed. `FullLoopAimPlayModeTests` in particular was the test that
  actually settled a live disagreement about root cause — it proved the aim-tracking code was
  clean through the loop's vertical/inverted portion (max 1.3° deviation, 0.0° at dead
  vertical), redirecting the investigation to the real cause (the Phase 2 placeholder
  camera). All four PlayMode tests pass as of this entry.

- **2026-08-22 (same session, final) — Two more for Phase 4's `ChaseCamera`.** First
  free-look assertion was too strict (2° within 1s) and failed for a real, understood reason
  — two smoothing stages (the free-look decay and the camera's own rotation Slerp) stack, so
  full settling takes longer than either alone suggests. Loosened the wait and threshold
  rather than the camera's actual behavior, since the underlying decay was working correctly,
  just slower than the first guess. All six PlayMode tests pass.

- **2026-08-22 (same session, truly final) — Three more for Phase 5's HUD.** Added
  `Unity.TextMeshPro` and `UnityEngine.UI` to `AstroAces.Tests.PlayMode.asmdef`'s references
  — the test assembly hadn't needed either until these tests started reading
  `TextMeshProUGUI`/`RawImage` state directly. All nine PlayMode tests pass.

- **2026-08-22 (same session, actually final) — One more for AA-009, free-look silently
  steering the aircraft.** `ChaseCamera` implemented free-look correctly on the camera side;
  `AircraftAimController` had no matching gate and kept feeding the same mouse delta into
  steering the whole time — a Phase 2 gap invisible until Phase 4 gave `FreeLookHeld` its
  first real consumer. Test confirms both the freeze while held and that normal aim still
  works immediately after release. All eleven PlayMode tests pass.

- **2026-08-22 (same session, absolutely final) — One more for Phase 6's `PlayAreaBounds`,
  and a real bug found writing it.** First two attempts failed with the warning message
  reading `null` — turned out the test's teleport (`transform.position`) was silently
  reverting every `FixedUpdate` because the rig is a non-kinematic Rigidbody, confirmed by a
  temporary `Debug.Log` traced straight from `PlayAreaBounds.FixedUpdate()` itself rather
  than continuing to guess at the test. Fixed by going through `rb.position`/`rb.rotation`
  instead — full writeup in `TOOLING.md`, since it's a generalizable Unity gotcha, not
  specific to this one test. All ten PlayMode tests pass.

- **2026-08-22 (same session, one more still) — One more for AA-010, free-look orbiting its
  own spot instead of the ship.** `FreeLook_OrbitsAroundShip_NotJustRotatesInPlace` failed
  three times before passing, each failure in the test's own setup rather than the camera
  fix: the rig's flight speed (~114 m/s) against the ~8.5m orbit radius let the ship move
  farther between irregular render frames than the whole camera-to-ship distance, so froze
  the rig's Rigidbody for the test to remove that confound; then a `Vector3.Lerp`-along-a-
  chord dips inside the orbit sphere mid-transition, which the test's early-break condition
  was catching before the position finished settling — added a short real-time settle window
  after the break. Full writeup in `BUGS.md` AA-010. All twelve PlayMode tests pass.
