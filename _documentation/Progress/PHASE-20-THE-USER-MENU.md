# Phase 20 — The user menu

The tasks are in
[Phase 20 — The user menu](../Plan/PHASE-20-THE-USER-MENU.md).

**P20.1 made `F4` start the editor without waiting for it, on Windows.**
`EditorProcess` was split into a description and a run. `describe` answers an
`EditorStart` — the `ProcessStartInfo` and an `EditorWait` — and `run` starts
what it describes and, only when told to, waits. On Windows the wait is
`StartsDetached` and the start goes through the shell (`UseShellExecute`), the
same verb `Enter` uses, which is what gives a console editor a console window
of its own instead of the terminal the panels are drawn in. On Unix the wait is
`WaitsForExit` and the editor is started directly as before: a console editor
such as `vi` has no window to open there, so the panels hold still until it
ends and the screen is repainted afterwards. The choice is the platform's and
nothing else's — the editor named for the extension, the default one and
`$EDITOR` are resolved exactly as they were.

The panel is no longer re-read when the editor reports back. `EditorStarted Ok`
leaves the state as it is; until now it ran the refresh `Ctrl+R` runs, which
had a meaning only while the editor's end was the moment `F4` returned. A
detached editor has no such moment, and the directory watch of phase 12
already follows the file as it is saved — on Unix too, so the waiting branch
did not keep the refresh either.

Two names changed with the behaviour. The message `EditFinished` became
`EditorStarted` and the port `Processes.RunEditor` became
`Processes.StartEditor`: on Windows nothing finishes before the answer comes
back, and a name that promised it would have lied on the platform the change
was made for.

The tests: `EditorProcessTests` prove the description on each platform — no
waiting and the shell on Windows, waiting and a direct start on Unix — along
with the file handed over, the directory it starts in and the editor chosen for
the file coming before `$EDITOR`; nothing in them starts a process.
`UpdateTests` prove a started editor changes no state and asks for nothing, and
`EditKeyTests` that `F4` leaves the panel as it was read even when the editor
wrote beside the file before answering. That a file saved by hand shows in the
panel, and that a console editor opens a window of its own, are checked by
running the application.
