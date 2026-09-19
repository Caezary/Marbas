# Phase 11 — Tabs

Goal: one Marbas holds several pairs of panels and remembers them between runs.
A tab is the pair — the left panel, the right one, and which of the two is
active — so switching one changes both panels at once. `F11` lists them,
`Alt+F11` opens one on the directories already on screen, `Alt+Shift+F11` closes
the current one, and the last one cannot be closed. The session file grows from
one pair into a numbered list, and every command that opens, switches or closes
a tab writes it, which is also all `Shift+F9` does.

What the terminals really deliver was measured before any of these keys was
bound, the way P10.7 measured `Enter` with a modifier. Five runs of
`_probes/KeyProbe` — cmder, Windows Terminal, cmd, and a WSL session started
from each of the first and the third — agree on everything but two keys. `F11`
never reaches the application under Windows Terminal or under cmd, which both
keep it for full screen, while cmder passes it through; and a WSL session
inherits whatever the terminal hosting it does, so it is the host and not the
platform that decides. `Ctrl+T`, the obvious second binding, is the one cmder
keeps for its own new-console popup. Everything else — `Alt+F11`,
`Alt+Shift+F11`, `Shift+F9`, `Alt+Left`, `Alt+Right`, `Alt+T`, `Alt+Shift+T` and
`Ctrl+F11` — arrives everywhere. The popup therefore gets two bindings, `F11`
because that is what the key bar advertises and `Alt+T` because that is what
works where `F11` is eaten; the other four keys need one each.

- **P11.1 A tab is a value** — `Tab` (the left panel, the right panel, the
  active side) and `Tabs`, a zipper of the tabs before it, the current one and
  the tabs after it, so "there is always exactly one current tab" and "the last
  tab cannot be closed" are facts of the type rather than checks at runtime.
  `AppState` keeps `LastCreatedDirectory` and `Programs`, which belong to the
  session and not to a place, and hands the panels to the current tab: `panel`,
  `updatePanel` and a new `activeSide` are the funnel every transition already
  goes through, so `state.Active` becomes `activeSide state` and nothing else in
  `Update` moves. `openTab` clones the current tab without its marks — a mark is
  an operation in progress, not a property of a place — and `switchTab` and
  `closeTab` complete the set. *Done when:* every existing test passes
  unchanged, and new ones cover opening, switching, and the close the only tab
  refuses.
- **P11.2 Tabs in the session file** — `Settings` carries a list of tabs and
  which one is current instead of one pair of panels: `tab.1.left.path`,
  `tab.1.left.sort`, `tab.1.left.direction`, `tab.1.left.hidden`, the same four
  for the right, `tab.1.active`, and one `tabs.current`. A file with no `tab.`
  key is read as a single tab from the `left.`/`right.` keys an earlier version
  wrote — the way P10.1 reads a role-less editor line — and the first save
  rewrites the file in the new spelling, so there is nothing to migrate. A panel
  standing inside an archive is written as the directory holding it, as it has
  been since P9.10, and a stored path that is no longer a directory falls back
  per panel, so one removed drive costs one panel and not the file. *Done when:*
  a config written before this task loads as one tab, three tabs survive a
  restart, and a hand-edited file still opens.
- **P11.3 Opening, closing and saving** — `TabOpened` on `Alt+F11` and
  `TabClosed` on `Alt+Shift+F11`, each answering with the new state and
  `SaveSettings`, and `Shift+F9` for the save on its own, which writes without a
  dialog because a save that succeeded has nothing to say. Closing the only tab
  is not a failure to report but a key that does nothing: the type cannot even
  express the state it would produce. *Done when:* a new tab opens on the
  directories already on screen, the file on disk changes after each of the
  three keys, and the last tab stays.
- **P11.4 Switching** — `F11` and `Alt+T` raise `ShowTabs`, a `ChooseTab` port answers with
  the tab that was picked or with nothing, and `TabSwitched` moves the zipper and
  saves. `TabDialog` lists one row per tab — its number, its left location and
  its right one — opening on the current one, `Enter` choosing and `Esc`
  changing nothing. `Alt+Left` and `Alt+Right` cycle without the popup;
  `Ctrl+Tab` is spoken for by the terminals. Switching reloads both panels of
  the tab it lands on, since a tab that has been sitting in the background may
  be standing in a directory that has since gone, and startup therefore loads
  only the current tab and the others when they are first reached. *Done when:*
  a tab reached from the popup or with `Alt+Right` shows the directories as they
  are now, and the session is written.
- **P11.5 The counter and the shortened title** — how many tabs are open sits in
  the top right corner as `[2/3]`, which is the top border of the right panel and
  therefore the line its title is written on, so the title gives way:
  `PanelTitle.describe` takes the width it may use and shortens a path that will
  not fit to `C:\...\parent\current`, keeping the root, the directory the panel
  stands in and the one holding it, because those are what tell one panel from
  another. Read off a rendered screen, not off a property of the view. *Done
  when:* the counter is legible with one tab and with several, a long path is
  shortened rather than clipped, and the sort marks keep their place.
- **P11.6 Cursors in the file** — `tab.1.left.cursor=<name>` and its right-hand
  twin, resolved on load the way `FocusTarget.ByName` already resolves a focus
  request and simply not written for an empty panel. A name that is no longer
  there leaves the cursor at the top rather than refusing the tab. Marks are
  still not saved: they belong to an operation, and an operation does not
  survive a restart. *Done when:* every tab comes back with both its cursors
  where they were, and a renamed entry costs nothing but the position.
- **P11.7 The keys and the platforms** — what the probe found becomes a row of
  its own in [Cross-platform](../Architecture/CROSS-PLATFORM.md): which terminal
  keeps `F11`, which keeps `Ctrl+T`, and that a WSL session inherits the
  terminal it was started from rather than answering for Linux. The second
  binding it argues for is already in the one table `KeyMap` holds, so what is
  left is the measurement repeated against the finished keys. *Done when:*
  opening, closing and listing tabs are each reachable in cmder, in Windows
  Terminal and in cmd, and under a WSL session started from any of them.
- **P11.8 The documentation** — `Architecture/TABS.md` says what a tab is, why
  the zipper, what a new one inherits and what becomes of a panel standing
  inside an archive; [Settings persistence](../Architecture/SETTINGS-PERSISTENCE.md)
  and [State and effects](../Architecture/STATE-AND-EFFECTS.md) gain what the file
  and the transitions grew, and the user guide gains the four keys. *Done when:*
  the phase is described where the rest of the design is, not only in this plan.
