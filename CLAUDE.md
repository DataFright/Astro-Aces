# CLAUDE.md

## Documentation Structure

- **README.md** – Project overview, features, and high-level concepts
- **DESIGN.md** – Chaptered, detailed design notes: the project's overall design idea/flow, plus specific feature and mechanic details — how each system works, exact values/amounts, and how systems connect to one another
- **BUILD_PLAN.md** – The ordered, executable task list for building the project (phases with explicit contracts and per-phase acceptance checks). Work through it in order; mark each section done as it's completed, and correct its spec in place when the actual implementation deviates from what was originally planned, rather than letting the doc go stale
- **HANDOFF.md** – Session handoff notes: current status, active goals, and next steps for the next agents. Include an onboarding section covering exactly what to study in the code and docs to get up to speed on the active goals
- **TESTS.md** – Tracking test coverage, updated every time a new test is added. Tests should be added when new features or bugs arise, to help maintain quality and shorten the debug lifecycle
- **BUGS.md** – Tracking bugs, updated every time a new bug is found. Updated and tracked until proven completed and closed. Should track with associated tests to help confirm bug is present or closed.
- **TOOLING.md** – Operational knowledge for working in this project: the Unity MCP bridge (what it is, how to health-check it, known caveats), how to write and run Play Mode tests without a human at the keyboard (including where that's genuinely not possible and why), using the vendored UTI telemetry toolkit, and general troubleshooting practice. Update it when you learn something new about the tools themselves, as distinct from game bugs (`BUGS.md`) or game mechanics (`DESIGN.md`)
- **ARCHIVE.md** – Overflow store for old log entries moved out of the other docs (starting with `HANDOFF.md`) once they stop being current-state-relevant, so the day-to-day docs stay fast to read. Historical trace only — entries are moved here verbatim, never edited or condensed, and this file is never more authoritative than the current summary in the doc an entry came from. Not part of the normal session-start reading list; open it only when you need the blow-by-blow of how something was found/tried/fixed


## Update Protocol

When modifying any documentation:
- Add a single timestamp — date and time-of-day (e.g. 2026-08-09 16:54 CDT) — to the log section at the bottom, summarizing the change and any other important notes.
- **Archiving policy:** when a doc's own log section gets long enough that reading it just to find current status is slow (a good trigger: you notice yourself skimming past old entries to find the current one), move the older entries into `ARCHIVE.md` under a dated, doc-labeled section, verbatim — do not summarize or edit them away. Replace them in the source doc with a short rollup paragraph plus a pointer to `ARCHIVE.md`. Never delete history, only relocate it.

## Session Workflow

1. **Before starting**: Read `README.md` and `DESIGN.md` for context, check `BUILD_PLAN.md` for the current phase and its acceptance check, then check `HANDOFF.md` and read it if present, and check `BUGS.md` for open issues relevant to the work. Before using Unity MCP tools, writing/running Play Mode tests, or touching the vendored UTI toolkit, check `TOOLING.md` first — it has known caveats and working patterns that are easy to otherwise rediscover the hard way
2. **During work**: Keep `DESIGN.md` and code in sync in both directions — align code comments with `DESIGN.md`, and update `DESIGN.md` when mechanics/values actually change. Follow `BUILD_PLAN.md`'s phase order and its stated acceptance check before moving to the next phase
3. **Before ending**: Update `BUILD_PLAN.md` to mark completed phases/sections and correct anything that turned out different from the original spec, update `HANDOFF.md`'s status if work is incomplete, add/update `TESTS.md` entries for anything new or fixed, log any new bugs found or update the status of any `BUGS.md` entries touched this session, and add anything newly learned about the tools/workflow themselves to `TOOLING.md`
