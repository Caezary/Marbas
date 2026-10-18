# Tabs

Phase 11. One Marbas holds several pairs of panels and remembers them between
runs.

**A tab is the pair, not a panel.** `Tab` carries the left panel, the right one
and which of the two is active, so switching a tab changes both panels and the
focus at once. Panels are what a tab holds; `Programs` stayed on `AppState`,
because the table `Enter` reads belongs to the session and not to a place. That
is also why the settings file keeps it outside the `tab.` keys. The name `F7`
offers was kept the same way until P16.9 moved it into the history.

**"There is always exactly one tab in front" is a fact of the type.**
`AppState.Tabs` is a `Zipper<Tab>` — the tabs before the current one, the
current one, and the tabs after it — so no state can have zero tabs, no index
can point past the end, and *the last tab cannot be closed* needs no check:
`Zipper.remove` on a lone item answers the same zipper, because there is nothing
to remove into. The alternative, a list and an index, would have put a guard in
`closeTab`, a clamp in `switchTab` and an empty-list case in `Settings.parse`,
three places where the type now says it for us. `Zipper` lives in
`Marbas.Collections`, below every other layer, because `Settings` in `Profile`
needs it as much as the state does.

**The panels are read through the tab in front, and the outside sees no
difference.** `AppState.Left`, `.Right` and `.Active` are members that answer
from `Zipper.current this.Tabs`, so `Update` and every test kept reading the
state exactly as before P11.1 — one field access became one member call and
nothing else moved. What it costs is type annotations: F# infers a record type
from field labels but cannot infer one from a member, so the functions in
`AppState` that take a bare `state` say `(state: AppState)` now. `Settings` got
the same three members for the same reason.

**A new tab is a clone of the one it is opened from, without its marks.**
`Alt+F11` copies the current tab — both locations, both cursors, both sort
orders — clears the marks and the open search, and inserts it *after* the
current tab, which then becomes the one in front. A mark is an operation in
progress, not a property of a place, so it does not travel; a cursor is a
property of a place, so it does.

**Switching reads the tab it lands on.** A tab that has been sitting in the
background may be standing in a directory that has since gone, so `TabSwitched`
and `TabCycled` reload both of its panels the way `Ctrl+R` reloads one, keeping
each cursor on the entry it stood on and the marks that survived. When the
directory is gone the switch still happens and the loss is reported: the state
after a failure is the tab that was asked for, not a silent return to where the
user came from. Startup therefore loads only the tab in front, and the others
the first time they are reached.

**The focus follows the side the tab left active.** Switching, walking or
closing a tab changes `AppState.Active` without a keystroke ever reaching the
other panel, but Terminal.Gui sends keys to whichever view holds its focus, and
`Enter` opens the entry under the cursor of the side the state calls active.
Left as it was, the focus stayed on the panel it had been on, so the arrows
moved one cursor and `Enter` opened the other's entry. `AppView.render`
therefore ends by moving the focus to the active side whenever the opposite
panel holds it. It moves the focus only away from the other panel, never from
a dialog or from nothing: at startup the focus still decides which side is
active, through `PanelFocused`, as it always has.

**A panel standing inside an archive is a place like any other, until the
session is written.** In memory a tab may hold `InArchive(archive, level)` on
either side, and cloning, switching and the popup all treat it as they treat a
directory. `AppState.settings` puts every panel through
`PanelLocation.holding`, so what the file records is the directory holding the
archive — the rule since P9.10, now applied per tab. A restored session
therefore opens beside the archive rather than inside it, which is a place that
is certainly still there.

**The keys, and why there are two for three of them.** `Alt+F11` opens a tab,
`Alt+Shift+F11` closes it, `F11` lists them, `Alt+←` and `Alt+→` walk between
them without a popup, and `Shift+F9` writes the session on its own. `F11` is
one a terminal argues about — Windows Terminal and the Windows console keep it
for full screen — so `Alt+T` is bound beside it, exactly as `Alt+H` stands
beside `Ctrl+H` and `Alt+W` beside `Ctrl+PgDn`. gnome-terminal under WSLg
argues about the other two: `Alt+F11` is its full screen and `Alt+Shift+F11`
does not arrive, so since P22.7 `Alt+Z` opens a tab and `Alt+Shift+Z` closes
it — a letter with `Alt` and the same letter with `Alt+Shift`, the pair the
function keys make. `KeyMap` left five letters free in both shapes — O, Q, S, Y
and Z — and all five arrived in every terminal measured; Q was set aside
because `Alt+Shift+Q` would close a tab one key away from `Ctrl+Q`, which ends
the session. `Ctrl+Tab`, the obvious key for
walking between tabs, is spoken for by the terminals, and `Ctrl+T`, the obvious
spare for the popup, is what cmder keeps for its own new console. See
[Cross-platform](CROSS-PLATFORM.md), *Keyboard and terminal*, for what was
measured where.

**The popup is a port, and the row is Ui's business.** `TabsRequested` raises
`ShowTabs` carrying the `Zipper<Tab>` itself — the tabs and which is current in
one value, so nothing has to be paired up again — and `Prompts.ChooseTab`
answers with the position that was picked or with nothing. `TabRow.describe`
turns one tab into `2.   C:\projects\Marbas          C:\backup`, and
`TabDialog` is a list with `Cancel` and `Ok`; `Enter` on the list accepts
through the default button, which was measured against a running application
rather than assumed.

**Every tab command writes the session.** `TabOpened`, `TabClosed`,
`TabSwitched`, `TabCycled` and `SessionSaveRequested` all answer with
`SaveSettings`, the same effect `QuitRequested` has always ended with, so the
file on disk follows the tabs rather than waiting for the exit. Closing the only
tab is a key that changes nothing and still writes: the rule "these keys save"
is simpler than an exception to it, and writing an unchanged session costs
nothing.

**The counter is a label over the border, not a bar of its own.** `[2/3]` is
drawn at the top right corner — the top border of the right panel, the line its
title is written on — because a row of its own would cost a row of listing.
Since the two share a line, the title gives way: `PanelTitle.describe` takes the
width it may use, the right panel's width is reduced by the counter, and
`PathText.shorten` cuts a path that will not fit down to `C:\...\parent\current`
and then, if the panel is narrower still, to `C:\...\current` and `...\current`.
The sort marks are never shortened — a panel that cannot say where it stands can
still say how it sorts. A panel that has not been laid out yet has no width to
measure, and shortens nothing.

See also [Settings persistence](SETTINGS-PERSISTENCE.md) for the shape of the
file and [State and effects](STATE-AND-EFFECTS.md) for where the transitions
sit.
