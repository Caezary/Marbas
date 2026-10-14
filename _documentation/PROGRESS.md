# Progress

Status as of 2026-10-15. Phases and task numbers refer to [the plan](PLAN.md).
What each phase actually did is in [Progress/](Progress), one file per phase;
what the application does today is in
[What works today](Progress/WHAT-WORKS.md).

## Where we are

| Phase | Tasks | State | Journal |
|-------|-------|-------|---------|
| 1 — Application skeleton | P1.1–P1.4 | done | — |
| 2 — Directory listing | P2.1–P2.4 | done | — |
| 3 — Navigation | P3.1–P3.5 | done | — |
| 4 — Selection | P4.1–P4.3 | done | — |
| 5 — File operations | P5.1–P5.6 | done | — |
| 6 — Viewing files | P6.1–P6.2 | done | — |
| 7 — Polish & configuration | P7.1–P7.5 | done | — |
| 8 — Quality & release | P8.1–P8.3 | done | — |
| After the first plan | eight rounds | done | [journal](Progress/AFTER-THE-FIRST-PLAN.md) |
| 9 — Archives | P9.1–P9.14 | done | [journal](Progress/PHASE-09-ARCHIVES.md) |
| 10 — Running a file | P10.1–P10.7 | done | [journal](Progress/PHASE-10-RUNNING-A-FILE.md) |
| 11 — Tabs | P11.1–P11.8 | done | [journal](Progress/PHASE-11-TABS.md) |
| 12 — Watching the directories on screen | P12.1–P12.6 | done | [journal](Progress/PHASE-12-WATCHING-THE-DIRECTORIES.md) |
| 13 — The drive, the link and what is left behind | P13.1–P13.16 | done | [journal](Progress/PHASE-13-DRIVES-LINKS-AND-TRASH.md) |
| 14 — Finding files | P14.1–P14.14 | done | [journal](Progress/PHASE-14-FINDING-FILES.md) |
| 15 — Answering the reviews | P15.1–P15.26 | done | [journal](Progress/PHASE-15-ANSWERING-THE-REVIEWS.md) |
| 16 — History | P16.1–P16.10 | done | [journal](Progress/PHASE-16-HISTORY.md) |
| 17 — The marks, the panel and the favourites | P17.1–P17.10 | done | [journal](Progress/PHASE-17-MARKS-PANEL-AND-FAVOURITES.md) |
| 18 — The clipboard | P18.1–P18.7 | done | [journal](Progress/PHASE-18-THE-CLIPBOARD.md) |
| 19 — The viewer finds and decodes | P19.1–P19.6 | done | [journal](Progress/PHASE-19-THE-VIEWER-FINDS-AND-DECODES.md) |
| 20 — The user menu | P20.1–P20.5 | done | [journal](Progress/PHASE-20-THE-USER-MENU.md) |
| 21 — The log and the debts | P21.1–P21.18 | done | [journal](Progress/PHASE-21-THE-LOG-AND-THE-DEBTS.md) |
| 22 — What the manual pass found | P22.1–P22.13 | in progress (6 of 13) | [journal](Progress/PHASE-22-WHAT-THE-MANUAL-PASS-FOUND.md) |

## What works today

The catalogue of what the application does — every key, what it acts on and
what it refuses — is in [What works today](Progress/WHAT-WORKS.md).

## Measurements

What was timed, against what, and what the numbers decided is in
[Measurements](Progress/MEASUREMENTS.md), one entry per task that turned on one.

## Architecture

Design decisions and their reasoning live in [_documentation/Architecture/](Architecture/OVERVIEW.md),
one topic per file — domain types, state and effects, file operations, archives,
UI and input, settings persistence, tabs, auto-refresh, testing, packaging and
cross-platform behaviour.

## Rough edges

What is known to be wrong, missing or awkward is in
[Rough edges](ROUGH-EDGES.md), checked against the code and grouped by the part
of the application it belongs to. What
[phase 15](Plan/PHASE-15-ANSWERING-THE-REVIEWS.md) did about the largest of them
is in [its journal](Progress/PHASE-15-ANSWERING-THE-REVIEWS.md).
