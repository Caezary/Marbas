# Finding files

Phase 14. What a search asks, how it walks, where its answer lives while it
arrives and afterwards, and how that answer becomes a panel the ordinary keys
act on.

## The question

**A query is a value, and the combinations that mean nothing cannot be built.**
`FindQuery` in `Domain` carries what is looked for, the scope, `HiddenEntries`,
whether links are followed (`LinkWalk`) and whether archives are entered
(`ArchiveSearch`). What is looked for is a `FindWhat` of two cases: `NameOnly`
of the masks and the targets — files alone, or files and directories — and
`NameAndText` of the masks and a `TextSearch`. The case, the whole-word rule,
the negation and the binary-file option live inside `TextSearch`, so they cannot
exist without text; and a query carrying text answers `FilesOnly` whatever the
popup's directories box was left on, because a directory holds no text.
`FindWhat.create` is the one way to build it, and the popup goes through it.

**`NameMasks` reads the field the way a user of the classic managers expects.**
The text is split at `;`, each piece trimmed and empty pieces dropped. `*` and
`*.*` both mean every name — which `FileSystemName.MatchesSimpleExpression` does
not do on its own, since `*.*` there wants a dot and passes `Makefile` over —
and a piece with neither `*` nor `?` is a fragment, so `report` is `*report*`.
An empty field matches everything. The pieces become `NamePattern` values, so a
mask is matched by the same function `+` and `-` have always used. A mask sees
the name alone and ignores case, always; case belongs to the text and to
nothing else.

## Matching text

**A file is never held whole.** `ContentMatch.search` settles the encoding from
the first `TextEncoding.ProbeLength` bytes (8192) with the same
`TextEncoding.detect` the viewer calls — a
byte order mark, then the zero pattern of UTF-16, then a NUL meaning Latin-1,
then valid UTF-8 — and reads the rest in buffers of 64 KiB through a `Decoder`,
so a multi-byte sequence split across two reads still decodes. A UTF-16 file is
searched as text rather than as bytes that happen to spell nothing.

**A match is found wherever it falls.** The last length of the text plus one
character is carried in front of the next buffer, so a match straddling a
boundary is seen whole; the tests put one at every offset around 64 KiB and
128 KiB. The extra character is for whole words, whose rule needs the character
*before* a match as well as the one after. A match ending exactly at a buffer's
end is deferred rather than decided, since the character that settles it has
not been read yet, and one at the very start of a carried piece is one the
previous buffer already turned down.

**Binary files are an option because the answer is not obvious.** Whether a file
is binary is not a second guess made here: `TextEncoding.detect` and
`TextEncoding.contentOf` come from one private `examine`, so the branch that
reads a NUL as Latin-1 is the branch that calls the file binary, and the
search and the viewer cannot disagree about a file. A binary file is passed over
unless the query asks for it (`FileSkipped`, counted), and asked for, it is
searched as the Latin-1 the viewer shows — which is what finds a string in an
executable, and also what floods a search with noise, hence off by default.

**"Not containing" is the negation of the answer and nothing else.** A file that
cannot be read answers `FileUnreadable` either way and is counted as skipped, so
it never passes as a file that lacks the text. Each buffer asks the batch
control first, so a stopped search answers `SearchStopped` rather than a verdict
it never reached.

## The walk

**`FileSearch.run` walks the places it is given and hands every hit over as it
finds it.** It takes the `FindGround` — the places to start from and the roots
not to cross — and a function to report hits through, and answers `Finished` or
`Stopped` with its counts of files, directories and skipped entries. A hit is a
`FindHit`: the `PanelLocation` holding it and the `Entry` a listing would give
for it, built by the very functions `DirectoryListing` builds its own with, so
what the results show and what a panel shows cannot drift.

**The scope decides only where the walk starts and how deep it goes.** Every
directory entered is reported through the `BatchControl` the batches carry, so
the summary line has something to say and `Esc` something to answer, and the
control is asked again before each entry, so stopping takes effect within one
entry. A directory that refuses to be read is counted and stepped over, the way
`DirectoryMeasure` steps over one. Hidden entries follow the query, and a
concealed directory is not walked into either: a place the user cannot see is
not a place they asked to search.

**It is the one walk in the application allowed through a link.** With
`FollowLinks` a directory link is entered, but only if the path it resolves to
has not been entered before; the walk keeps the set of what it has entered, and
a link's identity is its target. See [Links](LINKS.md), *The one walk that may
follow a link*.

## The scopes

`FindGround.resolve` turns a scope into places. It takes the panel's location,
its marked directories and the `PATH` directories `Dependencies` read once at
startup.

| Scope | Starts at | Descends | Refuses to cross |
|-------|-----------|----------|------------------|
| `FromDirectory` | the panel's location — a level inside an archive when it stands in one | yes | nothing |
| `DirectoryOnly` | the same | no | nothing |
| `MarkedDirectories` | each marked directory | yes | nothing; a marked file is ignored, and with none marked the scope is greyed out |
| `FromVolumeRoot` | the root `Drives.holding` names for the panel — the disk holding the archive inside one | yes | every other listed root, so `/` does not wander into `/media/user/stick` |
| `AllDrives` | every root `Drives.list` gives, the home row excepted since it lies on a drive already walked | yes | nothing — there is nothing left to cross into |
| `PathDirectories` | each directory on `PATH` | no | nothing |

The mounts P13.12 filters out are never listed, so `AllDrives` never starts at
one — but for the same reason they are no boundary either, and a search of `/`
walks into `/proc` and `/sys` like any directory; see
[Cross-platform](CROSS-PLATFORM.md), *Finding files*. `PATH` is
read where it can be tested rather than where it is fetched: `HostEnvironment`
carries the raw string, and `FindGround.pathDirectories` splits it at the
separator it is handed — `;` on Windows, `:` on Unix — trims the quotes Windows
allows round an entry, drops what is blank, names a directory once however often
and in whatever spelling `PATH` repeats it, and leaves out an entry that is not
there.

## Inside the archives

**With the option on, an archive is a hit by its own name and a place besides.**
Its index is read once and every entry at every level is matched by name — file
names from the index, directory levels gathered as the entries go by, implied
ones included, since an archive storing no entry for a folder still has the
folder. With text, each file entry is streamed into `ContentMatch`, which for a
tar.gz means decompressing it, a price the option says it pays.

**The archive is read in one pass.** The first attempt took the index as a list
and then asked each entry for its content, which fails: the list outlives the
open archive and the content does not. So each entry is answered while the
reader stands on it, which also keeps one entry in memory at a time rather than
an index. An encrypted entry stops that archive and counts it skipped — asking
for a password per archive would put a prompt between the user and a disk full
of them — and an archive inside an archive is a hit by name and nothing more.

## The popup

`Alt+F7` — `Alt+Shift+F` beside it, since GNOME's window manager takes the
classic key before the terminal sees it — opens *Find* over the active panel:
the masks, opening on `*.*` selected so the first keystroke replaces them, the
text, the six scopes as one option selector and the options as check boxes.
What the query type makes unbuildable is greyed out rather than refused: the four
text options until there is text, the directories box once there is, and the
marked directories while none is marked, which `Update` reads off the panel as a
`MarkedGround` rather than a count. The hidden box opens on what the panel
conceals. A *Last results (123)* button sits before *Cancel* and *Ok*, open only
when there is something to reopen; because Terminal.Gui reads every button but
the last as a dismissal, it sets a flag of its own the way the drive popup's
hand-over key does, and the popup answers a `FindChoice` — a query, or the last
results. It measures 56 by 17 columns, rendered, and the greying was read off the
screen: a closed option is grey where an open one wears its hot key in yellow.

## The results window

**The window is built like the viewer, because a dialog would steal `Enter`.**
P14.1 measured that a `Dialog` presses its default button before a handler sees
`Enter`, while a frameless full-screen `Window` with no buttons delivers it, so
`ResultsWindow` is the second shape and reaches everything by keys named in its
bar: `Enter` stands on the row, `F2` sends the list to the panel, `F3` views a
file, and `Esc` or `F10` leave — the first `Esc` stops a walk still running and
leaves the list up, the next closes it.

**The list is grouped by where a hit lives.** `ResultRow.rows` is pure: a header
row spelling the directory — `C:\work\backup.zip\docs` for a level inside an
archive — and its hits indented beneath it with the name, size and date. Groups
keep the order the walk reached them, a hit for a group already open joins it,
and since the list is rebuilt on each tick the cursor is put back on the row it
stood on rather than the index, so a hit landing in an earlier group cannot move
it. The summary line says `Searching: <directory>` with the counts so far,
`Finished` or `Stopped` with the walk's own counts afterwards — counted off the
hits while it runs and never stored twice.

**While the walk runs, the hits live in the window and not in the state.**
`RunSearch` is the port and `Adapters/SearchRunner` the only thing that knows a
thread exists: the walk runs on a `Task` with the batches' `BatchControl`, hits
are queued, and a timer on the UI thread drains the queue ten times a second —
one `app.Invoke` per tick rather than one per hit. Putting them into `AppState`
instead would mean a dispatch and a whole-screen render per tick for panels
nobody can see under the window, and a state that holds half an answer every
transition would then have to reason about. So the state learns about a search
once, when the window closes: it answers a `FindAnswer` — a `FindLanding` and
the `FindResults` — which reaches `Update` as `SearchAnswered`, and everything
that follows is a test over the update. The runner asks whether the window is
still showing before either the timer or the walk's last `Invoke` touches it,
since a window closed while its walk ran has been disposed.

**What the window answers decides where the panel goes.** `AtHit` stands the
active panel where the hit lives with the cursor on it — beside a directory hit,
not inside it — turning hidden entries on first if the hit is concealed, and
walking into the archive level for a hit inside one. `AtDirectory`, from a group
header, stands the panel inside that directory. `InPanel`, from `F2`, makes the
list the panel's own listing. `F3` is not an answer: it views a file hit over
the window through the same `showPath` the panel's `F3` uses, asking an
archive's password first, and `Esc` in the viewer comes back to the list.

## The last results

`AppState.LastSearch` holds the `FindResults` of the last window closed — the
query, the ground, the hits, whether the walk finished and the row the cursor
was on. It belongs to the session, not to a tab, is replaced by the next search
and is not written to the settings file. Reopening asks the file system whether
every hit still exists, drops the ones that do not and says how many went, and
the summary line names the query and where it ran
(`Last results: *.fs in C:\work — 5 files, 0 directories, 2 gone`), so a list
from half an hour ago cannot pass for a fresh one. The runner fills the window
two ways through one port — `Walk` starts the task, `Reopen` fills it from what
was kept and starts nothing — so the window, its keys and its answer are the same
for both.

## The results panel

**A results panel is a third kind of place.** `PanelLocation` has
`SearchResults` beside `OnDisk` and `InArchive`, carrying a `FoundEntries`: the
query, the ground, the hits, the location the panel stood on before and the
entry its cursor was on there. `FindHit` and `FindGround` moved into the same
recursive definition, because a hit names a location and this location holds
hits. `SearchListing` turns the hits into the entries a listing gives, so
sorting, hidden entries, marking, the quick search, the colours and the footer
need nothing new.

**The name is the path, and the path is the key back to the hit.** Each entry is
named by its path below where the search started — `App\Shell.fs` — or by its
whole path when the scope had more than one start (`AllDrives`,
`PathDirectories`, several marked directories), because two `README.md` from two
directories must not look like one. `SearchListing.hitFor` reads the name back
into the hit, so nothing is carried beside the listing: that is what gives `F3`
its file, `Enter` its program or directory, and every operation its path. The
title says `Find: *.jpg in C:\photos`.

**Every listing asks what is still there.** A hit on disk survives while its
path exists; a hit inside an archive survives while the archive's index still
names it, the index read once per archive per listing, and one whose index
cannot be read at all stays while the archive file does. So `Ctrl+R` and the
reload after an operation drop what was moved or deleted without a rule of
their own.

**A results panel is a place to look at, never a place to come back to.** It is
not watched — `PanelLocation.watched` answers nothing for it — since a result can
span more directories than a watch should hold. `..` returns to the location
stood on before with the cursor where it was; `Enter` on a directory goes into
that real directory and leaves the results behind; `Enter` on a file runs it as
P10.3 does. The session file keeps the location it returns to, a tab opened
beside it opens there, and `Alt+F7` from it searches from there too — a second
search sent to the panel keeps the first way home.

## Operations on the results

**The operation paths come from each hit, not from the panel.** The marks can
lie in as many directories as the search found them in, so `F4`, `F5`, `F6`,
`F8`, `Shift+F8` and `Alt+Del` take each entry's own path and act into the other
panel's directory where they have a target. What needs the panel itself to be a
directory refuses by name before a popup opens: `F7`, `Shift+F4`, `Shift+F1`,
`Shift+F2`, `Alt+F6`, and a bare name typed for `F6`, which would be a rename.
So do `F5`, `F6`, `Shift+F1` and `Shift+F2` from the other panel when a results
panel is where they would land. Each refusal is a `ResultsRefusal` in `Domain`,
carried by `OperationError.NotInSearchResults`, so the dialog names the command.
A path typed for `F6` that is not a bare name is read from the directory the
panel returns to.

**Marks in several places are one batch of several groups.** `TransferSource`
has a third case, `FromPlaces`, holding one group per place — the disk, or one
archive — in the order the listing first reaches them; marks in one place alone
answer that place's own case, so marks in one zip take exactly the way an archive
panel takes. `CopyPlaces`, `MovePlaces` and `DeletePlaces` carry the groups, and
`Shell` turns each into a piece of work through the path its place already has:
copying and moving on disk, extracting for `F5`, the extract-then-drop of
[Archives](ARCHIVES.md) for `F6`, removing entries for `F8`, and into an archive
the add and the carry across. Every question is asked before the batch starts —
the overwrite question per group, the password once per archive — and then all
the pieces run in one batch behind one progress window, each asking the control
before it starts, so `Esc` stops the whole of it.

**The capability gate is asked per group, and the rest still runs.** An archive
of a kind that cannot be changed answers one failure named after it, and a
password turned down leaves its archive out; the other groups go ahead. An
archive has no trash, so `F8` with any mark inside one asks to delete for good,
as it always has inside an archive panel; `Alt+Del` stays a wipe for what lies on
disk and removes the entries.

**Which marks stay is decided by file names, and that has one blind spot.** A
batch answers its items by file name, as it always has; a results panel
translates its shown names back to those and keeps the mark of an entry whose
name failed *or* never finished. Two hits called `README.md` of which one failed
therefore both stay marked — too many marks rather than too few. Carrying a path
in `Operations.ItemResult` would close it, at the price of every place that
builds one.

## Left for a later phase

The popup opens on `*.*` every time, and `LastSearch` does not outlive a
restart; remembering the last query and a history of masks and texts is its own
phase. Excluding masks in the classic spelling (`*.* | *.bak`), a depth limit,
regular expressions, text across a line break, and the viewer opening at the
match are listed in [PLAN.md](../PLAN.md).
