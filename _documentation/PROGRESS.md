# Progress

Status as of 2026-09-30. Phases and task numbers refer to [the plan](PLAN.md).
What each phase actually did is in [Progress/](Progress), one file per phase;
what the application does today is in
[What works today](Progress/WHAT-WORKS.md).

## Where we are

**Seventeen phases are complete** (143 of 143 tasks). Marbas
runs as a full-screen two-panel file browser: both panels list a real directory,
the keyboard moves between and inside them, entries can be marked one by one or
by wildcard for later operations, a status line describes the entry under the
cursor, and failures surface as dialogs instead of crashes. Every function key
does its job — `F3` views the file under the cursor and `F4` opens
it in the system editor, `F5` copies and `F6` moves or renames into the other
panel, `F7` makes a directory, `F8` deletes to the trash and `Shift+F8` for
good — the operations all acting on the
same operation targets, with copying and moving running off the UI thread behind
a progress window that `Esc` stops. Since P7.1 the screen wears the classic
colours: blue panels with cyan entries, a cyan cursor bar, yellow marks and a
cyan function-key bar, and since P7.2 each panel sorts by name, extension, date
or size in either direction, saying so in its title. P7.5 put every key binding
into one table: the bottom bar is a fixed row of twelve slots read out of it,
one per function key, and holding `Ctrl` or `Alt` swaps the commands on those
slots for the ones those modifiers reach without moving a key name. Since P7.3 each panel
also decides on its own whether the hidden entries — dotfiles and whatever the
OS marks hidden or system — are listed at all. P7.4 closed the phase: where each
panel stands, how it sorts and whether it conceals are written to a config file
when the app quits and read back when it starts, so a session survives a
restart. P8.1 put a number on the suite and a workflow around it, and P8.2 made
both hold on Linux as well as on Windows. P8.3 closed the plan: two publish
profiles turn the project into one self-contained, single-file executable per
operating system, and a tag builds both on their own runners and hangs them off
a GitHub release.

**The suite passes on both operating systems** — a handful of tests skip on
Linux, the ones that assert on drive letters, which exist nowhere else. Unit
tests cover the logic modules and the pure `Ui` ones; integration tests drive
real Terminal.Gui views over an in-memory file system. All of them follow the
arrange/act/assert shape and the folder layout described in
[Testing](Architecture/TESTING.md). `dotnet test Marbas.slnx` is the whole run,
and GitHub Actions runs it on both operating systems on every push and pull
request; coverage stays close to complete for everything but `Program.fs` and
`Adapters/`, the composition root no test can reach. What no test can reach
otherwise — colours, a resized terminal, a real permission bit, the mouse — is
written up as the checklist in
[Cross-platform](Architecture/CROSS-PLATFORM.md).

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
| 18 — The clipboard | P18.1–P18.7 | P18.1 done | [journal](Progress/PHASE-18-THE-CLIPBOARD.md) |

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
