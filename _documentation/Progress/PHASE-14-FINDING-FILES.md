# Phase 14 — Finding files

The tasks are in [Phase 14 — Finding files](../Plan/PHASE-14-FINDING-FILES.md).

**A fourteenth phase opened with a measurement, and it needed a new kind of
evidence.** P14.1 asked what becomes of `Alt+F7`, the key the search will use.
Every terminal tried hands it over — cmder, Windows Terminal, the Windows
console, and `gnome-terminal` under WSLg, which is a real VTE terminal and the
only piece of a Linux desktop this machine has — and it is lost all the same,
because GNOME's window manager claims `<Alt>F7` for moving a window and grabs it
before the terminal is sent anything. That half cannot be pressed without a
desktop, so it was read instead, out of
`org.gnome.desktop.wm.keybindings` with `gsettings`, which is a kind of evidence
the checklist did not have. `Alt+Shift+F` arrives everywhere and is the binding
the phase carries beside the classic name. The same reading turned up three keys
Marbas already binds in GNOME's own list — `Alt+F1`, `Alt+F2` and `Alt+F6`, the
drive choosers and the link key — which have no second binding and are therefore
unreachable on that desktop; that is a defect and is recorded below. The probe
also answered the question the results window asks: `Enter` reaches a
full-screen `Window` with no buttons, and does not reach a `Dialog`, where it
presses the default button first — so `F3`, `Enter` and a first `Esc` that is
held and a second that closes all work in a window shaped like the `F3` viewer,
which is what the window will be.

**P14.2 made the question a value, and made the combinations that mean nothing
unbuildable rather than checked.** `NameMasks` reads the typed field the way a
user of the classic managers expects: split at `;`, each piece trimmed, empty
pieces dropped, `*` and `*.*` meaning every name — which
`FileSystemName.MatchesSimpleExpression` does not do on its own, since `*.*`
there wants a dot and passes `Makefile` over — and a piece carrying neither `*`
nor `?` read as a fragment, so `report` is `*report*`. A mask is matched against
the name alone and always without regard to case; case belongs to the text and
to nothing else. The pieces become `NamePattern` values, so the matching is the
one `+` and `-` have always used and that type is untouched. What is searched
for is a `FindWhat` of two cases — a name with targets, or a name with a
`TextSearch` — which is what makes the impossible states impossible: the case,
the whole-word rule, the negation and the binary-file option live inside
`TextSearch` and so cannot exist without text, and a query carrying text answers
`FilesOnly` whatever targets it was built with, because a directory holds no
text. Beside them a `FindQuery` carries the scope — the six places of P14.5 —
along with `HiddenEntries`, whether links are followed and whether archives are
searched.

**P14.3 matched text without ever holding a file whole.** What it answers —
matched, did not match, skipped as binary, unreadable, stopped — is a `Domain`
word beside the query it answers, since nothing about it knows a file system
exists. `ContentMatch.search` settles the encoding from the first 8192 bytes with the same `TextEncoding` call
the viewer makes, then reads the rest in buffers of 64 KiB through a `Decoder`,
so a multi-byte sequence split across two reads still decodes. What could
straddle the boundary is carried over: the last length of the text plus one
character stay in front of the next buffer, which is what makes a match found
wherever it falls — the tests put one at every offset around 64 KiB and around
128 KiB. The plus one is for whole words, whose rule needs the character before
a match as well as the one after; a match ending exactly at a buffer's end is
deferred rather than decided, because the character that would settle it has not
been read yet, and a match at the very start of a carried-over piece is one the
previous buffer already rejected. Whether a file is binary is not a second guess
made here: `TextEncoding.detect` and `TextEncoding.contentOf` now come from one
`examine`, so the branch that reads a NUL byte as Latin-1 is the same branch that
calls the file binary, and the search and the viewer cannot disagree about a
file. A binary file is passed over unless the query asks for it, and asked for,
it is searched as the Latin-1 the viewer shows. "Not containing" is the negation
of the answer and nothing else: a file that cannot be read answers
`FileUnreadable` either way, so it never passes as one that lacks the text. Each
buffer asks the batch control first, so a stopped search answers `SearchStopped`
instead of a verdict it did not reach.

**P14.4 walked the disk, and it is the one walk in the app allowed through a
link.** `FileSearch.run` takes the places to start from and answers `Finished`
or `Stopped` with its counts of files, directories and skipped entries, handing
every hit to a function as it finds it — a full path and the same `Entry` a
directory listing gives, built by the very functions `DirectoryListing` builds
its own with, so what a hit shows and what the panel shows cannot drift. The
scope decides only how deep it goes: `DirectoryOnly` reads one directory's
entries, everything else walks the tree below each place it was given, which is
what makes the marked directories nothing more than several places. Every
directory entered is reported through the `BatchControl` the batches already
carry, so the summary of P14.8 has something to say and `Esc` has something to
answer; the control is asked again before each entry, so stopping takes effect
within one entry rather than one directory. A directory that refuses to be read
is counted and stepped over, the way `DirectoryMeasure` steps over one. Hidden
entries follow the query, and a concealed directory is not walked into either,
because a place the user cannot see is not a place they asked to search. Links
are the exception P13.8 did not make: with `FollowLinks` a directory link is
entered, but only if the path it resolves to has not been entered before — the
walk keeps the set of what it has entered, and the identity of a link is its
target, so a link pointing at its own ancestor is refused rather than walked. A
cycle therefore terminates with every real directory reported exactly once,
which `Fakes.LinkFollowing` — the mock that walks through a link the way a real
system does — proves, as it proves that a link leading out of the tree is
entered and that `StopAtLinks` never enters one at all.

**P14.5 gave the three scopes that do not start at the panel somewhere to
start.** `FindGround.resolve` turns a scope into the places to walk and the
roots not to cross: the panel's own directory for the two that start there — the
directory *holding* the archive when the panel stands inside one — the marked
directories as they were given, the root `Drives.holding` names for
`FromVolumeRoot`, every mounted root for `AllDrives`, and for `PathDirectories`
what was read off `PATH`. Only the volume root answers with roots not to cross,
and it is the plainest way to say what a volume is: every other listed root. The
walk refuses to enter one of them, so a search of `/` does not wander into the
memory stick mounted under `/media`, while `AllDrives` names them all as places
and crosses nothing, since there is nothing left to cross into. The mounts
P13.12 filters out are not listed at all and so are never entered, and the home
row is left out of `AllDrives` because it lies on a drive already walked.
`PATH` is read where it can be tested rather than where it is fetched:
`HostEnvironment` carries the raw string, `FindGround.pathDirectories` splits it
at the separator it is handed — `;` on Windows, `:` on Unix — trims the quotes
Windows allows around an entry, drops what is blank, names a directory once
however many times and in whatever spelling `PATH` repeats it, and leaves out an
entry that is not there. Those directories are walked without descending, which
is the second scope to join `DirectoryOnly` in staying where it was put.

**P14.6 opened the archives, and a hit stopped being a path.** A hit now carries
the place holding it and the entry standing there — a directory on disk, or a
level inside an archive — which is the `PanelLocation` the panels have used
since P9.10, so grouping the results by where they live and walking a panel to
one of them need no second vocabulary; `FindHit.spelled` puts the two back
together as `C:\work\backup.zip\docs\notes.txt` when a path is what is wanted.
The places a search starts from are locations too, which is what lets a panel
standing inside an archive search below the level it stands on rather than the
directory holding the archive, whether the archive option is on or off. On disk
the option decides: an archive the walk meets is a hit by its own name like any
file, and with `SearchArchives` its entries are matched at every level as well —
the file names from the index, the directory levels gathered as the entries go
by, implied ones included, since an archive that stores no entry for a folder
still has the folder. Text is read straight out of the entry, and that is what
settled the shape of the reading: the first attempt took the index as a list and
then asked each entry for its content, which fails, because the list outlives
the open archive and the content does not. So the archive is read in one pass
and each entry is answered while the reader stands on it, which also means one
entry in memory at a time rather than an index. An entry that is encrypted stops
the archive there and counts it skipped, since asking for a password once per
archive would put a prompt between the user and a disk full of them. An archive
inside an archive is a hit by name and nothing more. A zip and a tar.gz are
covered both ways, by name and by text.

**P14.7 put the question on the screen.** `Alt+F7` — `Alt+Shift+F` beside it,
since GNOME's window manager takes the classic key before a terminal sees it —
opens *Find* over the active panel: the name masks, opening on `*.*` and
selected so the first keystroke replaces them, the text to look for, the six
scopes as one option selector and the rest as check boxes. What P14.2 made
unbuildable is greyed out rather than refused: the four text options are closed
until there is text, the directories box closes once there is, and the marked
directories are closed while no directory is marked, which the update reads off
the panel as a `MarkedGround` rather than as a count. The hidden box opens on
what the panel conceals. A *Last results* button sits before *Cancel* and *Ok*,
disabled until P14.10 fills it — and because Terminal.Gui reads every button but
the last as a dismissal, it answers nothing today by the same rule `Esc` does.
What comes back is a `FindQuery` or nothing, built through `FindWhat.create`, so
a query carrying text asks about files alone whatever the directories box was
left on. Nothing runs yet: `FindEntered` reaches the update and changes nothing
until P14.8 gives it a search to start. The popup was rendered on this machine
at 56 by 17 columns, well inside an 80-column terminal, and the greying was read
off the screen rather than off a property: a closed option is drawn in grey where
an open one wears its hot key in yellow.

**P14.8 ran the query and filled a window while it ran.** `RunSearch` is the
port and `Adapters/SearchRunner.fs` the only thing that knows a thread exists:
the walk goes on a `Task` with the same `BatchControl` copying uses, the hits it
finds are queued, and a timer on the UI thread drains the queue ten times a
second — one `app.Invoke` per tick rather than one per hit, which is the rough
edge the progress window records. The window is a frameless full-screen
`Window`, run the way the viewer is run, because P14.1 measured that a `Dialog`
answers `Enter` with its default button before a handler sees it. Its list is
grouped: a header row spelling the directory a hit lives in — a level inside an
archive spelled `C:\work\backup.zip\docs` — and its hits indented beneath it,
with the name, the size and the date. `ResultRow.rows` is that grouping and it
is pure: groups keep the order the walk reached them, a hit for a group already
open joins it rather than opening a second one, and because the list is rebuilt
on each tick the cursor is put back on the row it stood on rather than on the
index it stood at, so a hit landing in an earlier group cannot move it. The
summary line says `Searching: <directory>` with what has been found so far,
`Finished` or `Stopped` with the counts the walk answered — the counts are never
stored twice: while it runs they are counted off the hits, and afterwards they
are the outcome's own. The first `Esc` stops a running walk and leaves the
window up over what it found, the next one closes it, and a walk that finished
on its own is closed by the first. Proved in the running application over this
repository: `Searching: …\linux-x64 — 18 files, 0 directories` at half a second,
`Stopped — 18 files, 0 directories, 187 skipped` after the first `Esc`, and the
window gone after the second. Where the search starts is still resolved outside
the walk: `Update` sends the query with the panel's location and its marked
directories, and `Shell` asks `FindGround.resolve` with the `PATH` directories
`Dependencies` read once at startup, splitting them at `;` on Windows and `:` on
Unix.

**P14.9 let the results be walked into and read.** `Enter` on a hit closes the
window and stands the active panel where that hit lives with the cursor on it,
whether it is a file or a directory — a directory hit lands *beside* it, not
inside it, because the panel stands where the hit was found. A hit the panel
conceals turns its hidden entries on first, the way `Alt+H` would, since the
user has just asked to stand on it; a hit inside an archive walks the panel into
that level, which needs nothing new, the location the hit carries since P14.6
being the one the panels have used since P9.10. `Enter` on a group header stands
the panel inside the directory the header names. `F3` views a file hit over the
results window — a hit inside an archive streamed as P9.11 streams it, its
password asked for first — and `Esc` in the viewer comes back to the list with
the cursor where it was; `F3` on a directory or on a header views nothing. What
the window answers is a `FindAnswer`: the landing, or nothing when it was left
by `Esc`, together with the hits and the row the cursor was on, which P14.10
will keep. It reaches `Update` as `SearchAnswered`, so every landing is a test
over the update rather than over a view. The `RunSearch` port grew the two
functions that make this possible — one to view a hit, one to take the answer —
the way `RunBatch` takes the work and what to do when it finishes, which keeps
the password question and the archive streaming in `Shell` beside the ones `F3`
in a panel already goes through: `ViewFile` and the new port now share one
`showPath`. Proved in the running application over this repository: the results
on screen, `F3` putting the viewer over them
(`FindSummary.fs  Text  UTF-8  1,117 B  line 1  64%`), `Esc` bringing the group
header back, and `Enter` closing the window with `AtHit … FindSummary.fs` at row
2.

**P14.10 made the answer outlive the window.** `AppState.LastSearch` holds a
`FindResults` — the query, the ground it ran over, the hits, whether the walk
finished or was stopped and the row the cursor was on — and every closing window
writes it, so the next search replaces the one before it. It belongs to the
session and not to a tab, and nothing of it reaches the settings file. The
*Last results* button of P14.7 carries the count, `Last results (123)`, and is
open only when there is something to reopen; because Terminal.Gui reads every
button but the last as a dismissal, it sets a flag of its own the way the drive
popup's hand-over key does, and the popup now answers a `FindChoice` — a query
or the last results — rather than a query alone. Reopening asks the file system
whether every hit is still there, drops the ones that are not and says how many
went, and the summary line names the query and where it ran
(`Last results: *.fs in C:\work — 5 files, 0 directories, 2 gone`), so a list
from half an hour ago cannot pass for a fresh one. The cursor comes back on the
row it left, clamped to what is still there. What made this cheap was giving the
runner one port with two ways to fill the window — `Walk` starts the task,
`Reopen` fills it from what was kept and starts nothing — so the window, its
keys, its grouping and its answer are the same in both. A window closed while
its walk still ran answers `Stopped` with the counts it had, since that is what
happened. Proved in the running application: a walk over `Ui/Find` left the
cursor on row 2 of five hits, and the window reopened from its own answer came
back on row 2 under that summary line.

**P14.11 made the answer a panel of its own.** `F2` in the results window — the
bar names it, and it is a key rather than a button for the reason P14.1 gives —
sends what was found into the active panel. `PanelLocation` has a third case,
`SearchResults`, holding the query, the ground, the hits, the location the panel
stood on before and the entry its cursor was on, and `SearchListing` turns those
hits into the entries a listing gives: each one named by its path below where
the search started — `App\Shell.fs` — or by its whole path when the scope had
more than one start, because two `README.md` from two directories must not look
like one. The name is the key to the hit, so nothing has to be carried beside
the listing: `SearchListing.hitFor` reads it back, and that is what gives `F3`
the file to view, `Enter` the program to run and `..` the way home. The title
says `Find: *.fs in C:\work`, `..` returns to the location stood on before with
the cursor where it was, `Enter` on a directory goes into that real directory
and leaves the results behind, and sorting, hidden entries, marking, the quick
search, the colours and the footer work as they do on a directory, since the
entries are the entries a directory gives. The panel is watched by nothing,
which is what `PanelLocation.watched` now answers with an option; every listing
of it asks the file system whether each hit is still there and drops the ones
that are not, so `Ctrl+R` needs no rule of its own. The location the panel
returns to is what the session file keeps, what a tab opened beside it opens on
and what `Alt+F7` searches from — the results are a place to look at, never a
place to come back to. Operations on the marks are P14.12; until then `F5`,
`F6`, `F7` and `F8` answer such a panel with silence rather than with a
sentence. Proved in the running application over `Marbas\Ui`: the window filling
under `F2 Panel`, the panel afterwards titled `Find: *.fs in
C:\projects\Marbas\Marbas\Ui` over `App\AppView.fs`, `Dialogs\FindDialog.fs` and
sixty more, and `Enter` on `..` back in `C:\projects\Marbas\Marbas\Ui`. The same
run turned up a hole the new key made easy to reach and `Enter` on a hit could
already fall into: a window closed while its walk still ran was written to by
the gathering timer and by the walk's own last `app.Invoke` after it had been
disposed. `SearchRunner` now asks whether the window is still showing before
either of them touches it.

**P14.12 let the keys act on what a results panel shows.** The operation paths
of such a panel are read off each entry's own hit rather than off the panel's
directory joined to a name, so the marks can lie in as many directories as the
search found them in: `F5` copies them into the other panel, `F6` moves them
there, `F8`, `Shift+F8` and `Alt+Del` send them to the trash, delete them for
good or wipe them, and `F4` edits the one under the cursor — each in one batch,
through the effects a directory already raises, since what reaches them is a
list of paths on disk. Afterwards the listing is read again, and because a
results panel lists only the hits still on disk, what was moved, deleted or
wiped leaves it without a rule of its own. Which marks stay is decided by the
file names a batch answers with, as it always was; a results panel translates
its shown names back to those, and keeps the mark of an entry whose name
failed *or* never finished, so two hits called `README.md` of which one failed
both stay marked rather than both losing the mark. What needs the panel itself
to be a directory refuses by name before a popup opens — `F7`, `Shift+F4`,
`Shift+F1`, `Shift+F2`, `Alt+F6` and a bare name typed for `F6`, which would
be a rename — and so do `F5`, `F6`, `Shift+F1` and `Shift+F2` from the other
panel when a results panel is where they would land. Each refusal is a
`ResultsRefusal`, a word in `Domain`, carried by the one new
`OperationError`, `NotInSearchResults`, so the dialog says which command it
was. A marked hit inside an archive refuses the batch it would be part of with
`HitInsideArchive` and a sentence naming it, which P14.13 removes. A path typed
for `F6` that is not a bare name is read from the directory the results panel
returns to. The same change mended `F3` on a hit inside an archive in a results
panel, which P14.11 had sent to the viewer as a path on disk that does not
exist: an archive hit now has no operation path at all, so `F3` streams the
entry the way it does inside an archive panel. The refusal was rendered in an
80-column terminal, the widest of the new sentences at 59 columns.

**P14.13 let the marks of a results panel lie inside archives too.** Every
operation had assumed one source place, and the marks of a results panel can
lie on the disk and in several archives at once. `TransferSource` therefore has
a third case, `FromPlaces`, holding one group per place — the disk, or one
particular archive — in the order the listing first reaches them, and a
results panel whose marks lie in one place answers that place's own case, so
marks in one zip alone take exactly the way an archive panel takes. Three
effects carry the groups, `CopyPlaces`, `MovePlaces` and `DeletePlaces`, and
`Shell` turns each group into a piece of work through the path its place
already had: copying and moving on the disk, extracting out of an archive for
`F5`, the extract-then-drop of P9.13 for `F6`, removing entries for `F8`, and
into an archive the add and the carry across of P9.14. Every question is asked
before the batch starts — the overwrite question per group, and the password
once per archive, since an archive is one group — and all the pieces then run
in one batch behind one progress window, each asking the control before it
starts, so `Esc` stops the whole of it. The capability gate is asked per
group: an archive of a kind that cannot be changed answers one failure named
after the archive, and the rest of the batch still runs, which is what moving
a mark on the disk, one in a zip and one in a tar shows. A password turned down
leaves its archive out the same way. Removing from an archive has no trash, so
`F8` with any mark inside one asks to delete for good, the way `F8` inside an
archive panel always has; `Alt+Del` stays a wipe for what lies on the disk and
removes the entries. `HitInsideArchive`, the refusal P14.12 left behind, is
gone. For the listing afterwards to hold only what is still there, a hit inside
an archive now survives only while the archive's index still names it — the
index read once per archive per listing — and one whose index cannot be read
at all stays while the archive file does, rather than vanishing from a list the
user did not ask to empty. The tests run the real archive code over the mock
file system: a tar stands in for the plan's rar, being just as readable and
just as unchangeable, because nothing in the toolchain can write a rar.

**P14.14 wrote the phase down where the rest of the design is.**
`Architecture/FINDING-FILES.md` is new: what a query is and why `*.*` is read
the classic way, how text is matched in a stream and why binary files are an
option, the walk and the six scopes with what each refuses to cross, the
archives, the popup, the results window and why its hits stay out of the state
while the walk runs, the last results, the results panel, and the operations on
it across the disk and the archives. [Links](../Architecture/LINKS.md) gained the
one walk that may follow a link and the set that keeps it from spinning;
[State and effects](../Architecture/STATE-AND-EFFECTS.md) gained `LastSearch`, the
third `PanelLocation` and `FromPlaces`; [UI and input](../Architecture/UI-AND-INPUT.md)
gained the popup, the results window with its keys and the `Alt+F7` row of the
key table; [Cross-platform](../Architecture/CROSS-PLATFORM.md) gained a *Finding
files* section with the `PATH` separator and the mount boundary; the
[user guide](../USER-GUIDE.md) gained the command; and the overview lists the new
topic and the phase's modules in the code map, `Ui` now counted as nine
folders. Writing the boundary down turned up a defect the P14.5 paragraph above
had described away: the walk is bounded by the *listed* drives, P13.12 keeps
`/proc`, `/sys` and their like off that list, so a search from the root of the
volume on Linux walks into them. It is recorded in [Rough edges](../ROUGH-EDGES.md),
beside the one blind spot of P14.12's marks.
