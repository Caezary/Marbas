# Phase 11 — Tabs

The tasks are in [Phase 11 — Tabs](../Plan/PHASE-11-TABS.md).

**An eleventh phase opened, and one Marbas now holds several pairs of panels.**
A tab is the pair — the left panel, the right one and which of them is active —
so switching a tab changes both sides at once. `Alt+F11` opens one on the
directories already on screen, `Alt+Shift+F11` closes it, the last one cannot be
closed, `F11` lists them in a popup and `Alt+←` / `Alt+→` walk between them.
`AppState` holds the tabs in a zipper, which is what makes "there is always
exactly one tab in front" a fact of the type rather than a check: closing the
only tab is a key that does nothing, because the type cannot express what it
would produce. The session file grew from one pair of panels into numbered
blocks — `tab.1.left.path`, `tab.1.active`, `tabs.current` — reading a file
written before tabs as a single tab and rewriting it in the new spelling; since
P11.6 it also carries the entry each cursor stood on, so every tab comes back
where it was left. Every tab command writes the session, and `Shift+F9` writes
it on demand. The top right corner counts the tabs, and the right panel's title
gives way to it: a path that no longer fits is shortened to
`C:\...\parent\current` rather than clipped. `F11` is the one key of the phase a
terminal argues about — Windows Terminal and the Windows console keep it for
full screen — so `Alt+T` is bound beside it, which is what the key probe in
`_probes/KeyProbe` was written to settle.

**The shared vocabulary moved down to where it is spoken.** P11.2 had sent
`PanelSide` alone to `Marbas.Domain`, because the session file names the active
panel and `FileSystem` cannot reach `State`; the rest of the words followed in
one pass. `EntryName`, `SortOrder`, `HiddenEntries`, `Executability`,
`SearchText`, `NamePattern` and `ProgramSettings` left `FileSystem/`, and
`PanelCursor`, `TabStep`, `SelectionMode`, `CursorDirection`, `DeleteScope` and
`DeleteMode` left `State/AppState.fs`. None of them takes an `IFileSystem` or
knows a view exists: they are the words the layers above speak, and holding
them below both is what stops a type living wherever it was first needed.
`NamePattern` was the plainest case — nothing in `FileSystem` used it at all,
only `State` and `Ui` did. `ProgramSettings` went whole rather than split:
`FileExtension`, `ProgramPath` and `AssociationRole` are the words, the
association list is the table they key, and separating them would have bought
nothing. `FileSystemPath` is the type that could not follow: its type is a bare
string, but `combine`, `name`, `directory` and `parent` all take an
`IFileSystem`, which is the line `Domain` is on the other side of. The change
is one `open` per consumer and no behaviour — four files under `Ui/` stopped
depending on `FileSystem` altogether, and the whole suite passes unchanged.
