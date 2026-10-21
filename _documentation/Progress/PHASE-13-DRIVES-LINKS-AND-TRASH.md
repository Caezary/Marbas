# Phase 13 — The drive, the link and what is left behind

The tasks are in [Phase 13 — The drive, the link and what is left behind](../Plan/PHASE-13-DRIVES-LINKS-AND-TRASH.md).

**A thirteenth phase opened with a measurement.** P13.1 asked the terminals what
they deliver before anything was bound to them, and the five runs — cmder,
Windows Terminal, the console, and a WSL session started from the first and from
the third — agree on all of it. `Alt+Del` arrives everywhere, and so does
`Alt+Shift+Del`, the stand-in it turned out not to need. `Ctrl+\` never arrives
as a backslash: the terminal sends the control byte `0x1C`, which is what `Ctrl`
with a backslash has meant since the teletype, and the driver decodes it into the
other key that produces the same byte, `Ctrl+4`. `Ctrl+/` is `0x1F` and comes
through as `Ctrl+7` for the same reason, so nothing downstream can tell the
punctuation from the digit. The root of the volume will therefore read `Ctrl+4`
and keep `Ctrl+\` bound beside it, while `Alt+\`, the spare the phase had
planned, arrives as nothing at all. The probe also grew a real modal dialog,
since P13.15 needed to know whether a key reaches a popup rather than the panel
behind it: `Alt+R` does, in all five, and `Shift+Enter` does not — it is the bare
`Enter` P10.7 already found in the panel, which on a popup means the default
button, so the drive popup binds `Alt+R` alone.

**P13.2 put the root of the volume one key away.** `VolumeRootRequested` sends
the active panel to the root of whatever it is standing on, through the same
transition a path opened from the root chooser goes through, so the cursor rules
and the watch of P12.2 hold without a second way in. Where a panel stands is
read as the location the operating system can report on — which is the archive
file when a panel is inside an archive, so `Ctrl+\` leaves the archive for the
root of the disk holding it — and a panel already on a root answers by staying,
rather than re-listing the directory it is already in and losing the cursor. The
key is `Ctrl+4`, the one the terminals deliver, with `Ctrl+\` bound beside it
for a driver that ever spells it out.

**P13.3 put a number on a directory.** `DirectorySize.measure` walks everything
below one place — a directory on disk or a level inside an archive, since
`PanelLocation` already says which — and answers a value that knows what it is:
an exact total, or a lower bound where something was skipped. Three things make
it a lower bound rather than a lie: a subdirectory that refuses to be read, a
walk the batch control stopped part way, and an archive that cannot be opened at
all. Each directory entered is reported through the same `BatchControl` copying
uses, so P13.4 has a progress window to fill and an `Esc` to answer. A directory
link is a leaf: the link is counted as nothing and never walked into, which is
what keeps a link pointing at its own ancestor from spinning — P13.8 will make
the same rule hold for every other walk in the app. An archive is measured from
its index, read once and summed below the level asked for; reading it level by
level would re-open the file once per directory in it. The value it answers is a
`Domain` type, because P13.4 needs it on a listing entry, which is read long
before anything can be measured; the walk itself stays in `FileSystem` as
`DirectoryMeasure`.

**P13.4 put the measurement on the screen, on the key that had nothing to say.**
`F3` viewed a file and did nothing at all on a directory; it now measures the
operation targets every other key takes — the marks, or the entry under the
cursor — and the answer lands in two places: the size column, in place of the
`<DIR>` the row wore, and the status line under the panels. A lower bound wears
a `≥` in both. The walk runs behind the progress window through a port of its
own, since the batch runner speaks in the item results copying and deleting
answer with, and measuring answers with sizes; `Esc` leaves the panel exactly as
it was, because a measurement stopped half way is worth less than the `<DIR>`
it would overwrite. `..` measures the directory it leads to, which inside an
archive at its root is the directory holding the archive. A measurement belongs
to the listing it was taken over: any re-read — `Ctrl+R`, an operation that
finishes, the watch of P12 — forgets it, which falls out of the listing being
read afresh rather than being a rule of its own.

**P13.5 let the size sort see what had been measured.** Sorting by size compared
two directories by `Size`, which is `None` for both, so they fell back on their
names; it now compares them by what was measured, and a directory nobody
measured counts as nothing and therefore keeps that name order. Files still
compare by their own length, and the two never meet in one comparison, because
the group rank of P2 has always put every directory above every file. A
measurement arriving into a panel that sorts by size moves the row where it
belongs straight away, through the same `reorder` the sort keys use, which keeps
the cursor on the entry it was on — so the row the user just measured travels
under their cursor rather than away from it.

**P13.6 taught `Operations` to wipe.** `Operations.wipe` clears the read-only
attribute, overwrites the whole length of a file with one pass of random bytes
in buffers of 64 KiB, reporting and asking about cancellation after each of
them, flushes, truncates the file to nothing, renames it to a random name of
the same length in the same directory and only then deletes it. The name is
drawn from lowercase hexadecimal digits, which can never spell a device name
Windows reserves; should a hundred draws all collide, the file keeps its name
rather than failing. A directory is walked depth-first and removed after what
was in it, and `wipeAll` runs a batch the way `copyAll` does. A run stopped
inside a file answers `Cancelled` with that file's path and leaves it in place,
already overwritten, because its contents are gone whatever `Esc` says. A link,
to a file or to a directory, is removed and never written through: overwriting
it would destroy what it points at, which is a worse outcome than P13.8
arriving a few tasks late, so the rule that walks stop at a link holds for
wiping from the start. Nothing is bound to a key yet — that is P13.7. The tests
watch the mock through `Fakes.WipeWitness`, which records what the file held at
each flush and every rename, so the order is proved rather than inferred.

**P13.7 put wiping on `Alt+Del`.** `DeleteMode` has a third case, `ByWiping`,
beside `ToTrash` and `Permanently`, and the confirmation asks under a title of
its own — *Wipe* — with two sentences the other deletes do not carry: what
wiping does, and that on a solid-state disk or a copy-on-write or journaling
file system the old blocks may outlive the file anyway. Those sentences are
broken by hand, because a message box is as wide as its longest line and the
single-line version, rendered, came out wider than an 80-column terminal. The
effect leaves the synchronous path the trash and the permanent delete take and
runs `Operations.wipeAll` through `RunBatch` under *Wiping*, so it shows the
progress window and `Esc` stops it; what comes back is the same
`DeleteFinished` the other deletes send, so the panel is read again and the
marks of what was not wiped — a failure, the file a stopped run was in, and the
entries it never reached — stay. Inside an archive there is no disk block to
overwrite, only an archive to rewrite, so `Alt+Del` there asks the same
question `F8` does, for good.

**P13.8 made a link say it is one, and stopped every walk at it.** A listing
entry carries `LinkTarget` — what the link points at, or nothing — read from the
entry's own `LinkTarget` rather than from `ResolveLinkTarget`, because
`MockFileSystem` throws where the real system answers `null` for an entry that
is no link. The listing and every walk ask the same
`FileSystemPath.linkTargetOf`, so there is one answer to "is this a link". A
link, to a file or to a directory, is drawn in bright cyan
whatever it points at, and the status line says `twin -> C:\data\docs` after its
name. Every recursive walk now stops at a directory link: copying makes a new
symbolic link pointing where the old one did instead of copying the tree behind
it, moving does the same and then removes the old link, packing stores it as an
empty directory, deleting removes the link alone, measuring counts it as nothing
and wiping removes it unwritten — and a directory link is never a *non-empty*
directory, so the delete confirmation does not warn about contents it will not
touch. A file link is still copied and packed by its contents, since following
it cannot spin. What .NET does on its own was measured on a real disk rather
than assumed: enumerating a directory link walks into what it points at, which
is why the walks must stop, and `Directory.Delete` with `recursive` removes a
symbolic link inside a tree without following it — though P13.10 found it
stumbling on a junction and gave deleting a walk of its own. `MockFileSystem` follows neither, so the logic tests
run on `Fakes.LinkFollowing`, a mock that walks through a link the way a real
system does and throws once a walk has gone round one; the disk tests are in
`Integration/Disk/LinkWalkTests.fs` and skip where the system refuses the user a symbolic
link. One consequence: copying a junction, or any directory link on Windows for
a user without that right, now fails for that entry where it used to copy the
tree — and since P13.9 it says why.

**P13.9 made the symbolic link.** `LinkKind` — `Symbolic`, `Hard`, `Junction` —
is a word in `Domain`, and a `LinkRequest` carries a kind, the target and the
location the link is to stand at. `Links.create` answers the refusals every kind
shares before anything is made: a location something already stands at is
`TargetExists`, and a target that is not there is `SourceNotFound`, because a
file manager has no business making a link to nothing. The target is written
absolute; a relative one is read from the directory the link will stand in, the
way `F6` reads a bare name. A symbolic link goes through
`fs.Directory.CreateSymbolicLink` or `fs.File.CreateSymbolicLink`, chosen by
what the target is, so it is made and read back on `MockFileSystem`. A hard link
and a junction have no managed API; until P13.10 puts them behind a port they
answered a refusal of their own, which P13.10 replaced. Windows' refusal was measured rather than taken from the plan: it is not
an access refusal at all but an `IOException` carrying `0x80070522`,
`ERROR_PRIVILEGE_NOT_HELD`, whose message the system writes in its own language.
`Operations.mapException` reads that code into `SymbolicLinkNotPermitted`, the
way it reads 1155 and 1223, so copying a directory link says the same thing as
making one: that Windows makes a symbolic link only for an administrator or for
a user with Developer Mode on, and where to turn it on. The sentence is broken
by hand so the failures dialog stays inside 80 columns. Run against this
machine, which has neither, `Links.create` answered exactly that.

**P13.10 made the two kinds the framework does not give.** What a kind can point
at is a fact of the kind, so `Links.refusal` answers it from the platform and the
file system before the system is asked, as a `LinkRefusal` in `Domain`: a hard
link to a directory, a hard link onto another volume and a junction to a file.
(P13.10 also refused a junction anywhere but Windows; that refusal has since
given way to a kind the platform does not offer at all — see after P13.11.) The
volume is the longest drive root holding the
path — a drive letter compared without regard to case on Windows, a mount point
on Unix — which is why the dialog of P13.11 can grey a kind out without making
anything. `MockFileSystem` names every drive `/:\` on Linux and so cannot model a
second mount; the Unix half of that check is left to the real system, whose
refusal still arrives mapped. A refused kind becomes `LinkRefused`, and its
reason is one sentence in `LinkRefusalMessage`, so the failures dialog and the
dialog to come say the same thing. What passes the refusals goes through
`NativeLinks` — two functions `Links.create` is handed — and
`Adapters/NativeLinks.fs` supplies them: `CreateHardLinkW` from `kernel32` and
`link` from `libc` for the hard link, and for the junction a directory created
and then turned into a mount point with `DeviceIoControl` and
`FSCTL_SET_REPARSE_POINT`, removed again if that fails. A Windows failure becomes
the exception its error code stands for, and a Unix `errno` is an access refusal
for `EPERM` and `EACCES` and an `IOException` with the system's own words
otherwise, so both reach `mapException` as every other failure does.
`Shell.Dependencies` has the port, `MakeLink`, and `Dependencies.create` wires it
to `Links.create` over the real adapter; nothing calls it until `Alt+F6`. Each
kind was made once on the real disk in `Integration/Disk/NativeLinkTests.fs` —
the hard link on both systems, the junction on Windows, the symbolic link where
the system allows it.

Making a junction on the disk turned up a hole in P13.8. `Directory.Delete` with
`recursive`, which deleting a tree had relied on, removes a junction inside the
tree and then throws, leaving the tree behind: it asks
`DeleteVolumeMountPoint` about every mount point it meets, and a junction is one
that is no volume. What it throws depends on who runs it — an access refusal for
an ordinary user in a terminal, a plain `IOException` in another session — so
the test that records it asserts only that it throws and what it leaves. So `Operations.delete` now walks a tree itself, the way
wiping always has: a directory link is removed on its own, a directory is
emptied and then removed, and nothing is followed. The disk tests hold both
halves — what .NET does, and that deleting a tree with a junction in it now
succeeds and leaves what the junction points at.

**P13.11 put the link on `Alt+F6`.** It takes the operation targets every other
key takes — the marks, or the entry under the cursor — and opens a popup
holding the other panel's directory and, for one entry, that entry's name,
beneath a choice of the three kinds. What was typed is read the way `F6` reads
its own: for one entry it is where the link will stand — a bare name beside the
entry, a directory that already exists inside it under the entry's name — and
for several it is the directory each link lands in under its entry's name.
`Links.requests` is that reading, and it is the only one: the popup asks it
about the text as it is typed, and the update asks it again about the text that
was accepted. Each keystroke asks `Links.refusal` — through the `RefuseLink`
port, since the platform is only known to `Adapters` — about every kind, and a
kind it refuses is greyed out, with its sentence from `LinkRefusalMessage` on a
line beneath the choices; a chosen kind that the new text refuses gives way to
the symbolic link, which nothing refuses before the system is asked. With
several entries a kind is greyed out only when every one of them refuses it,
because the rest would otherwise be kept from a link they can carry; the ones
that refuse come back as failures of their own. Terminal.Gui has no way to
disable one option of an `OptionSelector`, but the options are `CheckBox`
subviews, and disabling one was measured to take it out of the arrow keys and
its hot key alike. The links are made one after another through `MakeLink`
without a progress window, since making one costs a single call; what failed is
reported entry by entry and keeps its mark, and both panels are read again with
the cursor on the new link in whichever of them it landed in. The bar shows
`F6 Link` while `Alt` is held.

**Which kinds a platform can make is now said by the adapter, not checked by
the logic.** `NativeLinks.MakeJunction` is an option: Windows supplies the
`DeviceIoControl` junction and Unix supplies nothing, so the stand-in that threw
`PlatformNotSupportedException` is gone and no code on Unix can make a junction.
`Links.available` reads the kinds off that record, `Dependencies.create` puts
them in `Shell.Dependencies.LinkKinds`, and the `Alt+F6` popup is built from
them: on Linux there is no junction row at all, where there used to be a greyed
one. `JunctionOffWindows` left `LinkRefusal` with its sentence, so greying a kind
out is now only about the place typed — a directory for a hard link, another
volume, a file for a junction. `LinkKind.Junction` is still a value every
platform can spell, because it travels through `LinkEntered` and `MakeLinks` as
data; `Links.create` given a junction with no function to make it answers
`KindUnavailable`, a branch no popup can reach. `Platform` is still handed to
`Links`, for one thing only: whether two drive roots are compared with or
without regard to case.

**P13.12 made a drive a value.** `DriveKind` in `Domain` — `Fixed`, `Removable`,
`Network`, `Optical` — is what the system's `DriveType` is read into, with a RAM
disk and anything the system calls unknown counted as fixed, so no `System.IO`
enumeration goes further up. A `Drive` in `FileSystem/Drives.fs` carries the root
it is reached by, its label (`None` when blank), its kind, its size and the free
space the user can actually write to. `Drives.list fs home` reads them through
`fs.DriveInfo` in the order of their roots and hides what cannot be used: a drive
that is not ready, one whose size throws when asked — which a mount the user may
not enter does on Linux — and a mount whose format is not storage, `squashfs`,
`tmpfs`, `devtmpfs`, `overlay`, `proc`, `sysfs`, the cgroups and their like.
That filter looks at the format alone and so runs on Windows too, where no drive
carries one of those names. What the list answers is a `DriveRow`: a
`MountedDrive`, or the `HomeDirectory` after the drives when a home was given,
left out if a drive already stands there. `Roots` and `RootChoice` are gone;
`Shell.Dependencies` carries a `ListDrives` port instead, which
`Dependencies.create` wires to the list with the home on Unix and without it on
Windows, and which is asked every time the root prompt opens. That prompt still
offers bare roots through `ChooseOption` until P13.13 draws the rows; the panel
that has lost its drive takes the first root of the same list. `MockFileSystem`
covers the drive letters, the kinds and sizes added with `AddDrive` and the
not-ready filter on Windows; since it names every drive `/:\` on Linux, the Unix
spelling of a mount, the format filter and the home row are tested through
`Fakes.MountedFileSystem`, a mock whose drive factory answers the mounts a test
lists, on both systems. `Integration/Disk/DriveListTests.fs` checks the real list
holds the root the working directory is on.

**P13.13 drew the drives.** `Alt+F1` and `Alt+F2` open *Drives*, a list with one
line per row: the root, the label, the kind spelled out, the total size and the
free one followed by `free`. `DriveLine.describe` pads each column to the widest
cell among the rows on screen rather than to a fixed width the way `TabRow`
does, because a Linux mount point can be long and a drive letter is three
characters; text columns are padded on the right, sizes on the left, and the
label column is dropped when no row has a label. The home row says `home` and
has no sizes. Sizes are `CompactSize.describe`: bytes below a kilobyte as
`512 B`, anything larger with one decimal in the largest of `KB`, `MB`, `GB`,
`TB`, `PB`, `EB` it reaches — `EB` being as far as an `int64` goes. It never
leaves integers: the size is shifted right by ten bits until it drops below
1024, the number of shifts picks the unit and what is left is the whole part;
shifting that back and subtracting gives the remainder, whose tenth is rounded
to the nearest with a half going up. The arithmetic runs on `uint64`, because
ten times the remainder of an exabyte size does not fit in an `int64`. A tenth
that rounds up to ten carries into the whole part, and a whole part that then
reaches 1024 becomes `1.0` of the next unit, so `1048575` bytes read `1.0 MB`
rather than `1024.0 KB`. `DriveDialog` is
`TabDialog`'s twin — a `ListView` over the lines, `Cancel` and `Ok`, the
answer the index under the cursor — sized to the longest line and to at most
twelve rows. `ChooseDrive` takes the rows and answers one; it replaced
`ChooseOption`, which had no other caller and is gone with
`Prompts.chooseOption`. Since .NET on Unix reports a mount's own path as its
label, `Drives.list` now reads a label that only repeats the root as none, so
a Linux line does not say its path twice. The list is still read by
`ListDrives` at the moment the key is pressed; `Fakes.MountedFileSystem` can
`Plug` a mount, and the integration test that plugs one between two presses
sees it offered by the second. The popup was rendered on this machine through
the running application: both drives with their labels and sizes, and `Esc`
closing it with both panels where they were.

**P13.14 decided where the panel lands.** The popup no longer answers a bare
path: `Shell` dispatches `DriveChosen` with the side and the row, and
`AppState.driveLanding` picks the directory. If the other panel stands on the
chosen drive, the panel goes to the other panel's directory — also when this
panel stands on that drive too, so `Alt+F1` on the disk both panels share brings
them into one place, and the root stays the business of `Ctrl+\`. Any other
drive gives its root, which covers the drive only this panel stands on. The home
row gives the home directory. A panel inside an archive lends the directory that
holds the archive, since the other panel is on disk and a drive is chosen to
stand on disk. Which drive a panel stands on is `Drives.holding`: the longest
root among the listed drives that the path equals or lies under at a separator,
so a panel in `/media/user/stick/photos` stands on the stick and not on `/`, and
`/media/user/sticker` does not stand on the stick. Roots are compared without
regard to case, because update knows no platform and a drive letter must match
whatever case a path was typed in; on Unix that could only mislead with two
mounts whose paths differ in case alone. `Links` still keeps its own
platform-aware volume check. A drive that vanished between the listing and the
choice needs nothing of its own: loading its root — or the other panel's
directory, which went with it — fails, the failure is reported as the listing
error it is, and the panel stays where it was. `PathOpened` stays, for the start
of the application. The cases are tests over `update` in
`State/DriveLandingTests.fs`, on `Fakes.MountedFileSystem` with a second mount.

**P13.15 let the popup hand a drive over.** What `ChooseDrive` answers is now a
`DriveChoice`: `StandOn` a row, or `HandOver` one. `Alt+R`, the key that hands
the entry under the cursor to the system in the panel, answers the second inside
*Drives*: `DriveDialog.DriveView` remembers that it was pressed, sets the result
to the accepting button and stops, so `choice` reads the row under the cursor
either way and `Esc` still reads as nothing. `Shift+Enter`, its twin in the
panel, is left unbound here, since it arrives as a bare `Enter` and would stand
the panel on the drive instead; a test pins that it hands nothing over. `Shell`
dispatches `DriveChosen` for the first answer and `DriveHandedOver` for the
second, and the update turns that into the `RunProgram` of P10.2 — the root as
the target and as the working directory, no program, as the user — while both
panels stay where they were. Putting it through `LaunchProcess` turned up a hole
on Unix: a directory carries the execute bit, so a directory with no program
chosen was being started as if it were a program, which fails, rather than being
given to `xdg-open`. `LaunchProcess.describe` now sends a directory to the
desktop's opener before it looks at the mode, which also mends `Alt+R` on a
directory in the panel; `describe` is public so a test can read the command it
builds without starting anything. On this machine the running application,
with `Launch` replaced by a recorder, answered `Alt+F1`, `↓`, `Alt+R` with a
request for `D:\` and closed the popup over two unmoved panels. The popup does
not say that `Alt+R` exists; the user guide of P13.16 will.

**P13.16 wrote the phase down where the rest of the design is.**
`Architecture/LINKS.md` is new: the three kinds and what each can point at,
which system makes which and through which call, why only the symbolic link is
testable without a disk, why every recursive walk stops at a directory link and
why deleting walks a tree itself, and the `Alt+F6` popup.
[File operations](../Architecture/FILE-OPERATIONS.md) gained *Wiping* — the one
pass and why not three, the renamed name, the order proved on the mock, what the
confirmation says it cannot promise, and what a stopped run leaves — and
*Measuring*, the walk behind `F3` and the lower bound it can answer.
[UI and input](../Architecture/UI-AND-INPUT.md) gained the size column a measured
directory fills and the size sort that sees it, the colour of a link, the drive
popup with its columns, its sizes, its two answers and where a panel lands, and
a table of the three new keys. [Cross-platform](../Architecture/CROSS-PLATFORM.md)
gained a *disk* kind of evidence, the rows of the drive popup, a section for
links, wiping and measuring with the interop confined to `NativeLinks`, the Unix
mount filter with its whole list of formats, and two differences meant to be
there: the junction on Windows alone and the symbolic link Windows grants only
some users. The [user guide](../../docs/user-guide/README.md) names all five commands — *Drives*
with `Enter`, `Alt+R` and `Esc`, `Ctrl+4`, `F3` on a directory, `Alt+Del` and
`Alt+F6` — along with the link colour and the platform differences. The overview
lists the new topic and the phase's modules in the code map. The table below
and *Next up* call the phase finished.
