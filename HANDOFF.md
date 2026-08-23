# Astro Aces — Handoff

**Last updated:** 2026-08-23 (Phases 0-6 all done; Phase 7 next; camera distances retuned)

**Unity MCP is live.** `Window > MCP for Unity` (Ctrl+Shift+M), server running, auto-start
on, health check green, `claude mcp list` shows `UnityMCP: Connected`. Tools (scene control,
console reading, script editing, running tests) aren't available in whatever session set
this up, since MCP servers load at session start — **any new Claude Code session from here
on should have them.** Use them; don't keep falling back to screenshots/manual description
by default.
**Phase:** Foundation build in progress — Phases 0, 1, 2 and **3 are all fully done and
confirmed.** Phase 3's four bugs (AA-004 roll/pitch override, AA-005 manual pitch bank,
AA-006 cursor drift, AA-007 pause freezing aim) are all CLOSED — see `BUGS.md`. The loop
itself, previously deferred to Phase 14 as a "tuning characteristic," was fixed for real
2026-08-22 at the user's request (`AircraftConfig.elevatorStallFloor`, see `DESIGN.md` §2.6)
— a full loop now completes in ~8-13s instead of ~28s, with every verified headline number
(300/100/3,000/2.06°) unchanged. Fixing it exposed and closed one more bug, **AA-008**
(aim marker going stale during a keyboard-only loop) — its last reported symptom
("stuck when upside-down") turned out to be the Phase 2 placeholder camera rolling with the
aircraft, not the aim code, which is now the top of `BUILD_PLAN.md`'s Phase 4 section as a
heads-up. **Phases 4, 5 and 6 are now done too** — the real `ChaseCamera`, the real
HUD/crosshair/message log (both Phase 1/2 debug overlays now deleted), and the real world
(displaced ground, procedural sky, scattered rocks, drifting clouds, a soft play-area
boundary — `StarfieldPlaceholder.cs` now deleted too) all exist and are tested. Two more
autonomous-session bugs are closed too: **AA-009** (free-look silently steering the aircraft,
not just the camera) and **AA-010** (free-look orbiting its own fixed spot instead of
actually swinging the camera around the ship). Twelve PlayMode tests exist now
(`Assets/AstroAces/Tests/PlayMode/`), all passing — see `TESTS.md`.

**Phase 7 (Weapons: Projectile, ProjectilePool, AircraftGun) is next.**

---

## Status

The project was intentionally wiped and restarted on 2026-08-17. What exists now is the
**load-bearing core plus a full build plan**, deliberately front-loaded so the remaining
work is mechanical rather than derivational.

### Done

| Item | Where |
| --- | --- |
| Flight model derived **and numerically verified** | `DESIGN.md` §2 |
| Tuning values, all derived not guessed | `Scripts/Flight/AircraftConfig.cs` |
| Aerodynamics maths (pure, testable) | `Scripts/Flight/Aero.cs` |
| Control law + sign conventions (pure, testable) | `Scripts/Flight/FlightControlLaw.cs` |
| Layer index contract | `Scripts/Core/Layers.cs` |
| Tags/layers/settings installer | `Editor/ProjectSetup.cs` |
| Assembly definitions | `AstroAces.Runtime`, `AstroAces.Editor` |
| Offline compile check (~5 s) | `Tools/compile-check.ps1` |
| 14-phase implementation plan | `BUILD_PLAN.md` |
| Real chase camera (position/rotation smoothing, free-look, zoom) | `Scripts/UI/ChaseCamera.cs` |
| Real HUD (flight readout, gunnery/aim crosshair, fading messages) | `Scripts/UI/HudController.cs`, `CrosshairController.cs`, `MessageLog.cs` |
| Real world (toon shader, sky shader, displaced ground, rocks, clouds, play-area bounds) | `Shaders/Toon.shader`, `Shaders/SpaceSky.shader`, `Scripts/World/` |

All four runtime scripts pass the compile check against Unity 6000.3.21f1 reference
assemblies.

### Not done

Phases 7–14 of `BUILD_PLAN.md`: weapons, damage, enemy AI, minimap, menus, audio clip
assignment, the EditMode test assembly (Phase 13 — a PlayMode assembly already exists ahead
of schedule, see `TESTS.md`), and the final tuning pass. Phases 0–6 are all built and
confirmed (see `ARCHIVE.md` for the full session log) — the aircraft flies, turns from mouse
and keyboard input, can complete a loop, has a real smoothed chase camera, a real HUD, and a
real world (displaced ground, procedural sky, rocks, clouds, a soft play-area boundary).

---

## Onboarding — read in this order

1. **`README.md`** — the premise and the one design rule. 2 minutes.
2. **`DESIGN.md` §1** — the three layers (intent → controller → physics). Everything else
   depends on understanding that separation.
3. **`DESIGN.md` §11** — verified environment constraints. **Do not skip.** Nine of these
   ten items are mistakes that compile fine and fail at runtime, chiefly: this project is
   New-Input-System-only, so `UnityEngine.Input.GetKey` throws.
4. **`DESIGN.md` §2** — the flight model derivation, if you will touch a tuning value.
   Otherwise skim §2.5 (the envelope table) and §2.8 (sign conventions).
5. **`Scripts/Flight/Aero.cs`** then **`FlightControlLaw.cs`** — read the comments, not just
   the code. Roughly 300 lines total and they are the whole flight model.
6. **`BUILD_PLAN.md`** — then start at Phase 0.

Skip `DESIGN.md` §§4–10 until you reach the phase that implements them.

### The one thing to internalise

> Speed, stall and ceiling are **emergent from coefficients, not clamped**. There is no
> `Mathf.Clamp` on speed or altitude anywhere, and adding one is a design regression.
> If the aircraft flies wrong, change a coefficient — and expect an Edit Mode test to
> notice when you do.

---

## Next steps

**0. Health-check Unity MCP before trusting it for anything below.** It was confirmed
connected (`claude mcp list` → `UnityMCP: Connected`, Editor health check green) at
2026-08-21 14:52 CDT, in a session that couldn't use it yet (loaded after the tools list was
already fixed). Don't just take that as gospel — verify fresh, in whichever session reads
this first:
   - Confirm the tools actually showed up this session (search for them / try one) rather
     than assuming "connected" in the Editor means the *tools* are usable here too.
   - Read the Console through it and confirm it matches what a manual check would show —
     don't fully trust it blind on the first real use.
   - Try one Play Mode action (start/stop, or read a live GameObject's state) and sanity
     check the result against what you'd expect.
   - If anything's missing or wrong: don't silently fall back to the old screenshot-relay
     workflow and move on — that regresses exactly the pain point this was set up to fix.
     Say so, and either fix the connection or explicitly flag that MCP isn't usable this
     session before doing anything else.
   - If it all checks out: use it going forward instead of asking the user to screenshot or
     describe things — that's the entire point of having set it up.

1. ~~**Phase 0** — project setup.~~ Done, confirmed via `Astro Aces > Verify Project Setup`.
2. ~~**Phase 1** — aircraft flies on forces alone.~~ Done, confirmed in Play Mode
   (258–270 mph, shallow steady glide, matches the derived model). See `BUGS.md` AA-002 for
   the one real bug this caught (spawn velocity) before it could compound into later phases.
3. ~~**Phase 2** — intent layer (`AircraftInput`, `AircraftAimController`).~~ Confirmed
   clean in Play Mode, no issues found.
4. ~~**Phase 3.**~~ **Done, fully closed 2026-08-22.** Mouse-driven turning, AA-004, AA-005,
   AA-006 and AA-007 are all confirmed — AA-006 last, via real hardware telemetry and the
   user's own four-run retest. See `BUGS.md` AA-006 for the full mechanism if this area is
   ever touched again (a lock-transition mouse-delta echo, guarded by `AircraftInput`'s
   3-frame settle window).
5. ~~**Phase 4.**~~ **Done, 2026-08-22.** Real `ChaseCamera` built and tested — smoothed
   position/rotation follow, free-look, Caps Lock zoom, correct clip planes/culling mask. The
   AA-008 roll-following question is answered (full roll-following, relying on smoothing, not
   an up-vector clamp) — see `BUILD_PLAN.md`'s Phase 4 section for the reasoning if this ever
   needs revisiting.
6. ~~**Phase 5.**~~ **Done, 2026-08-22.** Real HUD built and tested — `CrosshairTexture`,
   `HudController`, `CrosshairController`, `MessageLog`. `Phase1DebugReadout.cs` and
   `Phase2DebugReadout.cs` are deleted, as planned.
7. ~~**Phase 6.**~~ **Done, 2026-08-22.** Real world built and tested — `Toon.shader`,
   `SpaceSky.shader`, `GroundBuilder`, `RockScatter`, `CloudField`, `PlayAreaBounds`.
   `StarfieldPlaceholder.cs` is deleted, as planned.
8. **Phase 7 is next** — Weapons: `Scripts/Combat/Projectile.cs` (swept raycast, not a
   collider — DESIGN.md Sec 6 is explicit this is a correctness requirement, not an
   optimisation), `ProjectilePool.cs`, `AircraftGun.cs`. Muzzle transforms need parenting
   under the wings, which means this phase needs a real aircraft mesh with wing geometry to
   parent to — the test rig now carries a rough placeholder mesh (see the "Aircraft model
   scale" row in Open Decisions below), which should be enough to proceed; the full
   verified-scale/recolour treatment is still separately deferred.
9. Continue in order from there. Each phase has an acceptance check; honour it.

---

## Working agreements

- Run `Tools\compile-check.ps1` after every file. It is 5 seconds and catches the entire
  class of Unity-6-API mistakes.
- Update `DESIGN.md` in the same session as any mechanic or value change, with a dated log
  line at the bottom. Same for `TESTS.md` and `BUGS.md`.
- Keep `Aero` and `FlightControlLaw` free of MonoBehaviour, scene and input dependencies.
  Their testability is the project's main quality guard.

---

## Open decisions and deferred work

| Item | Note |
| --- | --- |
| **Audio clip assignment** | The 50 clips in `Casual Game Sounds U6` are named `DM-CGS-01`…`50` with no descriptions. Which is gunfire vs. explosion cannot be determined without listening. Wire the events and `SoundBank` fields now, assign clips in a later pass. Audio stays **muted** meanwhile, per the brief. |
| **Music** | `Dynamic Music` pack is imported and unused. Out of scope for the foundation. |
| **Aircraft model scale** | A rough, unverified 1.5x scale of `Omega_fighterG/Meshes/fighter_black.FBX` is now visible as a child of the test rig (2026-08-22, replacing the capsule) — but it's a quick placeholder, not the real treatment. Still needed: verify it actually reads as a ~10 m wingspan, set the Rigidbody centre of mass sensibly, and do the grey/green vs. black/red player/enemy recolour. Has no phase number of its own in `BUILD_PLAN.md`'s 14 phases — this was never actually scheduled anywhere, just tracked here. |
| **Cel-shading depth** | Plan is one hand-written URP toon shader. If the look needs more later, that is a polish task, not a foundation one. |
| **Version control** | Set up 2026-08-22 — see the Log below. |
| **Characters / tone** | The goofy Star Fox cast is design work still to come. Nothing in the foundation should block it. |

---

## Log

Full session-by-session trace has moved to **`ARCHIVE.md`** — append new entries there once
they stop being current, not here. Kept here: current status (see the sections above) plus a
short rollup of the most recent history. See `CLAUDE.md`'s Documentation Structure for the
archiving policy going forward.

- **2026-08-22** — Phases 0–6 complete and confirmed (see Status above). Test rig now carries
  a placeholder aircraft mesh (`Omega_fighterG/fighter_black.FBX`, rough 1.5x scale) instead
  of the bare capsule, so Phase 7's muzzle transforms have real wing geometry to parent to.
  AA-009 and AA-010 (free-look silently steering the aircraft; free-look orbiting its own
  fixed spot instead of the ship) both found and closed the same day. Twelve PlayMode tests
  pass. **Full blow-by-blow of Phases 0–6, including AA-001 through AA-010's investigations:
  see `ARCHIVE.md`.**

- **2026-08-22 (later)** — This file's log (previously ~860 lines covering Phases 0–6) moved
  to `ARCHIVE.md` verbatim, replaced here with the rollup above, to keep read time down —
  nothing was edited or summarized away, only relocated.

  **Also set up version control** (`git init`, `.gitignore` for Unity's generated/local
  directories plus IDE-regenerated `.csproj`/`.sln`/`.slnx` files, initial commit, remote at
  the user's `https://github.com/DataFright/Astro-Aces.git`) — closes the "no safety net
  under six phases of hand-derived work" risk flagged in this session's project review.

  **First push attempt timed out** — traced to size, not auth (`git ls-remote origin`
  succeeded instantly, confirming credentials were fine). `Assets/Dynamic Music/` turned out
  to be **879 MB — 94% of the entire tracked tree** — and both this file and `BUILD_PLAN.md`
  already documented it as imported-but-unused. Asked the user rather than deciding
  unilaterally; they chose to exclude it from git (not delete it — it stays on disk, just
  gitignored, in case it's used later). Since nothing had reached GitHub yet, amended the
  still-local initial commit to drop it (`git rm -r --cached`, `git gc --prune=now` after
  expiring the reflog, since the reflog itself kept the pre-amend blobs reachable and the
  first `gc` alone didn't shrink anything) rather than leaving two commits with the 879 MB
  permanently baked into history. `.git` went from ~800 MB to ~27 MB. **Pushed clean** —
  `origin/main` now matches local `main`, working tree clean.
  **If the project starts actually using the Dynamic Music pack later:** remove its line from
  `.gitignore` and commit it deliberately at that point, ideally via Git LFS given the size.

  **Follow-up, same session — found and fixed a real licensing exposure, not just a size
  one.** Checked the pushed repo's visibility (`public`, confirmed via the GitHub API) and
  noticed four more third-party Asset Store packs were committed alongside the project's own
  code: `BTM_Assets`, `Casual Game Sounds U6`, `Omega_fighterG`, `SimpleNaturePack` (~39 MB
  total — small, unlike Dynamic Music, so this was a licensing problem, not a size one). Most
  Asset Store EULAs (free or paid) don't permit redistributing the raw files outside your own
  build, which a public repo does. Asked the user rather than assuming; they confirmed —
  gitignored all four (plus the leftover `Dynamic Music.meta` orphaned by the earlier commit)
  alongside Dynamic Music, same pattern: stays on disk, drops out of git.

  That alone wasn't enough, though: those four packs were already in the *first* two pushed
  commits, so simply removing them from a new commit would have left the raw files still
  fetchable from GitHub via the earlier commits in history — the actual problem (public
  redistribution) would have persisted invisibly. Since the repo was only minutes old with no
  collaborators, the clean fix was to rewrite history rather than patch around it: confirmed
  with the user first (force-push + history rewrite is always worth confirming, even on a
  brand-new solo repo), then squashed all commits into a single clean root commit via
  `git commit-tree` on the already-correct current tree (no `git filter-repo` available in
  this environment), and force-pushed it over `origin/main`. `.git` went from ~27 MB to
  3.4 MB; `git log` now shows exactly one commit, and none of the five excluded packs were
  ever part of any commit reachable from `main`.
  **Lesson:** "gitignore it going forward" and "it's no longer in the repo" are different
  claims — the second one requires checking (and if necessary rewriting) history, not just
  the current commit, especially once something has already been pushed.

- **2026-08-23** — Camera distances retuned per direct user feedback: both the default chase
  view and the free-look inspect view were "too far back." Chase offset 8m/3m → 6m/2m,
  `positionLerpPerSecond` 8 → 12 (cuts the documented cruise-speed follow lag roughly in
  half). Free-look now orbits at its own `freeLookOrbitDistance` (4.5m) instead of reusing the
  chase offset's magnitude (~8.5m before), since "look around from the chase spot" and
  "inspect the ship up close" turned out to want different distances. This also required
  fixing the scene's already-serialized `ChaseCamera` component (changing a script's default
  value doesn't retroactively update values Unity already saved on the component instance)
  and updating `ChaseCameraPlayModeTests`'s orbit-distance assertion, which had been checking
  "same distance as chase" — no longer correct now that free-look targets a different distance
  on purpose. Verified live via a Unity MCP screenshot (ship's wing/canopy detail clearly
  visible at the new free-look distance, versus barely resolvable before) and all 12 PlayMode
  tests passing. Full derivation in `DESIGN.md` §5's log; the Play Mode session needed two
  stuck-transition recoveries first — see `TOOLING.md`'s new 2026-08-23 entry. **First pass,
  not a final feel call** — these numbers are what looked reasonable from the tightened math
  plus one screenshot, not something the user has flown yet.

**Phase 7 (Weapons) is next.**
