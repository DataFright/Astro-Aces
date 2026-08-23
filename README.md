# Astro Aces

Arcade space dogfighting — **Star Fox meets War Thunder**.

You fly a fighter with War Thunder Air Arcade controls and combat, but the aircraft is a
spaceship and the cast is meant to be light-hearted and goofy. Battles happen in the thin
atmosphere of moons and asteroids: enough air and gravity that the ship flies like an
aeroplane, not like a free-floating spacecraft.

**Unity 6000.3.21f1 · URP 17.3 · C# 9 · New Input System**

---

## The one design rule

> The mouse controls **where the player wants to fly**.
> The flight controller decides **how the aircraft should manoeuvre**.
> Physics decides **how the aircraft actually moves**.

Three layers, never mixed. When the aircraft misbehaves, work out which layer owns the
problem before touching a number in another one. `DESIGN.md` chapter 3 has the triage table.

---

## Feel targets

| Target | Value | How it is achieved |
| --- | --- | --- |
| Top speed | ~300 mph | Emergent from thrust vs. drag. **No speed clamp exists.** |
| Stall | ~100 mph | Emergent from the lift curve. Below it the aircraft mushes and sinks. |
| Ceiling | ~3,000 ft | Emergent from air density falling off with altitude. **No altitude clamp exists.** |
| Kill | 6 hits | 5 damage × 6 = 30 HP. |
| Fire rate | 20 rounds/sec | Bullets inherit the aircraft's velocity, so you never fly into your own fire. |

Every one of those numbers is *derived*, not typed in — see `DESIGN.md` chapter 2 for the
arithmetic — and Edit Mode tests assert them, so changing a coefficient fails a test rather
than quietly changing the game.

---

## Look

Dark purple-and-blue sky, stars, fluffy non-colliding clouds, rocky ground below. Simple,
somewhat cel-shaded. The player's fighter is **grey and green**; the enemy's is **black and
red**.

---

## Repository map

| Path | What lives there |
| --- | --- |
| `Assets/AstroAces/Scripts/Flight/` | Aerodynamics, control law, aircraft components |
| `Assets/AstroAces/Scripts/Combat/` | Guns, projectiles, health, damage |
| `Assets/AstroAces/Scripts/AI/` | Enemy pilot |
| `Assets/AstroAces/Scripts/World/` | Ground, sky, clouds, play-area bounds |
| `Assets/AstroAces/Scripts/UI/` | HUD, crosshair, minimap, menus |
| `Assets/AstroAces/Scripts/Core/` | Layers, game state, audio |
| `Assets/AstroAces/Editor/` | `Astro Aces > Setup Project` menu tools |
| `Tools/compile-check.ps1` | Offline C# compile check, ~5s, no Editor needed |

## Documentation

| Doc | Read it for |
| --- | --- |
| `DESIGN.md` | How every system works and **why each number is what it is** |
| `BUILD_PLAN.md` | The ordered, executable task list to build the foundation |
| `HANDOFF.md` | Current status, what to study first, what to do next |
| `TESTS.md` | Test coverage |
| `BUGS.md` | Open and closed bugs |
| `TOOLING.md` | The Unity MCP bridge, Play Mode testing without a human, UTI, troubleshooting practice |

**Start at `HANDOFF.md`.**

---

## Third-party assets

Five Asset Store packs are used by the project but **not committed to this repo** —
`Assets/Dynamic Music`, `Assets/BTM_Assets`, `Assets/Casual Game Sounds U6`,
`Assets/Omega_fighterG`, `Assets/SimpleNaturePack` are all gitignored, since this repo is
public and their licenses don't permit redistributing the raw files. Cloning this repo alone
will **not** give you a project that opens cleanly in Unity — the scene references models,
materials and audio clips from those packs, and you'd need to re-import each one from the
Asset Store yourself into the matching `Assets/` folder name before `Dogfight.unity` will
load without missing-reference errors.

---

## License

No license is granted. All rights reserved. This repo is public for visibility, not for
reuse — code and assets here may not be copied, forked, or redistributed without permission.
(This doesn't apply to the third-party asset packs above, which were never included here and
remain under their own respective licenses regardless.)

---

## Status

Foundation phase. The flight maths, tuning config, project setup tooling and build plan
exist and compile; Phases 0–6 are done (see `HANDOFF.md`), the gameplay layer above them
(weapons, damage, AI) is not built yet. Audio is wired but **muted** deliberately.

---

## Log

- **2026-08-17 15:10 CDT** — Created. Project re-scoped from scratch after an intentional
  wipe: documents the design brief, the three-layer flight rule, the derived feel targets,
  and the repository layout.

- **2026-08-22** — Added `TOOLING.md` to the documentation table: operational knowledge
  about the Unity MCP bridge, writing/running Play Mode tests without a human, and using the
  vendored UTI toolkit — split out from `BUGS.md`/`HANDOFF.md` log entries where it had been
  accumulating, since it's about the tools themselves rather than game bugs or mechanics.

- **2026-08-22 (later)** — Repo went public on GitHub this session (`HANDOFF.md` has the full
  setup story, including a licensing scare: several third-party Asset Store packs had been
  committed and had to be gitignored and purged from history). Added the **Third-party
  assets** and **License** sections above to reflect that — no license is granted (all rights
  reserved, public for visibility only), and five Asset Store packs are used locally but
  intentionally excluded from the repo. Updated the Status section, which still claimed no
  version control existed.
