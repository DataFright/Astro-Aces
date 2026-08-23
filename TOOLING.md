# Astro Aces — Tooling

Operational knowledge for whoever (human or AI) is working in this project: the Unity MCP
bridge, writing and running Play Mode tests without a human at the keyboard, using UTI, and
general troubleshooting practice. This is about **the tools themselves** — distinct from
`BUGS.md` (game bugs) and `DESIGN.md` (game mechanics). Update it when you learn something new
about how to work in this project, so the next session doesn't have to rediscover it.

---

## Unity MCP bridge

**What it is:** [CoplayDev/unity-mcp](https://github.com/CoplayDev/unity-mcp)
(`com.coplaydev.unity-mcp`, pinned in `Packages/manifest.json`) — gives Claude Code tools for
scene control, console reading, script editing, running tests, and executing arbitrary Editor
C# (`execute_code`).

**Setup:** `Window > MCP for Unity` in the Editor. "Auto-Start on Editor Load" should be
enabled (Advanced Settings) so the bridge comes up automatically; otherwise click "Start
Server" manually each session. MCP tools load at Claude Code session start — a session that
was already running when the bridge came up won't have them; start a fresh session.

**Health check, every fresh session, before trusting it for anything:**
1. Confirm the tools are actually loaded this session (try one, or `ToolSearch`) — "connected"
   in the Editor does not guarantee this session's tool list includes them yet.
2. `read_console` — should return real log content, not an empty/placeholder response.
3. `manage_scene get_active` — should report the actual currently-open scene.
4. `manage_editor play` / `stop` — confirm it actually flips Play Mode (cross-check
   `mcpforunity://editor/state`'s `play_mode.is_playing`).
5. Always check the `mcpforunity://custom-tools` resource for anything project-specific
   before assuming a capability doesn't exist.

**Known caveats — found the hard way, don't rediscover these:**

- `mcpforunity://editor/state` and `mcpforunity://scene/gameobject/{id}` can go **stale**
  during or immediately after a Play Mode session, especially when the Editor window has no
  real OS focus (`editor.is_focused: false`). Symptom: `staleness.is_stale: true`,
  `observed_at_unix_ms` frozen across repeated reads. Don't trust a single read of either as
  "current state" during Play. Instead call `execute_code` for a live, synchronous read (e.g.
  `Time.time`, `Time.frameCount`, `GameObject.Find(...).GetComponent<T>()...`), or prefer real
  telemetry (a UTI CSV, `read_console`) over polling scene state directly.
- `manage_editor action:"play"` returns success **before** Unity's domain-reload/scene-load
  transition into Play Mode has actually finished — `play_mode.is_changing` can still read
  `true` a full second or more later, especially while unfocused. A short `sleep` placed right
  after calling `play` can be entirely consumed by this transition, leaving little or no real
  gameplay time before a subsequent `stop`. Either confirm real ticking directly (`execute_code`
  checking `Time.frameCount` twice with a gap — don't trust the possibly-stale `editor/state`
  resource for this) or just budget generous extra real time before anything that needs actual
  physics ticks to have happened.
- `manage_editor play/pause/stop` and `read_console` are the two calls confirmed fully
  reliable regardless of focus/staleness.
- The Play Mode transition's real duration is **unpredictable while unfocused**, not just
  "a bit longer than a focused session." Usually settles within a few seconds, but has taken
  30+ real seconds on at least one occasion (confirmed via repeated `execute_code` checks of
  `Time.frameCount`/`Time.unscaledTime` sitting completely frozen, `play_mode.is_changing`
  still `true`, then catching up and ticking normally without any intervention). Don't
  conclude "hung" from one long wait — poll patiently (`Time.frameCount` via `execute_code`,
  not the `editor/state` resource) for at least 20-30s before considering `stop` + a fresh
  `refresh_unity` + retry. That recovery has worked every time it's been needed.
- **2026-08-23 — this got worse than previously documented, twice in one session, and a real
  console error showed up alongside it.** Two consecutive `play` attempts each sat frozen at
  `Time.frameCount == 2` for 30+ real seconds (`editor/state` agreeing: `is_changing: true`
  the whole time), each requiring the full `stop` + `refresh_unity(force)` + retry recovery —
  not just a longer wait. The **third** attempt (after the second recovery) worked normally.
  Separately, `read_console` after that session's Play Mode work showed five identical
  `"An abnormal situation has occurred: the PlayerLoop internal function has been called
  recursively"` entries — a genuine Unity-internal warning, not a project bug (nothing in
  project code touches the player loop directly), plausibly related to calling `execute_code`
  to poll `Time.frameCount` repeatedly while the Editor was unfocused and mid-transition, but
  not confirmed as the cause. Didn't chase it further since it didn't block anything (tests
  still ran and passed, screenshots still worked) — noting it here in case it recurs and
  becomes worth reporting to Unity or the MCP bridge's maintainers. **Practical update:** if
  one `stop` + `refresh_unity` + retry doesn't recover a stuck transition within ~30s, don't
  assume something is fundamentally broken — try the same recovery a second time before
  escalating; it worked on the second attempt here.

---

## Writing and running Play Mode tests without a human

Don't conclude "this needs a human at the keyboard" from one failed naive attempt. Work out
**which specific ingredient** a test actually depends on first — most things are automatable
with the right technique; a real minority genuinely aren't.

- **Poking `Keyboard.current` / `Mouse.current` directly from `execute_code`** (e.g.
  `InputSystem.QueueStateEvent` against the live device) does **not** reliably work outside a
  real test harness — the real OS device backend keeps polling in the background and
  overwrites synthetic state within a frame or two. Confirmed directly: a queued "key held"
  state read back `true` once, then silently reverted within ~2 seconds with nothing having
  actually happened in-game.
- **`InputTestFixture`** (ships with the Input System package; asmdef
  `Unity.InputSystem.TestFramework`, `autoReferenced: false` — must be referenced explicitly)
  is the real, reliable way to simulate input. Its `Setup()` swaps in a fully isolated test
  runtime, severed from real hardware for the test's duration, so a simulated held key
  genuinely stays held for as long as the test wants. Use `Press` / `Release` / `Set` on
  devices added via `InputSystem.AddDevice<T>()`.
  - This project's test assembly: `Assets/AstroAces/Tests/PlayMode/`
    (`AstroAces.Tests.PlayMode.asmdef` — references `AstroAces.Runtime`,
    `Unity.InputSystem.TestFramework`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner`;
    precompiled reference `nunit.framework.dll`).
  - Pattern: `[UnityTest]` methods on a class extending `InputTestFixture`, loading the real
    `Dogfight` scene (`SceneManager.LoadSceneAsync`) so the actual rig and its actual wiring
    get exercised — not a synthetic stand-in.
  - Run via MCP: `run_tests` with `mode: "PlayMode"` and `assembly_names` or `test_names`;
    poll `get_test_job` (pass `wait_timeout` rather than busy-polling).
- **`InputTestFixture` cannot help with bugs that are fundamentally about real OS/hardware
  behavior** — its virtual devices have no real OS cursor and no real native event timing
  behind them. For those (e.g. whether `CursorLockMode.Locked` actually pins the OS cursor in
  the Editor's Game view — see `BUGS.md` AA-006), the choice is: get a human to reproduce it
  for real, or **observe** real device state during a real session (see UTI below) instead of
  trying to fake the input.
- **General lesson:** two different classes of "needs a human" exist — genuinely OS/hardware-
  bound behavior, and everything else. Don't conflate them. The second class is almost always
  automatable; only the first genuinely isn't.

---

## UTI (Unity-Testing-Inspector)

Vendored at `Assets/AstroAces/ThirdParty/UTI`, hand-copied from
[DataFright/Unity-Testing-Inspector](https://github.com/DataFright/Unity-Testing-Inspector)
(currently tag `v0.2.2`) — **not** installed via UPM, because the package's declared
`unity: 6000.5` is newer than this project's 6000.3.21f1 and blocks the normal git-package
install path.

- **Core pieces:** `BeanTracker` (captures a GameObject's transform + custom fields on an
  interval), `BeanLogger` (routes samples to Console / CSV / JSON Lines), `BeanSnapshotExporter`
  (path-trail PNGs), `BeanMouseTracker` (real cursor tracking via the **legacy Input Manager
  only** — unusable as-is here, since this project's Active Input Handling is
  New-Input-System-only; write a small `Mouse.current.position`-based `CustomCapture` adapter
  instead — see `CursorDriftDiagnostic.cs` for the pattern).
- **Custom telemetry pattern:**
  ```csharp
  [RequireComponent(typeof(BeanTracker))]
  void Awake() => GetComponent<BeanTracker>().CustomCapture = MyCaptureMethod;

  Dictionary<string, float> MyCaptureMethod(GameObject go) => new Dictionary<string, float> {
      ["someField"] = someValue,
  };
  ```
  Attach to any GameObject — an existing one, or one spun up dynamically via `execute_code`
  (`new GameObject(...)`, `AddComponent<BeanTracker>()`, `AddComponent<BeanLogger>()`, set
  `logger.OutputTargets = BeanOutputTargets.Csv`). See `LoopDiagnostic.cs` for a real example
  already wired onto the test rig.
- **CSV output** lands at `<project root>/UTI/BeanLogs/*.csv` — read directly off disk, no
  export step needed. Filenames are timestamped plus a random token; `ls -t` for newest-first.
- **Upgrading the vendored copy in place:** preserve the old `.meta` GUIDs for every
  pre-existing file, or Unity hands out fresh random ones on next import and any existing
  scene reference to those components breaks into "Missing (Mono Script)". Diff old vs. new
  `.meta` files' `guid:` lines and reuse the old value for anything that already existed; only
  genuinely new files need fresh GUIDs.
- **Capture-cadence gotcha:** `BeanTracker` samples on `Update` or `FixedUpdate` depending on
  `CaptureMode`. A value that actually changes on the *other* cadence (e.g.
  `AircraftInput.MouseDelta`, which is `Update`-driven, read by an `EveryFixedUpdate` tracker)
  can show a one-tick-stale read in the CSV even though the underlying game state already
  changed correctly. Verify against a direct C# assertion/read when the two cadences might not
  align, not just a raw grep of the CSV column.

---

## Teleporting a Rigidbody in a test (or anywhere)

Setting `transform.position`/`transform.rotation` directly on a **non-kinematic Rigidbody**
gets silently overwritten on the very next `FixedUpdate` -- the physics engine drives the
Transform for a dynamic body every physics step, so a direct Transform edit loses the race.
Confirmed directly (`PlayAreaBoundsPlayModeTests`, 2026-08-22): a test set
`rig.transform.position` to a point 4000m out, then traced `PlayAreaBounds.FixedUpdate()`
every tick afterward and found the rig sitting at its original spawn point on every single
one -- the edit never took effect at all, no error, no warning, just silently reverted.

**Fix:** go through `rb.position` / `rb.rotation` instead (or `rb.MovePosition`/
`rb.MoveRotation` for a smooth kinematic-style move over the next step, not applicable to an
instant teleport). Also worth doing at the same time: zero `rb.linearVelocity`/
`rb.angularVelocity` if the teleport should look like a clean reset rather than carrying
whatever velocity the body had — and if the new position implies a specific heading, set
`rb.rotation` to match, since a big rotation/velocity mismatch on this project's aircraft
(thrust following the OLD forward, lift computed from the NEW velocity) touches off a real
transient (high AoA, big lift/drag swings) that can fling the body somewhere unexpected
before a test gets to check anything.

---

## Testing camera/follow logic around a fast-moving target

Found writing `ChaseCameraPlayModeTests.FreeLook_OrbitsAroundShip_NotJustRotatesInPlace`
(`BUGS.md` AA-010) — took three failed attempts before landing on the right technique, and
neither failure was in the camera code being tested.

- **Freeze the target for tests that check a smoothed follow/orbit position, if the target
  normally moves fast relative to the follow distance.** This project's aircraft flies
  ~114 m/s; a camera orbiting it at only ~8.5m can have the target move farther between
  render frames than the entire camera-to-target distance whenever frame delivery is
  irregular (already true in this environment while the Editor is unfocused — see the Play
  Mode transition caveats above). That swings the follow logic's computed target
  position/rotation wildly frame to frame and can look exactly like "the smoothing never
  converges," when the smoothing math is actually fine and the confound is a moving target
  outrunning the frames available to observe it. Set `rb.isKinematic = true` and zero
  velocity on the target for the duration of the test to remove this — it isolates the
  follow logic itself, which is what's actually under test.
- **`Vector3.Lerp` between two points on an orbit "sphere" travels a straight chord, not an
  arc.** Any chord between two points on a sphere's surface passes *inside* the sphere except
  at its endpoints — so distance-from-target legitimately dips below the orbit radius
  mid-transition before recovering, as a normal consequence of exponential position Lerp, not
  a bug. A test with an early-break polling loop (breaking as soon as some threshold clears)
  can catch this dip and read it as "distance is wrong." Give the position a short extra
  settle window after the break condition fires, before asserting on distance specifically.
- Neither of these is specific to `ChaseCamera` — they generalize to any test of a smoothed
  follow/orbit component tracking a fast or continuously-moving target.

---

## When stuck on a Unity/package-specific quirk

Search for it before spending a long time re-deriving from first principles. Unity itself, the
Input System package, and third-party packages all have plenty of prior art on common quirks —
domain-reload timing, Editor-vs-build input differences, package version gating, and so on.
This document exists specifically so a future session doesn't have to rediscover the findings
above; the same courtesy likely applies to problems other people have already hit and written
up publicly.

---

## Log

- **2026-08-22 (later, this session) — Added the fast-moving-target follow/orbit testing
  technique.** `FreeLook_OrbitsAroundShip_NotJustRotatesInPlace` (`ChaseCameraPlayModeTests`,
  `BUGS.md` AA-010) failed three times in a row with what looked like a real convergence
  bug — turned out to be two different test-setup confounds instead: the aircraft's flight
  speed outrunning irregular render-frame delivery at the orbit's close radius (fixed by
  freezing the target's Rigidbody for the test), and `Vector3.Lerp` legitimately dipping
  inside the orbit sphere mid-transition (a chord, not an arc — fixed with a short settle
  window past the test's early-break point). Full detail in the new section above.

- **2026-08-22 (later still) — Added the Rigidbody-teleport gotcha**, found writing a
  PlayAreaBounds regression test: a direct `transform.position` edit on a non-kinematic
  Rigidbody silently reverts on the next `FixedUpdate`. Traced with a temporary `Debug.Log`
  in the component itself rather than guessing further after two failed attempts at fixing
  the *test* alone — the position was never actually changing, so no amount of adjusting
  wait times or velocity would have helped.

- **2026-08-22** — Created, consolidating everything learned this session about the MCP
  bridge's real behavior and caveats, the `InputTestFixture` technique for solo Play Mode
  testing (and where it can't help), and UTI usage/upgrade practice.

- **2026-08-22 (later, same day) — Added the "Play Mode transition can take 30+ seconds
  while unfocused" caveat**, found while verifying Phase 4's `ChaseCamera` solo: a Play
  session sat completely frozen at frame 1/2 for well over its usual settling time before
  catching up on its own. Documented the recovery that worked (patient polling, then
  `stop` + `refresh_unity` + retry if it still looks stuck after ~30s) so a future session
  doesn't mistake a slow transition for a real hang.

  **User's hypothesis, worth testing next time it happens:** the stall may be tied to the
  Editor window never receiving a real OS click/focus event, not just "unfocused" in
  general — a session driven purely via MCP tool calls might never trigger whatever internal
  path a genuine mouse click into the window does. Not confirmed either way (nothing here can
  synthesize a real OS click), but worth noting if it recurs: whether the Editor's had any
  real human interaction since the last successful Play session.
