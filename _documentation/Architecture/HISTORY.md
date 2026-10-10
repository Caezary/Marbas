# History

What the application remembers of where the user has been and what they did,
and how it hands it back. The decisions behind the phase — its own file, the file
as the source of truth, the moment stamped by the shell, global capped lists,
nothing checked on show, places rather than listings, text rather than values —
are set out in [Phase 16](../Plan/PHASE-16-HISTORY.md); this file records what
the code does about them as it arrives.

## The history and its file

**A history is four kinds of list, each a list of `Remembered` items.**
`History` holds `Folders` (a `Place`), `Launches` (a `LaunchRequest`),
`FileVisits` (a `Place` and whether it was `Viewed` or `Edited`) and `Inputs`, a
map from `InputKind` to the `EnteredText` typed into that kind of field. Every
item carries `At`, the `DateTimeOffset` it was last used, and every list runs
newest first. A kind of field nothing was typed into has no key in the map at
all, and `History.inputs` answers it with an empty list, so an emptied list and
one never filled are the same value.

**`Place` is the part of `PanelLocation` a history can keep.** It is `OnDisk` or
`InArchive` — a directory or a file on disk, an archive and a path inside it —
and has no case for search results, which are a listing rather than a place. It
is declared with `RequireQualifiedAccess` and spelled `Place.OnDisk` wherever it
is used, since `PanelLocation` already owns the bare `OnDisk` and `InArchive`
and both types are in scope wherever a panel is turned into a place.

**`EnteredText` keeps what was typed, not what it means.** It is the one
constructor in `Domain` that does not trim: a field hands back exactly the text
it was given, and whether that text is a name, a pattern or a path is decided
where it is decided today, after the popup is confirmed. It refuses a blank text
and one that breaks a line, because the file below cannot hold the second.

**Every change is one of three, and they are values.** `HistoryChange` is
`Remember item`, `Forget item` or `ClearList list`, and `History.apply` is the
pure function that performs one. Remembering puts the item on top with its
moment, drops the entry it replaces, and keeps the hundred most recent; forgetting
drops the entry the item names. What "the same" means differs per list, and is
what the item points at rather than the whole value: a place is the same place
by `FileSystemPath.sameEntry`, so on Windows `C:\Work` and `C:\work` are one
entry and on Unix they are two; a launch is the same launch by its target, so a
file started as administrator after being started as the user leaves one entry,
started the latest way; a file visit is the same by its place, so a file viewed
and then edited is one entry, now `Edited`; typed text is the same only when it
is spelled the same. An item with a line break in any path it would write is
not remembered at all — `apply` answers the history unchanged.

**The file is `history.conf`, beside the settings.** `History.beside` takes the
settings path and answers its sibling, so the rule for where the configuration
lives — application data, or `$HOME/.config` — stays in `Settings.location`
alone. `Dependencies` carries the answer as `HistoryPath`, and a `Clock` beside
it for the moment `Shell` will stamp on each change.

**Each entry is a numbered block, and a block is read whole or not at all.**

```
folder.1=C:\work
folder.1.at=2026-09-28T14:05:00.0000000+02:00
folder.2=C:\backup.zip
folder.2.inside=docs/2024
folder.2.at=…
run.1=C:\tools\setup.exe
run.1.program=installer.exe
run.1.directory=C:\tools
run.1.elevation=administrator
run.1.at=…
view.1=C:\notes.md
view.1.use=edited
view.1.at=…
input.target.1=D:\backup
input.target.1.at=…
```

The input lists are `input.target`, `input.mkdir`, `input.newfile`,
`input.pattern`, `input.find.masks`, `input.find.text`, `input.bytes`,
`input.archive`, `input.link` and `input.program`. `input.find.text` is the one
list two fields share: the text *Find files* looks for in the files and the
text `F7` looks for in the viewer are the same kind of thing, so each offers
what the other searched for. The bytes `F7` looks for in the hex dump are not,
and keep `input.bytes` to themselves. The moment is written in the round-trip
format, offset included. A value is everything after the first `=`, untrimmed,
so a typed `a=b` survives. Numbers are read in ascending order and need not
start at one or run without gaps, the way the tab blocks of the settings are
read. Where the settings file degrades one *setting* at a time, this one
degrades one *entry* at a time: a block missing a part it is always written
with — the moment, a launch's directory or elevation, a visit's use — or
carrying one that cannot be read, such as an `inside` that climbs, is passed
over, and the blocks around it are kept. A launch's program is the one optional
part.

**A change reads the file, applies itself and writes the file back.**
`History.change` is `load`, `apply`, write, in a `result` expression, and answers
the history it wrote. So the file is the source of truth: a change made by
another copy of Marbas since this one last looked is in what `load` reads, and
this change lands on top of it rather than on a stale copy. A file that cannot
be read ends the change with an error before anything is written, so a history
that is there but unreadable is never replaced by one that is empty. A missing
file is an empty history; the directory it lives in is made on the first write.
`HistoryError` is `HistoryAccessDenied` or `HistoryFailed` with the path and the
system's message, and `HistoryError.mapException` is the one place an exception
becomes one — the same shape as `SettingsError`, kept apart from it because the
sentence a user reads for "the history could not be written" is a different one.

## Remembering the directories

**A panel that lands somewhere new is remembered, and it is found by comparing,
not by asking every transition.** `Update.update` runs `transition`, then
`watching`, then `remembering`. `remembering` turns both panels of the tab in
front into a `Place` before and after the message — `Place.ofLocation` answers
nothing for search results — and for each side whose place is new appends
`ChangeHistory(Remember(Visited place))`. That is the shape `watching` has had
since P12.2, for the same reason: `EntryOpened` alone moves a panel four ways,
and `WalkIntoRequested`, `PathOpened`, `DriveChosen`, `VolumeRootRequested`,
`SearchAnswered` and every other way a panel moves would each have had to raise
the effect themselves. With the comparison they raise nothing and cannot forget
to. A listing that fails leaves the panel where it stood, so it is not
remembered either.

**A few messages move a panel without the user moving it, and they are named.**
`movesPanelsItself` lists them: `ExternalChangeDetected`, whose panel walks up
when its directory vanishes; `RefreshRequested`, a re-read that may do the same;
and the four tab messages, which put a different tab in front and so change
what both panels show without either of them going anywhere. For those the
comparison is skipped. A sort, a hidden toggle and a cursor movement need no
entry in that list — the place does not change.

**The shell stamps the moment and hands the file's answer back.**
`Shell.interpret` answers `ChangeHistory` with `History.change` over
`deps.HistoryPath` and `deps.Clock()`, then dispatches `HistoryChanged` with the
history the file now holds, which becomes `AppState.History`, or
`HistoryUnwritten` with the error. The state is therefore a copy of the file as
of the last change, not something the transitions build up themselves; the
popup of P16.4 reads the file afresh when it opens. The state starts empty: the
first change reads the file, so nothing at startup has to.

**A history that cannot be written says so once.** Every change of directory
would otherwise raise the same dialog. `AppState.HistoryReport` is `Unreported`
until the first `HistoryUnwritten`, which answers `Report(Problem.History …)` and turns
it `Reported`; every later one is passed over for the rest of the session. The
dialog is `HistoryErrorMessage`, titled *History not saved*, naming the file.

## The popup

**The popup is asked for by the state and filled by the shell.**
`HistoryRequested` answers `ShowHistory tab` with the tab last used in the
session, which `AppState.HistoryTab` keeps and which starts on
`FolderHistoryTab`. `Shell` reads the file afresh with `History.load` — so what
another copy remembered since is there — dispatches it as `HistoryChanged`, and
hands it to `Prompts.ChooseHistory` with the tab and `deps.Clock()`. The answer
is a `HistoryAnswer`: the tab the popup was left on, always, and a
`HistoryChoice` when a row was chosen. `HistoryAnswered` stores the tab and, for
`OpenPlace place`, lands the active panel on `Landing.inside place` through
`Update.landingOn`, the one function a search's group header, a favourite and a
path from the clipboard land through as well, so a directory that is gone is a
listing error and the panel stays. The comparison of P16.3 then remembers
the place again, which moves it to the top. A file that cannot be read is
reported and the popup is not shown.

**`HistoryDialog` is `Tabs` over three `TableView`s, set up the way P16.1
measured.** Each table has its `CollectionNavigator` removed, full-row
selection, and two columns, *When* and *What*. The buttons are `[Close]`,
`[Delete]`, `[Go to]` and `[Open]`, built with `LiteralText.showing` so none
carries a hot key; `[Open]` is the default, and the answer is read from
`Dialog.Result`, the index of the button pressed — `OpenButton` is 3. `KeyDown`
takes `Ctrl+PgDn` and `Ctrl+PgUp`, which walk the tabs round, and `Alt+G`,
which presses `[Go to]` only while it is enabled. In this task `[Delete]` and
`[Go to]` stay disabled: the first waits for P16.7, the second means nothing for
a directory and waits for the tabs of P16.5 and P16.6. `[Open]` on those two
tabs chooses nothing yet.

**The rows are shortened to the table, not cut at it.** `HistoryRow.moment`
shows a moment of today as its time and an earlier one as its date, read in the
offset of `now` rather than the one it was written with. `HistoryRow.place`
spells a level inside an archive as the archive's path followed by the level.
A path wider than the table is shortened in the middle by `PathText.shorten`,
the rule the panel titles use, from a `RepresentationGetter` on the *What*
column that reads the table's width when it draws — so the row keeps its root
and its last directories, and the table keeps its right-hand frame, which a
column wider than the table had pushed off. The table's own outer lines are
switched off, since the tab's frame already surrounds it, and the tab lines are
single to match. The strip still misdraws its joints after a switch; that is
`Tabs` itself and is recorded in [Rough edges](../ROUGH-EDGES.md).

## Remembering the files started

**A start is remembered once it has happened, and only a start of a file.**
`LaunchFinished` carries the `LaunchRequest` it answers beside the outcome, so
the transition that learns a start succeeded also knows what was started. It
raises `ChangeHistory(Remember(Launched request))` when the outcome is `Ok` and
the target is a file — `FileSystemPath.kind` says `FileEntry`. That one test
covers the three ways a file is started — `Enter`, a program chosen in `Alt+R`
and `Alt+Shift+R` — and leaves out the two ways `RunProgram` hands the system a
directory: `Alt+R` on a directory and on a drive, which show it in the system
file manager rather than start anything. Marking the request at each call site
would have been the other way; the kind of the target is the fact that decides,
and the transition may read it. A start that failed, and one whose elevation
was declined, raise nothing.

**Starting again is the same request, and going to it is standing beside it.**
On the *History* tab `[Open]` answers `RunAgain request`, which the state turns
straight back into `RunProgram request`: the same target, program, working
directory and elevation as the first time, and the start that follows is
remembered again, which moves it to the top. `[Go to]`, and `Alt+G`, answer
`GoTo place`; `Landing.beside` turns a file's place into a `Beside` — the
location holding it and its name, a directory on disk or the level of an
archive above an entry — and `Update.landingOn` loads that location with the
cursor on the name. The root of an archive has no name to stand beside and
lands nowhere.
`[Go to]` is enabled only while the tab in front has a row — on *Folder
history* it answers `OpenPlace`, the same as `[Open]`; it follows the tab through `Tabs.ValueChanged`. That handler also runs while the
dialog is being disposed and its tables removed, so the tab in front is read
from the `Id` of `Tabs.Value` rather than by finding the tables again.

**A command run from the user menu shares the list.** Since P20.4
`History.Launches` holds `Started` values — `StartedFile` of a
`LaunchRequest` or `StartedCommand` of a `CommandRun` — so the files started
and the commands run are one list, newest first, under one limit and one
*History* tab. `HistoryItem` gained `Ran of CommandRun` beside `Launched`, and
`History.startedItem` turns a row back into the item `Forget` needs. Two file
starts are the same entry when they name the same file, as before; two command
runs are the same entry only when everything about them is — the label, every
word as expanded, the list file, the way of running, the directory and the
rights — so the same entry run on other files is a row of its own.
`CommandFinished Ok` raises `ChangeHistory(Remember(Ran run))`, a failed run
and a declined elevation raise nothing. `[Open]` answers
`RunAgain(StartedCommand run)`, which the state turns back into
`RunCommand run`: the same words, not the line expanded again against the
panels as they are now. `[Go to]` answers `OpenPlace` of the directory it ran
in, since a command has no single file to stand beside.

A command run is written under the `run.` keys, told apart from a file start
by a `kind` part a file start never has, so a file written before P20.4 reads
as it did:

```
run.3=Pack
run.3.kind=command
run.3.command=7z
run.3.command.quoting=bare
run.3.argument.1=a
run.3.argument.1.quoting=bare
run.3.argument.2=C:\work\b c.txt
run.3.argument.2.quoting=quoted
run.3.list=C:\Temp\Marbas\4f2a\marked-1.txt
run.3.list.1=C:\work\a.txt
run.3.mode=shell-keep
run.3.directory=C:\work
run.3.elevation=administrator
run.3.at=2026-10-07T14:05:00.0000000+02:00
```

A run without its label, its program, its way of running, its directory or
its rights is passed over, costing that entry alone; a missing quoting reads as
bare. A run any of whose words or paths breaks a line is not remembered, as a
file start whose path breaks one is not.

**The row says how the file was started.** `HistoryRow.launchWhat` follows the
path with the program's name, without its directory, in brackets when one was
chosen, and a third column, *How*, holds `HistoryRow.launchHow`: `admin` when
it ran so, nothing otherwise. A command run is shown by
`HistoryRow.startedWhat` as its label, a colon and `ExpandedCommand.spelled` —
the words as they ran, every one that was quoted or had a path put in it in
double quotes. P16.4–P16.6 put the program into *How* as well;
the refinements at the end of this file moved it next to the path, so *How*
could be as narrow as its longest word.

## Remembering the files viewed and edited

**A file is remembered when it is asked for, not when it is closed.** The
transition that raises `ViewFile`, `ViewArchiveEntry` or `EditFile` raises
`ChangeHistory(Remember(Opened visit))` in front of it, so `Shell` writes the
file before the viewer or the editor runs — a console editor holds the screen
until it is closed, and a crash inside it loses nothing. Unlike a start, which
learns only afterwards whether it happened, viewing and editing are remembered
on the way in. `F3` on a file remembers it `Viewed`, inside an archive as
`Place.InArchive`; `F4` and `Shift+F4` remember it `Edited`. `F3` on a directory
measures it, `F4` on one shows its properties, and `F4` on an archive entry is
refused: none of the three hands a file to anyone, and none is remembered.

**One function turns a visit into its effect, from the panel and from the
popup alike.** `Update.reopening` maps a `FileVisit` to `ViewFile`,
`ViewArchiveEntry` or `EditFile` with the editor the extension names, or to the
`ArchiveEntryNotEditable` refusal the panel already reports. `visiting` puts the
history change in front of that effect, but only while the file — or the
archive holding the entry — is still on disk: a visit chosen from the popup
whose file is gone is handed to the viewer or the editor all the same, and is
not moved to the top of the list with a fresh moment for a file that is no
longer there. From the panel the file is there unless it vanished since the
listing, so the check costs one look at the disk and settles only that case.

**The popup answers `Revisit visit`, with the use the key asked for.** The
*View history* tab gains the *How* column, `V` or `E` from `HistoryRow.visitHow`.
`[Open]` answers the row as it is; `F3` and `F4` answer it as `Viewed` or
`Edited` whatever it says. `Dialog.Result` refuses any value that is not the
index of a button — P16.6 tried an index past the last one and got
`ArgumentOutOfRangeException` — so the two keys press `[Open]` and leave the use
they asked for in `HistoryView.AskedUse`, the way `DriveDialog` keeps
`HandingOver`, and `answer` reads it beside `Result`. They act only while *View
history* is in front and has a row; elsewhere they are passed over and the popup
stays open. `[Go to]` and `Alt+G` answer `GoTo` with the visit's place, which
`Landing.beside` already turns into the archive level above an entry.

## Forgetting and clearing

**The popup changes the file in place, through a function `Shell` hands it.**
`HistoryPrompt` carries `Change`, a `HistoryAfter`: the history after a change,
or nothing when it could not be written. `Shell`
builds it from the same `changeHistory` that answers the `ChangeHistory`
effect: `History.change` over `deps.HistoryPath`, stamped by `deps.Clock()`,
with the result dispatched to the state as `HistoryChanged`, or
`HistoryUnwritten` on failure. So only `Shell` touches the file, the file stays
the source of truth, and the state's copy follows every change. What the popup
does not get is a decision from a transition, and it needs none — forgetting a
row is the whole of the request. The first shape of P16.7 went the other way:
the popup closed with the row to forget, the state raised the change and asked
for the popup again, and everything the popup held that the file does not —
the tab, the row, the scroll, and in P16.8 a typed filter — had to be carried
across the reopening. Done in place, none of it moves.

**`Del` forgets the row under the cursor, and the tables are filled again.**
`Del`, and `[Delete]`, turn the row into the item it stands for — `Visited`,
`Launched` or `Opened`, the value `Remember` stored, so `History.apply` finds it
by what it points at — and hand `Forget item` to `Change`. The history that
comes back becomes `HistoryView.History`, every tab is filled from it again, so
the tab reads what the file holds, anything another copy added since included,
and the cursor goes back to the row number it was on: the entry below the
forgotten one, or the new last row. A change the file refused answers `None`
and the rows stay; the `HistoryUnwritten` it dispatched is queued behind the
popup, and the dialog of P16.3 follows when the popup closes, once per session.
`[Delete]` is a dialog button, and a dialog button closes the dialog; its
`Accepting` is marked handled, which a running `Application` proved keeps the
popup open.

**`Shift+Del` asks inside the popup.** `HistoryDialog.create` also takes a
`confirm: title -> question -> bool`, which `Adapters/Prompts` fills with the
same `MessageBox.Query` every other question uses; it runs over the popup, and
the popup stays open whatever it answers. *No* is the default button, so
`Enter` does not clear by accident, and `Esc` closes only the question. A *Yes*
hands `ClearList` of the tab's list — `LaunchHistory`, `FileHistory` or
`FolderHistory` — to `Change`. Both keys, and `[Delete]`, are live only while
the tab in front has a row, which is checked again after every change.

**`Tabs.ValueChanged` still fires while the dialog is disposed.** The handler
that enables `[Go to]` and `[Delete]` is given the tab container and the two
buttons when the dialog is built, rather than finding them in the dialog: the
rewrite that looked them up again broke every dialog test at the moment of
disposal, the way P16.5 found.

**A row stood on is scrolled to.** `SetSelection` moves the cursor but not the
view, so after a change the table is scrolled to the cursor explicitly, and
every table also calls `EnsureCursorIsVisible` on `FrameChanged`, which fires
once it is laid out and again on a resize, and never on scrolling.

## The filter

**The filter is the popup's own state, a `SearchText`.** `HistoryView.Filter`
is `SearchText option`, the type the quick search of a panel already uses, and
it is kept by the popup alone: it lives no longer than the popup, and since
P16.7 changes the file in place nothing has to carry it anywhere. A row is kept
when `SearchText.foundIn` finds the text in its *What* or its *How* — the full
path, not the one shortened to the table, and the program and elevation of a
start — ignoring case the way the panel does, by `OrdinalIgnoreCase`. The
*When* column is not searched: its spelling changes with the day it is read.
The filter narrows every tab when the tables are filled, but a tab switch
clears it, so only the tab in front is ever narrowed; the line under the table
is a `Label` reading *Filter:* and the text, or nothing.

**A filtered row knows which entry it stands for.** Each `Row` carries `Entry`,
its position in the history's list, and the table's source is asked for the
row under the cursor — `EnumerableTableSource.GetObjectOnRow` — rather than the
cursor's position being taken as the entry's. `Open`, `F3`, `F4`, `Go to` and
`Del` all go through it, so each acts on the entry the row shows. A change
fills the tables again under the same filter, so a row forgotten leaves the
filter in place; `Shift+Del` still clears the whole list, since the question
names the tab. `[Go to]` and `[Delete]`, and `F3` and `F4`, follow the rows the
table shows rather than the entries the list holds, so a filter that leaves
nothing disables them.

**Keys.** A printable key without `Ctrl` or `Alt` — `KeyMap.printable`, now
shared with the panel's quick search — appends to the filter, the space
included; `Backspace` shortens it and drops it with its last character; `Esc`
clears a filter that is not empty and is marked handled, so the popup stays,
and with no filter it is left alone and the application's quit binding closes
the popup, as a running `Application` confirmed. The cursor goes to the first
row after every keystroke. Switching tabs clears the filter from
`Tabs.ValueChanged`, which also runs while the dialog is disposed, so the
handler refills the tables it was given when the dialog was built rather than
looking them up.

**An empty table eats letters.** P16.1 found that a `TableView` takes letters
until its navigator is removed. With the navigator gone it still takes them
once it has no rows: driven through a running `Application`, the letter that
emptied the table reached the dialog and the next one did not — the table's
`KeyDown` saw it and something inside the table marked it handled. So every
table forwards printable keys to the filter from its own `KeyDown`, which runs
before the table acts on them, and the dialog's `KeyDown` keeps the same
branch for a key that arrives while a button has the focus.

## The fields that ask one question

**Five popups remember, in four lists.** The `To:` of `F6` keeps
`CopyOrMoveTarget`, the name of `F7` keeps `NewDirectory`, the name of
`Shift+F4` keeps `NewFile`, and the pattern of `+` and `-` keeps one
`SelectionPattern` list between them. The plan named the `To:` of `F5` as well,
but `F5` asks nothing — it copies into the other panel straight away — so the
list of targets is the field of `F6` alone.

**The shell reads each list when it asks, and remembers when the popup is
confirmed.** `TargetPrompt`, `NamePrompt` and `PatternPrompt` carry
`Remembered`, the `EnteredText` of their kind newest first, which `Shell` reads
from `history.conf` with `History.load` just before asking — the state's copy
of the history is empty until its first change, so it cannot be the source. A
file that cannot be read offers an empty list rather than stopping the popup.
The ports now answer the text as it was typed, a `string option`, and the
meaning is read in `Shell` after that: `EntryName.tryCreate` for a name,
`NamePattern.tryCreate` for a pattern, the target as it already was. Before
reading it, `Shell` remembers the text through the same `changeHistory` the
history popup uses, so a text is remembered whether or not the operation then
succeeds; `EnteredText` refuses a blank one, so a blank confirmation remembers
nothing, and a cancelled popup never reaches the shell's branch at all.

**`F7` remembers in the state, once the directory exists.** A name that fails —
taken, refused by an archive — must not become the next suggestion, and only
the transition knows whether the directory was made. `NameEntered` answers a
successful make with `ChangeHistory(Remember(Typed(NewDirectory, name)))`, and
the suggestion `F7` opens with is the head of that list, which the shell hands
the prompt as `Suggestion`. So `LastCreatedDirectory` left `AppState` and
`Settings`, `PromptForName` lost its argument, and `mkdir.name` is no longer
written; a stale line is passed over like any other unknown key.

**`HistoryField` is a `DropDownList` with the four cures of P16.1.** It is a
`TextField` underneath, so `PromptDialog` takes it in the place of the plain
field through `PromptDialog.offering`, and everything that reads the field —
`PromptDialog.text`, `selecting` — reads it unchanged; the password still uses
the plain `PromptDialog.create`, since a secret is never remembered.

- `Space` is removed from the field's bindings, so it types a space; `F4` and
  `Alt+↓` still toggle the list.
- The list is a popover of the application, not a subview of the dialog, and a
  row chosen with `Enter` comes back to the field as its `Accept`. While the
  field's own list is the active popover — `App.Popovers.GetActivePopover()`,
  whose `Target` is the field — `Accepting` is marked handled, which keeps the
  row and stops the dialog's default button.
- The row lands in `Text` after `Accepting`, once the list is already hidden,
  and `App.Invoke` ran the move at once, before the text arrived — measured
  through a running `Application`. So `Accepting` marks the field as choosing,
  and the `TextChanged` that follows moves the insertion point to the end; any
  key typed into the field clears the mark, so a row equal to the text already
  there does not leave it behind.
- `Esc` on the open list closed the whole dialog, because the application's
  quit binding sees it before the popover does. `HistoryField.apply`, called
  once in `Session.run` beside `DialogStyle.apply`, subscribes to
  `app.Keyboard.KeyDown`, which runs first, and hides the active popover when
  it belongs to a `DropDownList`.

**The list is as wide as the field and wears the field's colours.** The plan
said the dialog's; rendered, a list in `White` on `Blue` with no frame could not
be told from the dialog under it, and being only as wide as its longest row it
let the buttons show through beside it — `D:\older targetCancel ] [► Ok ◄]`.
The popover's list asks for the `Menu` scheme, which nothing else in Marbas
uses, so `DialogStyle.apply` registers `ClassicScheme.input` under it: rows
`Black` on `White`, the chosen one on `Aqua`, like the field it drops from.
`HistoryField.apply` also listens to `PopoverRegistered` and gives the list of
a field the field's width, so it covers the dialog in one block.

**The newest row is chosen when the list opens.** The control pre-selects the
row equal to the text and nothing otherwise, so `Enter` on a list just opened
closed it and kept the text — a `+` confirmed that way selected `*` rather than
the pattern shown. When the list becomes visible with no row chosen, the first
one — the newest — is, and `Enter` then picks it.

**The arrows still walk the list of a closed field.** In a popup of one field
they have nothing else to do, so the bindings stay; in the larger dialogs
P16.10 removes them, as the next section describes.

## The fields of the larger dialogs

**Five more fields, each with a list of its own.** The masks and the text of
`Alt+F7` keep `FindMasks` and `SearchedText`, the archive path of `Shift+F1`
keeps `ArchiveTarget`, the place of `Alt+F6` keeps `LinkDestination`, and the
program of `Alt+R` keeps `OpenWithProgram`. Each is a `HistoryField` built by
`HistoryField.amongOthers` and dressed by `DialogStyle.typedInto` like the plain
field it replaces; the password of `Shift+F1` stays a plain secret field and is
never remembered. The dialogs take their lists as arguments, and the prompts
carry them — `PackPrompt.Remembered`, `LinkPrompt.Remembered`,
`HandlerPrompt.Remembered`, and `FindPrompt.Masks` and `.Texts` — read by
`Shell` from `history.conf` just before asking, as in P16.9.

**The arrows keep moving between the fields.** Measured through a running
`Application` before the change, `↑` and `↓` walked the focus through each of
the four dialogs — the masks, the text and the first scope in `Alt+F7`, the
target and the format options in `Shift+F1` and `Alt+F6`, the known programs,
the program and the check box in `Alt+R`. A `DropDownList` binds the two to
walking its list, which would have stopped that at the first field. So
`amongOthers` removes the two bindings, and the same measurement after the
change moved the focus through exactly the same views; the list of a field in a
larger dialog opens only with `Alt+↓` or `F4`. The one-field popups of P16.9
keep the walk, since there the arrows have nothing else to do.

**What is remembered is what the popup hands back, read where it is read
today.** `ArchiveChoice.Target` and `LinkChoice.Target` are already the text of
the field, so `Shell` remembers them as they come. `FindChoice` carries a parsed
query, so the find port answers `FindAsked` — the choice beside the masks and
the text as typed, which the adapter reads with `FindDialog.typed` — and a
search remembers both; asking for the last results remembers neither, and an
empty text is refused by `EnteredText`. `OpenWithChoice` carries a
`ProgramPath`, which trims the spaces around what was typed; the program is
remembered as that path, which differs from the text only by those spaces, and
spares the port a fourth shape.

## The controls, measured before use

P16.1 rendered the controls the phase leans on — `Tabs` holding a `TableView`
per list, and an editable `DropDownList` — inside a `Dialog` dressed by
`DialogStyle`, and drove them with synthetic keys through a running
`Application`, the way the `render-tui-screen` skill does. Terminal.Gui 2.4.17
answered as follows, and each answer is a constraint on a later task.

**`Tabs` switches on its own keys, not on the ones the plan named.** `←` and `→`
walk the tabs, and so do `Tab` and `Shift+Tab`, while `↑` and `↓` move the cursor
of the table in front; `Ctrl+PgUp` and `Ctrl+PgDn` are not handled at all and
reach the dialog's `KeyDown` untouched. P16.4 therefore binds those two in the
dialog and sets `Tabs.Value` itself, and keeps the arrows as they come. A
`TableView` with `FullRowSelect` does not take `←`/`→` from `Tabs`.

**A `TableView` eats letters until its navigator is removed.** Its
`CollectionNavigator` answers every printable key by looking for a row that
starts with it, and reports the key handled whether or not a row was found, so
the dialog never sees it. With `CollectionNavigator <- null` — the same thing
`TabDialog` and `DriveDialog` do to their `ListView` through
`KeystrokeNavigator = null` — letters, digits, `Space` and `Backspace` all reach
the dialog, which is where the filter of P16.8 reads them. `Home` and `End` stay
with the table and leave the cursor on the row it was on, rather than taking it
to the first or the last one.

**A button's hot key fires on a bare letter.** With the navigator gone, typing
`d` pressed `_Delete` and closed the dialog: once no focused view takes a
letter, Terminal.Gui treats it as the hot key of whichever button carries it,
without `Alt`. A popup that filters by typing therefore gives its buttons no hot
keys at all, and `Alt+G` for `[Go to]` is bound in the dialog's `KeyDown` the way
`DriveDialog` binds `Alt+R` — so the letter reaches the filter and the key with
`Alt` still acts.

**`Enter` presses the default button, and `Result` says which.** `[Open]` made
the default is pressed by `Enter` whatever row is under the cursor, which is the
behaviour P16.4 wants. `Dialog.Canceled` is not the way to tell the buttons
apart: it answered `true` for `[Go to]` as well as for `Esc`, so the popup reads
`Dialog.Result`, the index of the button pressed, the way `DriveDialog` does.
`Del`, `Shift+Del` and `Backspace` are not taken by the table and reach the
dialog. `Esc` closes it.

**The dialog frame, the tab headers and the table draw three line styles.** The
dialog is `LineStyle.Double`, the tab headers are rounded and the table's grid
is single. The colours follow the dialog scheme — `White` on `Blue`, the cursor
row and the header of the tab in front `Black` on `Aqua` — so nothing there
needs a scheme of its own; the headers want `TabLineStyle` settled in P16.4.

**An editable `DropDownList` opens its list on `Space`.** `Space` is one of its
default bindings next to `F4` and `Alt+↓`, which in a field meant for paths
makes `Program Files` impossible to type. Removing that one binding from the
instance, `KeyBindings.Remove Key.Space`, lets it type a space again and leaves
`F4` and `Alt+↓` opening the list.

**`↑` and `↓` walk the history of a closed drop-down.** Each press replaces the
text with the next or previous item of the list, without opening it. In a
dialog of one field that is the history of a shell and costs nothing; in the
larger dialogs of P16.10 it takes away the arrows that move between fields, and
that task removed the two bindings there, keeping the arrows for the fields.

**`Enter` and `Esc` on the open list act on the whole dialog.** `Enter` on a row
puts it into the field and then presses the default button, so choosing a
remembered target would also start the copy; `Esc` closes the dialog rather
than the list. Both are cured outside the control: the field's `Accepting` is
marked handled while the list has focus, which keeps the choice and stops it
there, and an `Esc` that arrives while the list has focus is turned into the key
that closes the list. With both guards the list behaves as a list — `Enter`
chooses, `Esc` closes it, and the next `Enter` confirms the dialog. Pressed by
hand during the runs of the key probe, whose field carries no guard, the
unguarded `Enter` closed the popup as well, so this is the control's
behaviour and not an artefact of synthetic keys; P16.9 does not ship a field
without the guard. The `Esc`
guard has to sit on `app.Keyboard.KeyDown`, because the list lives in a popover
that is not a subview of the dialog and the dialog's `KeyDown` never sees it.

**The list keeps the stock colours and the width of its longest item.** It is
drawn `Charcoal` on `LightBlue`, bold, without a frame, as wide as its longest
row rather than as the field; the arrow on the right of the field is `Black` on
`Aqua`. P16.9 gives it the dialog's colours. A remembered item chosen from the
list also leaves the insertion point where it was, so the next letter lands in
the middle of the path rather than after it — P16.9 moves it to the end.

## Refining the popup

**`↑` and `↓` stay on the list; `←` and `→` always walk the tabs.** A
`TableView` that cannot move its cursor any further leaves the arrow unhandled,
and `Tabs` takes it: `↑` on the first row put the tab before in front. Each
table's `KeyDown` now marks `KeyMap.historyRowUpKey` on the first row and
`historyRowDownKey` on the last — or on an empty table — as handled, so they
never leave the table. `KeyMap.nextHistoryTabArrow` and
`previousHistoryTabArrow` join `Ctrl+PgDn` and `Ctrl+PgUp` in `tabStep`, read
both by the tables' `KeyDown` and by the dialog's, so the arrows walk the tabs
wherever the focus is rather than only while `Tabs` happens to see them.

**The tab headers are of one width, and the one in front is not lit.** Each
header is the `TitleView` of the table's `BorderView`; `BorderView.TabLength`
fixes its width and `TextAlignment.Center` centres the title in it.
`HistoryDialog.headerLength` answers the width from the width of `Tabs`: the
longest title with three columns of room on each side where the three headers
fit, less where they do not, and never less than the longest title, so on a
very narrow screen the strip scrolls rather than cuts a title.
`dressHeaders` sets it on every `FrameChanged` of `Tabs`. The `TitleView` draws
its title in the `Focus` role while its tab has the focus, which made the
header of the tab in front look selected like a row;
`ClassicScheme.tabHeader` gives it the dialog's white on blue for both roles.

**The columns keep their widths.** `ColumnStyle.MinWidth` and `MaxWidth` are
set equal on every column: *When* to the length of `HistoryRow.DatePattern`, so
a date never pushes the columns aside, *How* to the length of
`HistoryRow.AdministratorNote`, which also holds `V` and `E`, and *What* to
what the table's width leaves, which `PathText.shorten` then fits the path to.
`fitColumns` runs when a table is filled and on its `FrameChanged`, since the
width of *What* follows the width of the popup.

**`[Go to]` on a directory opens it.** A directory's place is where the panel
should stand, not beside, so on *Folder history* `[Go to]` and `Alt+G` answer
`OpenPlace` like `[Open]` rather than being disabled.
