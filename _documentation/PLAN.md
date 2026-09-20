# Marbas — Implementation Plan

The plan is split into phases, one file per phase under [Plan/](Plan). Each
phase produces a runnable, observable improvement, and each task inside a phase
is small enough to be implemented and verified in one sitting. Tasks are
numbered `P<phase>.<task>` so they can be referenced in commits (e.g.
`P2.3: sort directories first`). What has been built of them is in
[Progress](PROGRESS.md).

## Guiding principles

- **Always runnable** — after every task the app builds and starts.
- **Logic separated from UI** — file-system logic lives in plain F# modules
  (pure functions where possible) so it can be unit-tested without a terminal.
  UI code only wires that logic to Terminal.Gui views.
- **Keyboard first** — every feature must be reachable without a mouse,
  with the classic orthodox key bindings as the reference.
- **Test first** — wherever feasible, implementation starts with a failing
  test (unit tests over the UI-free modules, in-memory file system via
  `System.IO.Abstractions`). Tests are the standing proof the app works.

## The phases

| Phase | Tasks | State |
|-------|-------|-------|
| [1 — Application skeleton](Plan/PHASE-01-APPLICATION-SKELETON.md) | P1.1–P1.4 | done |
| [2 — Directory listing](Plan/PHASE-02-DIRECTORY-LISTING.md) | P2.1–P2.4 | done |
| [3 — Navigation](Plan/PHASE-03-NAVIGATION.md) | P3.1–P3.5 | done |
| [4 — Selection](Plan/PHASE-04-SELECTION.md) | P4.1–P4.3 | done |
| [5 — File operations](Plan/PHASE-05-FILE-OPERATIONS.md) | P5.1–P5.6 | done |
| [6 — Viewing files](Plan/PHASE-06-VIEWING-FILES.md) | P6.1–P6.2 | done |
| [7 — Polish & configuration](Plan/PHASE-07-POLISH-AND-CONFIGURATION.md) | P7.1–P7.5 | done |
| [8 — Quality & release](Plan/PHASE-08-QUALITY-AND-RELEASE.md) | P8.1–P8.3 | done |
| [9 — Archives](Plan/PHASE-09-ARCHIVES.md) | P9.1–P9.14 | done |
| [10 — Running a file](Plan/PHASE-10-RUNNING-A-FILE.md) | P10.1–P10.7 | done |
| [11 — Tabs](Plan/PHASE-11-TABS.md) | P11.1–P11.8 | done |
| [12 — Watching the directories on screen](Plan/PHASE-12-WATCHING-THE-DIRECTORIES.md) | P12.1–P12.6 | done |
| [13 — The drive, the link and what is left behind](Plan/PHASE-13-DRIVES-LINKS-AND-TRASH.md) | P13.1–P13.16 | done |
| [14 — Finding files](Plan/PHASE-14-FINDING-FILES.md) | P14.1–P14.14 | done |
| [15 — Answering the reviews](Plan/PHASE-15-ANSWERING-THE-REVIEWS.md) | P15.1–P15.24 | P15.1–P15.6 done |

## Suggested module layout

```
Marbas/
  FileSystem/        P2, P5 — listing, operations (no UI dependencies)
  State/             P3, P4 — panel & app state, selection, Store
  Ui/                P1+    — panels, dialogs, key bindings, theming;
                              since grouped into feature folders, see
                              Architecture/OVERVIEW.md
  Program.fs         composition root
```

Dependencies flow downward only: `Ui` → `State` → `FileSystem`.

### State handling

`AppState` is immutable: every transition is a pure function returning a new
state, which is what the unit tests exercise. The single mutable cell lives in
`State/Store.fs` — a small store that Terminal.Gui event handlers use to
`Dispatch` those pure transitions and re-render afterwards. Handlers never
mutate state directly.

Terminal.Gui runs a single-threaded event loop, so the store needs no
synchronisation; background work (P5.6) must marshal its dispatch back onto
the UI thread via `Application.Invoke`.
