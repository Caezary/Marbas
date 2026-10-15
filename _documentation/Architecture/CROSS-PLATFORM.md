# Cross-platform checklist

P8.2. What has to hold on Windows and on Linux, how to check it, and what is
known to differ. Run it before a release and after anything that touches
`Adapters/`, `Drives`, `Links`, `Settings` or a key binding.

Six kinds of evidence appear below:

- **suite** — an automated test asserts it; `dotnet test` on both platforms is
  the whole check, and `.github/workflows/ci.yml` runs both legs on every push.
- **WSL** — verified by running the built application under WSL (Ubuntu 24.04,
  .NET 10.0.110) on 2026-08-18, driving it through a pseudo-terminal and
  reading the screen back.
- **probe** — measured by a throwaway program rather than asserted, in three
  flavours. `_probes/KeyProbe` reports what a terminal really hands to
  Terminal.Gui rather than what a binding says it should; the rows marked so
  under *Keyboard and terminal* were measured with it on 2026-09-05 in cmder,
  Windows Terminal and the Windows console, and under WSL started from cmder and
  from the console. The rows under *Watching the directories on screen* were
  measured on 2026-09-11 with a probe that builds the real `MainWindow` over a
  real file system and changes the disk behind its back. The drive popup rows were
  rendered on 2026-09-14 with the `render-tui-screen` probe, which runs the real
  application with `Launch` replaced by a recorder. The rows of phases 17 to 20
  were measured on 2026-10-02 and 2026-10-03 in the five terminals of P13.1 with
  the guided walk the key probe has had since P17.2. See
  [Development](../DEVELOPMENT.md), *Probes*.
- **disk** — an integration test in `Marbas.Tests/Integration/Disk` does it
  against the real file system of whichever machine runs the suite, so it runs on
  both legs of CI; it is reached for only where no mock can say what the system
  does.
- **desktop defaults** — read out of a desktop environment's own configuration
  rather than pressed. A window manager grabs a shortcut before any terminal sees
  it, so what it claims by default decides whether a key can reach Marbas at all;
  `gsettings list-recursively org.gnome.desktop.wm.keybindings` under WSL answers
  for GNOME and Mutter without a desktop being installed. Read on 2026-09-16,
  and again on 2026-10-03 for the keys of phases 17 to 20.
- **terminal defaults** — read out of a terminal's own key table rather than
  pressed: Windows Terminal's `defaults.json` in its package directory (1.24.11911.0)
  and gnome-terminal's `org.gnome.Terminal.Legacy.Keybindings` (3.52.0), read with
  `gsettings` under WSL. Read on 2026-10-03. It says why a key the probe found
  missing went missing, and which keys a terminal takes only in a state no probe
  run had — several tabs open, or text selected.
- **manual** — needs a person at a real terminal. These are the items where the
  answer is "does it *look* right", which nothing here can assert. What to
  prepare and what to press for each of them is
  [The manual pass](MANUAL-PASS.md). A row that was walked carries the date in
  plain type, and its cells say what was seen; a row still in bold has not
  been, and says what it waits for. The pass of 2026-10-12 ran the published
  binaries of phase 21 in cmder on Windows and in gnome-terminal 3.52.0 under
  WSLg on Linux (Ubuntu 24.04) — a real Linux terminal with no desktop under
  it, so what needs a window manager, a file manager or `xdg-open` still waits
  for one.

## Paths

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| Panels list a real directory, `..` first | `C:\…` | `/…` | suite, WSL |
| Entering and leaving directories keeps the cursor on the one left | same | same | suite |
| A panel at the root has no `..` | `C:\` | `/` | suite |
| `F6` typing a bare name renames in place | `notes.txt` | `notes.txt` | suite |
| `F6` typing a name with a separator means a directory | `docs\notes.txt` | `docs/notes.txt` | suite |
| A backslash inside a Linux file name is part of the name, not a separator | n/a | `docs\notes.txt` is one file | suite |

Nothing in `FileSystem`, `State` or `Ui` builds a path by hand: every join,
split and parent goes through `fs.Path`, so the separator follows the platform
without a single conditional. The tests say so too — `Marbas.Tests.Root.at`
turns `"start/docs"` into `C:\start\docs` or `/start/docs`, which is why one
suite covers both.

## Roots and drives

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `Alt+F1` / `Alt+F2` open *Drives* | lists ready drives, `C:\`, `D:\`… | lists the storage mounts, then the home directory | suite; probe on Windows; manual 2026-10-12 on Linux |
| Each row shows root, label, kind, size and free space in aligned columns | yes | yes, with no label column for mounts | suite; probe on Windows; manual 2026-10-12 on Linux |
| A drive plugged in while Marbas runs is listed on the next opening | same | same | suite |
| Choosing a drive the other panel stands on lands in that panel's directory | same | same | suite |
| Choosing any other drive lands in its root | same | same | suite, WSL |
| Cancelling changes nothing | same | same | suite, probe on Windows |
| A drive that cannot be read is reported, panel stays put | same | same | suite |
| `Alt+R` in the popup opens the drive in the system file manager, panels unmoved | Explorer | `xdg-open`; the key arrives and the start is asked for, and where no `xdg-open` is installed it is reported as such since P22.9, with the package to install, where the pass saw the runtime's sentence | suite (injected), probe on Windows; manual 2026-10-12 on Windows; the missing opener: suite, and the published binary in tmux under WSL on 2026-10-15; **manual** on Linux for the window that opens, waiting for a desktop |
| A drive that is not ready, or whose size cannot be read, is left out | yes | yes | suite |
| A mount that is not storage (`squashfs`, `tmpfs`, `proc`…) is left out | n/a | yes | suite |
| `Ctrl+4` / `Ctrl+\` send the panel to the root of its volume | yes | yes | suite, probe |

Both lists are `Drives.list fs home`: the ready drives that are storage, and a
row for `$HOME` after them when a home is given, which is what P3.5 asked for.
`Dependencies.create` gives one on Unix and none on Windows; everything below it
takes the home as a value, so both branches are tested on both platforms. The
mount points are spelled through `Fakes.MountedFileSystem`, a fake drive
factory, because `MockFileSystem` names every drive `/:\` on Linux; the tests
over drive letters carry `[<WindowsFact>]`.

**On Unix most mounts are not drives, so the list filters by format.**
`DriveInfo.GetDrives()` on Linux answers every entry of the mount table: the
root and a home partition, and beside them `proc`, `sysfs`, the cgroups,
`devpts`, `tmpfs` for `/run` and `/dev/shm`, an `overlay` per container and a
`squashfs` loop per snap — easily a dozen rows burying the two a user can store
anything on. `Drives.list` drops a mount whose `DriveFormat` is one of the
formats that hold no files of the user's (`autofs`, `binfmt_misc`, `bpf`,
`cgroup`, `cgroup2`, `configfs`, `debugfs`, `devpts`, `devtmpfs`, `efivarfs`,
`fuse.gvfsd-fuse`, `fuse.portal`, `fusectl`, `hugetlbfs`, `mqueue`, `nsfs`,
`overlay`, `proc`, `pstore`, `ramfs`, `rpc_pipefs`, `securityfs`, `selinuxfs`,
`squashfs`, `sysfs`, `tmpfs`, `tracefs`), a drive that is not ready — an empty
card reader — and one whose size throws when asked, which is what a mount the
user may not enter does. The format list runs on Windows too, where no drive
carries one of those names, so it needs no platform. A format not on the list
shows up as a drive; the list is a denial list on purpose, because a real file
system Marbas has not heard of is better listed than lost. The system's
`DriveType` is read into four kinds, with a RAM disk and an unknown type counted
as fixed.

## Links, wiping and measuring

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| A symbolic link or junction is drawn in its own colour, `-> target` on the status line | same | same | suite |
| Copying, moving, packing, deleting, measuring and wiping stop at a directory link | same | same | suite, disk |
| `Alt+F6` makes a symbolic link | only for an administrator or with Developer Mode on; otherwise refused with a sentence saying which | yes | suite, disk |
| `Alt+F6` makes a hard link to a file on the same volume | `CreateHardLinkW` | `link` | disk |
| `Alt+F6` makes a junction to a directory | `CreateFileW` + `DeviceIoControl` | **not offered** | disk |
| A kind the typed target cannot carry is greyed out with its reason | same | same | suite |
| `F3` on a directory measures it; `Esc` leaves `<DIR>` | same | same | suite |
| `Alt+Del` wipes behind the progress window, `Esc` names the file it was in | same | same | suite |
| The old blocks of a wiped file are unreadable afterwards | **not promised** on SSD, copy-on-write or journaling file systems | the same | stated in the confirmation |

**Interop is confined to one adapter.** The hard link
and the junction have no managed API, so `Adapters/NativeLinks.fs` holds the
`DllImport`s: `kernel32` on Windows (`CreateHardLinkW`, `CreateFileW`,
`DeviceIoControl` with `FSCTL_SET_REPARSE_POINT`) and `libc` on Unix (`link`).
`NativeLinks.forPlatform` picks the record for the platform and leaves the
junction out on Unix, so no import of the other system is ever bound — the same
guarantee `ModifierWatch` relies on for `user32`. The *disk* rows are
`Integration/Disk/NativeLinkTests.fs` and `LinkWalkTests.fs`, which run on both
CI legs and skip the symbolic-link half where the user may not make one. The
whole story is in [Links](LINKS.md).

## Finding files

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `Alt+F7` opens *Find* | yes | yes in a terminal; **no** under a GNOME desktop, which takes the key first | suite; probe; desktop defaults |
| `Alt+Shift+F` opens *Find* wherever `Alt+F7` does not | yes | yes | suite; probe |
| `PATH` is split at the platform's own separator | `;`, quotes round an entry trimmed | `:` | suite |
| A directory `PATH` names twice, in any spelling, is searched once; one that is not there is left out | same | same | suite |
| *From the root of the volume* does not cross into another listed drive below it | a drive letter holds nothing else | `/` does not enter `/media/user/stick` | suite, over `Fakes.MountedFileSystem` |
| *All drives* walks every listed drive and not the home row | the ready drives | the storage mounts; the pseudo file systems are never listed, so never started from | suite |
| *From the root of the volume* stays out of `/proc`, `/sys` and the other pseudo file systems | n/a | **no** — they are not listed roots, so nothing bounds them | code reading |
| A directory link is followed only when asked, and a link to its own ancestor ends the walk | same | same | suite, over `Fakes.LinkFollowing` |
| A UTF-16 file is searched as text | yes | yes | suite |

**The `PATH` separator is the one platform difference the search reads, and it
is read where the platform is known.** `HostEnvironment` carries `PATH` as the
raw string and `Dependencies.create` picks the separator; `FindGround`
`.pathDirectories` takes both as arguments, so the Windows and the Unix split are
tested on either system. Whether one path lies under another is the second
difference, and since P15.2 it is one rule: `FileSystemPath.strictlyUnder`,
`atOrUnder` and `sameEntry` take a `Platform` and fold case on Windows only,
`FileSystemPath.platformOf` reads that platform off the `IFileSystem` the module
already holds, and the rule is covered by `[<Theory>]` rows for both platforms
whichever system the suite runs on. Drive letters, mount points, search
boundaries, the archive's own refusal to pack itself and the walk's record of
where it has been all go through it.

**The volume is bounded by the other drives, not by asking the system.**
`FromVolumeRoot` starts at the root `Drives.holding` names and refuses to enter
any other root `Drives.list` gives, which is the plainest statement of what a
volume is to Marbas: on Windows no drive lies inside another and the rule never
fires, on Unix it is what keeps a search of `/` off a stick, a second disk and a
network share mounted below it. The price of reading the boundary off the list
is what the list leaves out: a mount the P13.12 filter drops as not being
storage — `/proc`, `/sys`, `/run` — is no listed root, so a search of `/` walks
into it like a directory. That is a defect, recorded in
[Rough edges](../ROUGH-EDGES.md); the fix is to hand the walk the
filtered mounts as boundaries too.

## Hidden entries

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| A dotfile counts as hidden | yes | yes | suite, WSL |
| An entry the OS marks hidden or system counts as hidden | yes | yes (dotfiles carry the flag) | suite |
| `Alt+H` reveals them and the title says `hidden` | same | same | suite, WSL |
| `..` is never concealed | same | same | suite |

`EntryVisibility.classify` asks about both conventions on both platforms rather
than switching on the OS, which is why a `.gitignore` copied onto a Windows
drive still reads as hidden there.

## Deleting

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `F8` / `Del` ask before sending the targets to the trash | same | same | suite |
| `Shift+F8` / `Shift+Del` ask before deleting for good | same | same | suite |
| What the trash does with them | recycle bin, each entry with the directory it came from | `~/.local/share/Trash`: the entry under `files`, a `.trashinfo` naming its path under `info` | manual 2026-10-12 |
| A deleted entry can be put back by the desktop's own tool | Explorer's *Restore* | `gio trash --restore`; a file manager's *Restore* was not tried | manual 2026-10-12; **manual** for a file manager on Linux, waiting for a desktop |
| A trash that refuses is reported entry by entry, marks kept | same | same | suite |

`Shell` asks `deps.Processes.SendToTrash`, and `Dependencies.create` decides what that is:
`RecycleBin.send` on Windows, which is
`Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile` /
`DeleteDirectory` with `RecycleOption.SendToRecycleBin` — the VB
runtime ships with .NET, so nothing is added to the package list — and
`Trash.send` elsewhere, which is the freedesktop home trash written through
`IFileSystem` and therefore tested on `MockFileSystem` on both legs of CI. A
permanent delete is the same `Operations.delete` as before on either platform.

## Archives

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `Shift+F1` packs the targets into an archive in the other panel | same | same | suite |
| `Shift+F2` empties the archives under the cursor or marked into the other panel | same | same | suite |
| An archive made by another tool unpacks here with its tree and its times | `tar` and 7-Zip's 7z: the tree, and the files to the second; 7-Zip's zip: the same since P22.4, to the fraction of a second its NTFS field holds, where the pass saw the files an hour late; a zip Explorer made: an hour late, since it holds nothing but a local time Explorer moved; a directory at the time its entry carries since P22.3, where the pass saw it dated at the unpacking | `tar`: the tree, and the files to the second; the zips of `zip` and of 7-Zip: to the second and to the fraction since P22.4; a directory the same | manual 2026-10-12 for `tar` and the 7z; the zips: suite, on a zip each of the two tools made, and a probe on a real disk on 2026-10-14; the directories: suite, disk test; **manual** to walk again, on Linux for the first time with the zips of `zip` and 7-Zip |
| The name is deduced from the targets, the root from the drive | `diskCcontents.zip` | `rootcontents.zip` | suite (one test per platform) |
| Entry paths inside the archive are relative and separated by `/` | `docs\a.txt` is stored as `docs/a.txt` | already `/` | suite |
| A backslash inside a Linux file name stays in the entry name | n/a | in a zip and a tar.gz it does since P22.5 — packed, added with `F5` and typed at `F7` — where the pass saw `a\b.txt` written as `a/b.txt`; in a 7z it does not, since the library's writer makes the level itself | manual 2026-10-12 for the defect; since P22.5: suite, on the Linux leg alone, and a probe on a real disk on 2026-10-14 against `unzip` 6.00 and 7-Zip 23.01, which both list and extract one file; **manual** to walk again |
| A zip says where it was made | nothing of it, as before: the bytes of a zip packed and added to are those P22.4 wrote | made on Unix, every file `rw-r--r--` and every directory `rwxr-xr-x` | suite (`ZipEntriesTests`, either platform on either leg); a probe on a real disk on 2026-10-14, the two zips compared byte for byte on Windows and read by `zipinfo` on Linux |
| A file whose name ends in a backslash goes into a zip | n/a | into a new one, with its content; a zip that exists refuses it and keeps what it held, where it used to be left unreadable | suite, on the Linux leg alone; a probe on a real disk on 2026-10-14, `unzip -t` after each |
| A backslash in an entry another tool wrote is read as part of the name | listed as one entry; unpacked as a level, since a backslash separates on the disk it lands on | same: `ArchiveListing` splits on `/` alone, and it is unpacked as one file | suite; a probe on a real disk on 2026-10-14 for the unpacking |
| The zip opens in the system's own tool | Explorer, *Extract All* | `unzip`; a desktop's archive manager was not tried | manual 2026-10-12; **manual** for an archive manager on Linux, waiting for a desktop |
| The tar.gz opens in the system's own tool | `tar -xzf` | `tar -xzf` | manual 2026-10-12 |
| A password-protected zip asks for the password there | Explorer cannot open AES zips at all; 7-Zip can | `7z x` | manual 2026-10-12 |
| Modification times survive the round trip | a file: to the second through `tar`, and through 7-Zip and `unzip` since P22.4, which writes the moment beside the local time, where the pass saw 7-Zip an hour early; through Explorer an hour early still, since it reads the local time alone; a directory: its own time since P22.3, where the pass saw the moment of packing | a file: to the second through `tar` and `unzip`, and through 7-Zip since P22.4, where the pass saw it an hour early; a directory: the same | manual 2026-10-12; the zip since P22.4: suite, and a probe on a real disk on 2026-10-14 against 7-Zip 25.01, `unzip` 6.00, `tar` and Explorer on Windows and `unzip` 6.00 and 7-Zip 23.01 on Linux; **manual** to walk again |

Everything up to the bytes is tested on `MockFileSystem`, both writers being
stream APIs, and one test reads a zip back with `System.IO.Compression` after
`SharpZipLib` wrote it — so the two halves of the suite already disagree if the
output is not a real zip. What no test can answer is whether the other side of
the platform fence likes it, which is why the round trips are manual. The one to
try first is the AES zip: Windows Explorer has never supported AES-encrypted
entries and will report the archive as empty or damaged rather than asking for a
password, which is a property of Explorer and not of the archive.

**The round trips were run on 2026-10-12, and the times are where they
differ.** Every tree of the fixture is dated `2001-02-03 04:05:06`, a winter
date, and the pass ran in summer time, which is what showed it. A tar keeps a
moment and both directions were exact for the files. A zip keeps a local time
in a two-second field unless a tool adds the exact moment in an extra field:
Marbas added none then and read none. So the files it packed came out of Explorer
and of 7-Zip at `03:05:06` — both turn the local time back with today's offset
— and out of `unzip` at `04:05`; and the zip 7-Zip made, whose local field
7-Zip itself wrote as `05:05:06` beside an NTFS field holding the moment, was
unpacked here at `05:05:06`. That is in [Rough edges](../ROUGH-EDGES.md). The
directories were a second matter: the entries Marbas wrote for them carried
the moment of packing, and the times of the entries another tool wrote were
not put on the directories unpacked. P22.3 closed both — a directory is packed
with its own time and unpacked with the one its entry carries, as
[Archives](ARCHIVES.md) describes — and the rows wait to be walked again. On Linux the fixture held `tar`'s archive
alone, since `zip` and 7-Zip were installed after it was made.

**P22.4 closed the hour as far as a tool lets it be closed.** A spike tried
both extra fields on each tool first: the extended timestamp is honoured by
7-Zip, `unzip`, `tar` and SharpCompress, the NTFS field by 7-Zip alone, and
Explorer honours neither and writes neither. Marbas now writes the timestamp
beside the local time and reads both fields from the central directory
itself. On 2026-10-14 a probe ran `Archive.write`, `Archive.addEntries` and
`Archive.extract` on a real disk, on both systems, over the tree of the
fixture — not the published binary by hand, which is P22.18's to do. A zip
packed here came out of 7-Zip, `unzip` and `tar` at `04:05:06`, the
directories with the files, and so did everything in it after a file was
added to it. Out of Explorer the files still came at `03:05:06` and the top
directory dated today. The zips 7-Zip made on either system were unpacked
here at `04:05:06.500`, the half second the tree was given included, and
those of `zip` and of `tar` at `04:05:06`. The zip Explorer made was unpacked
at `05:05:08`: its local time is the only one it holds, written with the
offset of the day it was packed and rounded up. What is left is in
[Rough edges](../ROUGH-EDGES.md).

**P22.5 keeps a backslash in a name, in a zip.** The name was never changed
by the entry: `ZipOutputStream` put it through a transform as it wrote, which
[Archives](ARCHIVES.md) describes, and Marbas now hands it none. That alone
does not satisfy `unzip`, which takes a backslash for a separator in any name
that holds no `/` unless the entry says it was made on Unix, warns, and ends
with a failure — as it did on a file `F5` had added, whose name the update
had always kept. So a zip packed on Linux is marked as one made there, with a
mode beside the mark, as `zip` and 7-Zip mark theirs. On 2026-10-14 the probe
packed `a\b.txt` and a directory `dir\below` on ext4, added a file and a
directory so named, and both `unzip` 6.00 and 7-Zip 23.01 listed and
extracted each as the one entry it is; Marbas unpacked its own zip, `zip`'s,
7-Zip's and 7-Zip's 7z the same. The build before it gave `a/b.txt` and an
`unzip` that ended with a warning. On Windows the same zips unpack to a level
with `tar` and with Marbas, to `a_b.txt` with `unzip` and to a stand-in
character with 7-Zip: there the name cannot exist. What a 7z, a name ending
in a backslash and the fixed mode leave open is in
[Rough edges](../ROUGH-EDGES.md).

## Permissions

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| Deleting a read-only file reports access denied, file survives | same | same | suite |
| A file held open by another process is reported, not thrown | same | same | suite |
| An unreadable directory leaves the panel where it was | same | same | suite |
| A root-owned file refuses to be deleted and is named in the dialog | n/a | yes, by `F8` and by `Shift+F8`: *Operation failed*, `Access denied to '…/file.txt'`, and the file stays | manual 2026-10-12 |
| A directory without `+x` is reported when entered | n/a | yes: *Cannot open directory*, `Access denied to '…/no-enter'`, and the panel stays | manual 2026-10-12 |

The suite provokes failure on the file itself — `MockFileData.Attributes` set to
`ReadOnly`, `AllowedFileShare` set to `None` — which covers the mapping. What it
cannot cover is a real Unix permission bit, so the last two rows are for a
person with a `sudo`-owned file to hand. The one bit the pass found unread is
the executable one, which is a row of *Viewing and editing*.

## Session

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| Quitting writes the session | `%APPDATA%\Marbas\settings.conf` | `~/.config/Marbas/settings.conf` | suite, WSL |
| Starting restores both panel paths, sorts and toggles | same | same | suite, WSL |
| Starting from a different working directory still restores | same | same | WSL |
| A stored path that no longer exists falls back to the working directory | same | same | suite |
| Starting restores every tab, and opens on the one it was left on | same | same | suite |
| Each tab comes back with both cursors where they stood | same | same | suite |
| A session written before tabs existed still opens, as one tab | same | same | suite |
| A session writes who owns its working directory when it starts | `%TEMP%\Marbas\<session>\owner` | `$TMPDIR/Marbas/<session>/owner`, else `/tmp/…` | suite, probe, WSL |
| The directory of a session closed with the console window's button is gone after the next start | removed | removed, for a terminal window closed by its button and for a killed session | probe, WSL; manual 2026-10-12 on Linux |
| The directory of a session still running is kept by one started beside it | kept | kept | probe, WSL |
| A directory whose owner's id belongs to a process started at another moment is removed | removed | same | suite, probe |
| A directory nobody claimed is removed a day after its last write, and kept before | same | same | suite, probe, WSL |
| A process's start read by another process | the same to the tick | up to 2.8 ms apart; two seconds allowed | probe, WSL |

`Settings.location` is what makes the Linux column true: .NET answers
`SpecialFolder.ApplicationData` with an **empty string** on a machine whose
`~/.config` does not exist yet, and the old code combined that empty string into
a *relative* path — so the session was written to `./Marbas/settings.conf`,
wherever the app happened to be started from. The fallback to `$HOME/.config` is
tested and was confirmed by running the app.

The rows on the working directory were run on 2026-10-10 with the built
application and the temporary directory pointed at a scratch one (`TEMP` and
`TMP` on Windows, `TMPDIR` under WSL). On Windows each session ran in a console
window of its own (`conhost.exe Marbas.exe`) and was ended by posting `WM_CLOSE`
to that window, which is what its close button does: the first session's
directory stayed, with its `owner`, and was gone when looked for six seconds
after the second session started, together with a directory planted with the id of a running process
and another start, and one planted unclaimed and dated two days back; an
unclaimed one just made and a file beside the directories stayed, and so did
the second session's directory when a third started beside it. Under WSL three
sessions ran in tmux: the first was kept by the second while it ran — the row
that an equality on the start would have failed — and removed by the third
after `kill -9`. Closing a window by its button has no counterpart in tmux; a
terminal emulator closed by its button sends `SIGHUP`, which ends the process
the same way. The manual pass of 2026-10-12 closed a gnome-terminal window
under WSLg with Marbas running in it: the session wrote no quit to its log,
and its directory was gone after the next start.

## Keyboard and terminal

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `F1`–`F10`, `Tab`, `Insert`, `Enter`, arrows | same | same | suite, WSL |
| `Ctrl+Q` quits and saves | yes | yes | suite, WSL |
| `Ctrl+R`, `Ctrl+F3`–`Ctrl+F6` | yes | yes | manual 2026-10-12 on Linux |
| `Alt+H` toggles hidden entries | yes | yes | suite, WSL |
| `Ctrl+H` toggles hidden entries | only where the terminal reports it as a key | only under the kitty keyboard protocol | known difference |
| The bar follows a held `Ctrl` / `Alt` | yes | **no, by design** | known difference |
| `+` / `-` open the pattern prompt | yes | yes | manual 2026-10-12 on Linux |
| `Shift+↑` / `Shift+↓` arrive as keys of their own and mark a run | yes | yes | suite (injected), manual 2026-10-12 |
| `Shift+PgUp` / `Shift+PgDn` and `Shift+←` / `Shift+→` mark a page | yes | yes | suite (injected), manual 2026-10-12 |
| The screen redraws when the terminal is resized | yes | yes | manual 2026-10-12 |
| The sixteen classic colours look right | yes | yes | manual 2026-10-12 |
| Directory / file / archive / program colours are told apart | yes | yes; a program is a file with its executable bit set, read by the listing since P22.2 | manual 2026-10-12; a program on Linux: probe (the published binary in tmux, 2026-10-13) |
| Bright blue file names stay readable on the blue panel | yes | yes | manual 2026-10-12 |
| The rule under the listing joins the panel border | yes since P22.6: `╟────╢` on the focused panel, `├────┤` on the other, where the pass saw it stop at the border with no joint | yes, the same | manual 2026-10-12 for the defect; since P22.6: suite (the rule's columns), the `render-tui-screen` probe at 60, 72, 81 and 100 columns, resized and after `Tab`, and the published binary in tmux under WSL on 2026-10-15; **manual** to look at in cmder and gnome-terminal |
| Arrows stop at the ends of a panel; only `Tab` leaves it | yes | yes | suite (as handled keys), manual 2026-10-12 |
| `←` / `→` jump by the page on screen | yes | yes | suite, manual 2026-10-12 |
| Typing opens the search bar and moves the cursor | yes | yes | suite, manual 2026-10-12 |
| The mouse selects an entry and clicks the key bar | yes | yes | manual 2026-10-12 |
| `F11` lists the tabs | **only under cmder** — Windows Terminal and the console keep it for full screen | the terminal hosting the session decides, not the platform | probe |
| `Alt+T` lists them where `F11` is eaten | yes | yes | probe |
| `Alt+F11` opens a tab, `Alt+Shift+F11` closes it | yes | yes under WSL in cmder and in the Windows console; **no** in gnome-terminal under WSLg, where `Alt+F11` puts the window in full screen and neither key arrives | probe; manual 2026-10-12; probe 2026-10-15 in gnome-terminal |
| `Alt+Z` opens a tab, `Alt+Shift+Z` closes it | yes | yes, gnome-terminal under WSLg included | suite; probe 2026-10-15 in the five terminals; the published binary in tmux under WSL and by hand in gnome-terminal under WSLg, 2026-10-15 |
| `Shift+F9` writes the session | yes | yes | probe |
| `Alt+←` / `Alt+→` walk from tab to tab | yes | yes | probe |
| `Ctrl+T`, the other candidate for listing tabs | **no under cmder**, which opens its own console tab | yes | probe |
| The tab counter is legible in the top right corner | yes, at `[1/1]` and `[2/2]` | yes at `[1/1]`; a second tab could not be opened there | manual 2026-10-12 |
| `Alt+Del`, the key phase 13 wipes with | yes | yes | probe |
| `Alt+Shift+Del`, the stand-in it turned out not to need | yes | yes | probe |
| `Ctrl+\` reaches the application as a backslash | **no**, arrives as `Ctrl+4` | **no**, the same | probe |
| `Alt+\` reaches the application at all | **no** | **no** | probe |
| `Shift+Enter` reaches a popup that is up | **no**, arrives as `Enter` | **no**, the same | probe |
| `Alt+R` reaches a popup that is up | yes | yes | probe |
| `Alt+F7`, the key phase 14 finds with, reaches the terminal | yes | yes | probe |
| `Alt+F7` reaches the application on a desktop | n/a, no window manager claims it | **no** under GNOME, whose `begin-move` is `<Alt>F7` by default | desktop defaults; **manual** for KDE and Xfce |
| `Alt+Shift+F`, the stand-in phase 14 therefore binds | yes | yes | probe |
| `F3` reaches a full-screen window that is up | yes | yes | probe |
| `Enter` reaches a full-screen window that is up | yes, in a `Window` with no buttons; **no** in a `Dialog`, where it presses the default button | the same | probe |
| `Esc` can be taken by a full-screen window and close it on the second press | yes | yes | probe |
| `Alt+F1`, `Alt+F2` and `Alt+F6`, already bound, reach the terminal | yes | yes | probe |
| `Alt+F1`, `Alt+F2` and `Alt+F6` reach the application on a desktop | n/a | **no** under GNOME: `panel-main-menu`, `panel-run-dialog` and `cycle-group` | desktop defaults |
| `Alt+F12`, the key phase 16 opens the history with | yes | yes | probe |
| `Alt+Shift+H`, its stand-in | yes | yes | probe |
| `Ctrl+PgUp`, `Ctrl+PgDn`, `Del`, `Shift+Del` and `Alt+G` reach a popup that is up | yes | yes | probe |
| `Alt+↓` and `F4` reach a drop-down field inside a popup | yes | yes | probe |
| `Alt+F12`, `Alt+Shift+H`, `Ctrl+PgUp`, `Ctrl+PgDn`, `Alt+G`, `Alt+↓`, `F4`, `Del` and `Shift+Del`, the keys of phase 16, are claimed by a desktop | n/a | no, none of them under GNOME; its only `Page_Up` bindings carry `<Super>` and its only `Down` bindings `<Control><Alt>` | desktop defaults, read 2026-09-28; **manual** for KDE and Xfce |
| The grey `+`, `-` and `*` are told apart from the keys of the main keyboard | **no**, the same code in all three | **no**, the same in both | probe |
| `Ctrl` with the grey `+` or `-`, the keys P17.3 was planned on | **no**, nothing arrives; Windows Terminal keeps both for the font size | **no**, nothing arrives | probe, terminal defaults |
| `Ctrl` with the main `+`, `=` or `-` | **no**: `Ctrl+5` and `Ctrl+7` under cmder, a bare `+` or nothing in Windows Terminal and the console | **no**: `Ctrl+5` and `Ctrl+7` under cmder, a bare `=` or nothing in gnome-terminal, which zooms on `Ctrl+plus` and `Ctrl+minus` | probe, terminal defaults |
| `Alt` with the grey or main `+`, `-` or `=` | **no**: the terminal sends `ESC` and the character and Terminal.Gui swallows it; cmder hands `Alt+grey -` over as a bare `-`; Windows Terminal keeps `Alt+Shift+plus` for a split pane | **no**, swallowed, or a bare `+` or `-` | probe, terminal defaults |
| `Shift` with the grey `+` or `-`, the keys P17.4 was planned on | **no**, a bare `+` or `-` | **no**: a bare `+` or `-` under cmder, nothing in gnome-terminal | probe |
| `Ctrl` with the grey or main `*` | **no**: a bare `*`, nothing under cmder, and `Backspace` for the main `*` | **no**: nothing or `Backspace` under cmder, a bare `*` in gnome-terminal | probe |
| A bare `*`, grey or main | yes | yes | probe |
| `Ctrl+M` | **no**, arrives as `Enter` | **no**, the same | probe |
| `Ctrl+PgUp` reaches the panel | yes | yes with one tab; gnome-terminal keeps it for `prev-tab` once there are several | probe, terminal defaults |
| `Alt+0` … `Alt+9` | yes | yes with one tab; gnome-terminal keeps them for `switch-to-tab` once there are several | probe, terminal defaults |
| `Shift+Alt+0` … `Shift+Alt+9` | **no**: `ESC` with `)`, `!`, `@` …, which Terminal.Gui swallows; cmder opens a new console on `1` to `9` | **no**, swallowed | probe |
| `Alt+F8` | yes | yes in the terminal; **no** on a GNOME desktop, whose `begin-resize` is `<Alt>F8` | probe, desktop defaults |
| `F2` | yes | yes | probe |
| `Ctrl+Ins`, `Alt+Shift+Ins`, `Ctrl+Alt+Ins` | yes; Windows Terminal's copy on `Ctrl+Insert` lets the key through while nothing is selected | yes under WSL in cmder; **no** in gnome-terminal | probe, terminal defaults |
| `Ctrl+Shift+Ins` | yes | yes under WSL in cmder; **no** in gnome-terminal, which pastes | probe |
| `Ctrl+Shift+C` and `Ctrl+Shift+X` | **no**, they arrive as `Ctrl+C` and `Ctrl+X` | **no**: `Ctrl+C` and `Ctrl+X` under cmder; gnome-terminal keeps `Ctrl+Shift+C` for its copy and hands over `Ctrl+X` | probe, terminal defaults |
| `Ctrl+Shift+V` | **no**: `Ctrl+V` under cmder, a paste in Windows Terminal and the console | **no**: `Ctrl+V` under cmder, a paste in gnome-terminal | probe, terminal defaults |
| `F7`, `Shift+F7` and `F8` reach a full-screen window that is up | yes | yes | probe |
| A bare letter, a digit, `Enter`, `Ins`, `Del`, `F4`, `Ctrl+↑` and `Ctrl+↓` reach a list inside a modal dialog | yes | yes | probe |
| The stand-ins of phases 17 and 18: `Alt` and `Alt+Shift` with `E`, `N`, `A` and `C`; `Alt+Shift` with `X` and `V`; `Alt` with `I`, `M`, `U`, `B`, `K`, `P`, `L` and `J`; a digit after `Alt+K` | yes | yes | probe |
| The keys of phases 17 to 20 are claimed by a desktop | n/a | `Alt+F8` alone under GNOME; none of the stand-ins | desktop defaults, read 2026-10-03; **manual** for KDE and Xfce |

`Ctrl+Q` deserves a note: it is `0x11`, which is also `XON`, so a terminal doing
software flow control could in principle eat it. Terminal.Gui puts the terminal
into raw mode, and the WSL run confirmed the key arrives and the session is
written — but `F10` is the binding to reach for if a particular terminal
disagrees.

**`F11` is the one key of phase 11 an application cannot count on, and the
terminal decides, not the platform.** Five runs of `_probes/KeyProbe` on
2026-09-05 agree on everything but two keys. `F11` never reaches the
application under Windows Terminal or under the Windows console, which both
keep it for full screen; cmder passes it through. A WSL session inherits
whatever hosts it — the same Linux build answers `F11` when started from cmder
and never sees it when started from the console — so the Linux column of that
row is not a property of Linux at all. `Ctrl+T`, the obvious spare key, is the
one cmder keeps for its own new-console popup, which is why the second binding
is `Alt+T`: a letter with `Alt` is the shape every terminal in this checklist
has been seen to deliver, the same reasoning that gave P10.7 `Alt+W` and
`Alt+Shift+R`. Everything else phase 11 asks for — `Alt+F11`, `Alt+Shift+F11`,
`Shift+F9`, `Alt+←` and `Alt+→` — arrived in all five runs and needs no spare.
None of the five was gnome-terminal, and the manual pass of 2026-10-12 found
the exception there: under WSLg `Alt+F11` puts its window in full screen and
no tab opens, which leaves that terminal without a way to open one.

**The tabs got a letter, as the other keys did.** P22.7 ran the `tab-letters`
round of `_probes/KeyProbe` on 2026-10-15 in the five terminals of P16.1 —
cmder, Windows Terminal, the Windows console, WSL inside cmder and
gnome-terminal under WSLg. It asked for `Alt+F11` and `Alt+Shift+F11` and for
the five letters `KeyMap` leaves free with `Alt` and with `Alt+Shift` alike —
O, Q, S, Y and Z. In gnome-terminal neither function key reached the probe;
everywhere else both did, as in 2026-09-05. Every letter arrived in all five,
in both shapes and to the code — `Alt+Z` is `0x8000005A`, `Alt+Shift+Z`
`0x9000005A` — and `Alt+Shift+O` among them, although `ESC O` is the prefix
of a terminal's own sequences. `Alt+Z` and `Alt+Shift+Z` are bound beside the
function keys.

`F11` keeps the label on the bar all the same. It is the key the phase was
asked for, it works where the terminal lets it, and the bar has no way to know
which terminal it is drawn in; `Alt+T` is named in the user guide beside it for
the terminals that eat it.

The five keys were then pressed in Marbas itself, under Windows Terminal, on
2026-09-09: `Alt+T` opens the popup, `Alt+F11` adds a tab and the counter
follows, `Alt+←` / `Alt+→` walk between them, `Alt+Shift+F11` closes one, and
`Shift+F9` writes `%APPDATA%\Marbas\settings.conf` without quitting. The probe
says a key arrives; that pass says the application answers it.

**`Ctrl+\` never arrives as a backslash, and the five runs of P13.1 agree on
why.** The terminal does not send a key there at all: it sends the control byte
`0x1C`, which is what `Ctrl` with a backslash has meant since the teletype, and
the driver decodes that byte back into the key that also produces it — `Ctrl+4`,
`code=0x40000034`. The same thing one place over: `Ctrl+/` is `0x1F` and arrives
as `Ctrl+7`. `0x1C`–`0x1F` are the four bytes the digits `4`–`7` share with the
punctuation beside them, and nothing downstream can tell which of the two was
pressed. So the command reads `Ctrl+4` and keeps `Ctrl+\` bound beside it — the
shape `hiddenKeys` and `openWithKeys` already have — and the second binding is
there for a driver that one day resolves the byte differently, not for a
terminal seen in this checklist. `Alt+\` was measured as the way out and is
worse: it arrives as nothing at all, in every one of the five.

**`Shift+Enter` is no better inside a popup than it was in the panel.** P10.7
found it arriving as a bare `Enter` and called that worse than a dead key; on a
popup it is worse still, because a bare `Enter` there is the default button, so
a drive the user meant to reveal would be opened in the panel instead — the
silent wrong answer again. The probe asked this of a real modal `Dialog` rather
than of the panel: `Alt+R` arrives inside one in all five terminals, and it is
the only binding P13.15 takes. `Alt+Del` and `Alt+Shift+Del` arrive everywhere
too, so wiping needs no spare at all — a letter or a named key with `Alt` is
once more the one shape that travels.

**`Alt+F7` is the first key in this checklist that no terminal argues about and
a desktop takes anyway.** P14.1 measured it on 2026-09-16 in cmder, Windows
Terminal, the Windows console and a real VTE terminal — `gnome-terminal` run
under WSLg, which is where a Linux terminal's own encoding can be read without a
Linux machine. All four hand the key over as `Alt+F7`, `code=0x80110075`. The
loss happens one layer higher: GNOME's `org.gnome.desktop.wm.keybindings`
claims `<Alt>F7` for `begin-move` by default, and a window manager's grab means
the terminal is never sent the key at all. So the measurement had to be split —
what the terminal encodes, which a probe can press, and what the desktop claims,
which is read out of its configuration. WSLg is exactly the right place for the
first half and cannot answer the second at all, because it runs no window
manager.

The same reading found three keys Marbas already binds in the same list:
`<Alt>F1` is `panel-main-menu`, `<Alt>F2` is `panel-run-dialog` and `<Alt>F6` is
`cycle-group`, which are the drive choosers of P13.13 and the link key of
P13.11. On a GNOME desktop those three commands cannot be reached, and unlike
`Alt+F7` they carry no second binding. That is a defect rather than a
measurement, and it is recorded as one in [Rough edges](../ROUGH-EDGES.md).

**A full-screen window may take `Enter`; a dialog may not.** The results window
of phase 14 needs `Enter` to choose the row under the cursor, and the probe first
asked this of a `Dialog`, which answered by closing: `Enter` presses the default
button before a handler sees it, exactly as `Shift+Enter` arriving as a bare
`Enter` does to a popup. Asked again of a full-screen `Window` with no buttons —
the shape `FileViewer` has had since P6.1 — the same key is delivered to the
handler in all four terminals, `F3` with it, and `Esc` can be taken on the first
press and close the window on the second. So the shape of the window is what
decides, not the terminal: a window that wants the keys of a panel must be
built like the viewer and reach its commands with keys rather than buttons.

**Every key of phase 16 travels, and the one surprise was the probe's own.**
P16.1 ran `_probes/KeyProbe` on 2026-09-28 in the five terminals of P13.1 —
cmder, Windows Terminal, the Windows console, WSL inside cmder and
`gnome-terminal` under WSLg — and all five agree to the code: `Alt+F12` is
`0x8011007A`, `Alt+Shift+H` `0x90000048`, `Ctrl+PgUp` and `Ctrl+PgDn`
`0x40110020` and `0x40110021`, and `Del`, `Shift+Del`, `Alt+G`, `Alt+↓` and
`F4` arrive as themselves, inside a dialog and inside a drop-down field alike.
Each run had one terminal tab open, so a terminal that walks its own tabs on
`Ctrl+PgUp` once there are several is not ruled out; `←` and `→` walk the
history's tabs as well and need no binding of ours. The stand-in is therefore not needed
anywhere measured; it stays bound because a terminal or a desktop not in this
list may still take `Alt+F12`. `Esc` did not close the probe's popups, which is
the probe and not the terminals: it bound `Command.Quit` to `Ctrl+Q` alone,
where `MainWindow.configureQuitKey` binds `Esc` beside it. The probe now binds
both, as the application does. What the same run showed about `Enter` on an
open drop-down list — that it closes the popup as well as choosing the row — is
the control's doing, not the terminal's, and is taken up in
[History](HISTORY.md).

**The grey keys are not keys of their own, and `Alt` with punctuation is not a
key at all.** P17.2 measured every key phases 17 to 20 bind, in the same five
terminals, with a probe rebuilt for the question: it names one physical key at a
time and records whatever arrives — at the application, at the view that would
handle it, and as a sequence the driver swallowed or a paste — because a
checklist that waits for an exact `Key` cannot tell a grey `+` from a main one.
Nothing can. A terminal sends the character either key produces, and
Terminal.Gui 2.4.17 runs its `ansi` driver on Windows as well as on Linux; even
its `windows` driver maps the keypad's `VK_ADD` and the main `VK_OEM_PLUS` to the
same character. So the plan's first fallback, the same modifier on the main key,
was measured too, and it fails in a different way in each terminal. `Ctrl` with
`=` or `-` is a control byte under cmder, decoded as `Ctrl+5` and `Ctrl+7` — the
`Ctrl+\` story again; Windows Terminal and the console drop the `Ctrl` or keep
the key for the font size; gnome-terminal zooms. `Shift` on a grey key is
dropped everywhere, which leaves a bare `+` or `-` — the key that already opens
the pattern popup. `Ctrl+M` is byte `0x0D` and arrives as `Enter` in all five.

`Alt` with punctuation is lost one layer higher. Every terminal sends what an
`Alt` chord has always been, `ESC` followed by the character, and the probe
caught each of them as a sequence the driver swallowed: Terminal.Gui's
`EscAsAltPattern` accepts `ESC` followed by a control character, a letter, a
digit or `_`, and nothing else. That takes `Alt` with the grey or main `+`, `-`
and `=`, and it takes `Shift+Alt+0` … `Shift+Alt+9`, which reach the driver as
`Alt` with the shifted character of the layout — `)`, `!`, `@` … — in every
terminal; cmder opens a new console on `Shift+Alt+1` to `9` before that. A
Terminal.Gui that decoded those sequences would still hand over a character that
depends on the layout, so there is no stable key to wait for. The same pattern
explains a row of P13.1 that was left as a fact: `Alt+\` is `ESC \`, and it
arrived as nothing at all for exactly this reason.

What travels is what travelled before: a letter or a digit with `Alt`, with or
without `Shift`. A second round on 2026-10-03 pressed every stand-in chosen
from that shape — `Alt` and `Alt+Shift` with `E`, `N`, `A` and `C`, `Alt+Shift`
with `X` and `V`, `Alt` with `I`, `M`, `U`, `B`, `K`, `P`, `L` and `J` — and each
arrived as itself in all five, as did a bare `*`, grey or main, and a digit after
`Alt+K`. The spelling each task of phases 17 and 18 binds is listed in
[the plan of phase 17](../Plan/PHASE-17-MARKS-PANEL-AND-FAVOURITES.md); the keys
of phases 19 and 20 — `F2`, `F7`, `Shift+F7`, `F8`, and a letter, a digit,
`Enter`, `Ins`, `Del`, `F4`, `Ctrl+↑` and `Ctrl+↓` inside a dialog holding a
list — arrived everywhere and need none.

The rest is terminals and desktops keeping keys for themselves. gnome-terminal
delivers none of `Ctrl+Ins`, `Alt+Shift+Ins`, `Ctrl+Alt+Ins` and
`Ctrl+Shift+Ins`, the last of which it pastes with, and keeps `Ctrl+Shift+C` for
its copy; `Ctrl+Shift+X` arrives without its `Shift` everywhere, and
`Ctrl+Shift+V` is a paste in three terminals and a bare `Ctrl+V` in the other
two. Windows Terminal's own copy on `Ctrl+Insert` lets the key through while
nothing is selected, which is why it arrived there. Every run had a single tab:
gnome-terminal's table binds `Ctrl+PgUp` to `prev-tab` and `Alt+1` … `Alt+0` to
`switch-to-tab`, so with several tabs open those keys stay in the terminal —
`Alt+U` stands beside `Ctrl+PgUp`, and the favourites popup reaches every slot
where the digits do not. And GNOME, which already took `Alt+F7`, holds `<Alt>F8`
for `begin-resize`, so the favourites popup has `Alt+B` beside it. `Alt+D` is
left unbound on purpose: it is the natural stand-in for the drive popups,
`Alt+F1` and `Alt+F2`, which GNOME also takes and which still have none.

## Watching the directories on screen

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| A file written by another program appears, a deleted one goes | same | same | probe |
| A directory made outside the application shows up | same | same | probe |
| Two hundred files written in one burst cost one listing, not two hundred | one or two, the burst outran the window | one | probe |
| Deleting the directory a panel stands in leaves it in the parent, no dialog | same | same | probe |
| Pulling out the drive a panel stands on leaves it on a root that exists | `subst Y: /D`, panel lands on `C:\` | n/a, no drive letters | probe (Windows), suite |
| A change made while a prompt is up waits for it to close | same | same | probe |
| Only the tab in front is watched | same | same | suite |
| Entries added inside a share the watcher says nothing about | **not seen until `Ctrl+R`** | **not seen until `Ctrl+R`** | known limit |

The **probe** rows were measured on 2026-09-11 with the scenes described under
[Development](../DEVELOPMENT.md): the real `MainWindow` over a real file system in
a temporary directory, changed from outside while the loop runs, the screen read
back afterwards. On Linux the same four scenes ran under WSL (Ubuntu 24.04,
.NET 10.0.110) through a pseudo-terminal, which is also where the dialog and the
panel underneath it were seen in one picture.

**The two systems do the same thing through entirely different machinery, and
`FileSystemWatcher` hides it.** On Windows it is `ReadDirectoryChangesW`, a
directory handle and a completion buffer; on Linux it is `inotify`, a per-user
instance with one watch per directory. Both report a directory and not a tree
here — `IncludeSubdirectories` is off — and both are given the same
`NotifyFilters`, though what each one maps those onto differs: the Linux driver
translates them onto the inotify masks it has, which is why the debounce matters
more there than the filter does.

**The inotify limits are two, and Marbas is nowhere near either.** Each
`FileSystemWatcher` takes one inotify *instance*
(`fs.inotify.max_user_instances`, 128 on a default Ubuntu) and one *watch* per
directory it is given (`fs.inotify.max_user_watches`, 8192 on older kernels,
65536 or more now). One tab watches at most two paths, and the previous pair is
disposed before the next is opened, so the count is two per application and not
two per directory ever visited. An application that watched a tree recursively
would have to care; this one does not.

**A file system that reports nothing is the other reason the poll exists.** SMB
and NFS shares deliver change notifications unreliably or not at all — the
server decides, not .NET — and so do some virtual file systems. The two-second
poll of P12.4 notices a watched path that went or came back on any of them,
because it asks `Directory.Exists` rather than the watcher. What it cannot
notice is a file added *inside* such a share, since it reads no listing: that is
the one row above marked a known limit, and `Ctrl+R` is still the answer there.

## Viewing and editing

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `F3` opens the viewer, `F3`/`Esc` closes it | same | same | suite |
| `F4` in the viewer switches to a hex dump | same | same | suite |
| `F8` in the viewer reads the file in the code page chosen, popup and all | same | same | suite; probe on Windows |
| A UTF-16 file is shown as the text it is | same | same | suite |
| The viewer's arrows, page keys, `Home`/`End` and `F2` wrap | same | same | suite |
| `F4` starts the editor the extension is associated with | same | same | suite |
| `F4` falls back to the default editor, then `$EDITOR` | same | same | suite |
| `F4` falls back again when neither is set | `notepad` | `vi` | suite |
| `F4` on a directory opens its properties | same | same | suite |
| A flag written to a directory is read back | same | read-only: yes, the write bits go; hidden, system and archive are not offered since P22.8, where the pass saw a tick taken without a word and gone when the popup opened again | suite (mock, either platform on either leg); manual 2026-10-12 for the defect; **manual** to walk again |
| `Shift+F4` creates a file and opens it | same | same | suite |
| `F12` opens the settings popup | same | same | suite |
| `F4` waits for the editor | **no**, started through the shell | yes, with the terminal handed over | suite |
| A console editor gets a window of its own | yes, a console of its own | n/a | manual 2026-10-12 |
| A file saved in the editor shows in the panel | the directory watch | the directory watch | manual 2026-10-12 |
| A full-screen console editor gets every key and gives the terminal back | n/a, a console of its own | yes, the driver stopped and built again | suite (the switch); probe (tmux); manual 2026-10-12 |
| The screen is whole again after the editor exits, at the size it has then | n/a, nothing waited | yes | probe (tmux); manual 2026-10-12 |
| `Enter` walks into a directory and an archive | same | same | suite |
| `Enter` runs a document, an executable and an associated file | same | a document and an associated file: same; an executable: started from its own file, by its mode | suite; disk test (the mode on a real disk); manual 2026-10-12 on Linux; an executable: probe (the published binary in tmux, 2026-10-13) |
| `Alt+W` walks into the entry under the cursor | same | same | suite; manual 2026-10-12 on Linux, where the key arrives |
| `Ctrl+PgDn` reaches the panel as a key at all | **yes** | yes | manual (Windows Terminal; gnome-terminal on 2026-10-12) |
| A started program leaves the panels un-scribbled | a console program: own console, shell verb; a window program: started from its file, streams drained | streams drained | suite (the start described); probe (ConEmu); manual 2026-10-12 on Windows; **manual** on Linux |
| A program named by its start is resolved to its file | as written, `PATH` with `PATHEXT`, App Paths | n/a | suite (mock); probe |
| A started program does not hold the loop up | yes | yes: the session took the next key at once | manual 2026-10-12 on Windows; probe on Linux (the published binary in tmux, 2026-10-13), **manual** there still to come |
| `F2` opens the user menu, a letter and `Enter` run an entry | same | same | suite; probe on Windows |
| A user menu entry is started the way it names | shell verb; `cmd /s /c`, `cmd /s /k` | drained; `/bin/sh -c` for both shell ways | suite (the start described); manual 2026-10-12 (each way run) |
| A user menu entry runs as administrator | `runas`, `cd /d` first in the shell | refused by platform, with the reason in the dialog | suite (described); manual 2026-10-12 |
| The list file of `%f` is written before the start | same | same | suite |
| `Ins`, `F4`, `Del`, `Ctrl+↑`, `Ctrl+↓` edit the user menu popup | same | same | suite; probe on Windows; keys measured in P17.2 |
| A program that is not there is named, not thrown | `Win32Exception` 2 | `Win32Exception` 2 | manual 2026-10-12 |
| `Alt+R` asks which program opens the entry | same | same | suite |
| `Shift+Enter` reaches the panel as a key at all | **no**, arrives as `Enter` | **no**, arrives as `Enter` | manual (Windows Terminal; gnome-terminal on 2026-10-12) |
| `Alt+R` on a directory opens the file manager | Explorer | the desktop's own, through `xdg-open`; where none is installed the dialog says so and names the package that provides it, since P22.9 | manual 2026-10-12 on Windows; the missing opener: suite; **manual** on Linux, waiting for a desktop |
| `Alt+Shift+R` runs the entry elevated | same | same | suite; manual 2026-10-12 on Linux, where the key arrives and is not taken for `Alt+R` |
| `Ctrl+Alt+Enter` reaches the panel as a key at all | **no** | **no**, nothing arrives | manual (Windows Terminal; gnome-terminal on 2026-10-12) |
| An executable starts with administrator rights | `runas`, the system prompts | refused by platform, with the reason in the dialog | manual 2026-10-12 on Windows; probe on Linux (the published binary in tmux, 2026-10-13) |
| A prompt the user turns down leaves no trace | `Win32Exception` 1223, silence | n/a | manual 2026-10-12 |
| A document refuses to be run elevated | same | same | suite |
| The handlers of a common extension are listed | registry | XDG mime database | Windows: manual 2026-10-12; Unix: suite |
| An extension the system knows nothing about lists nothing | same | same | Windows: manual 2026-10-12; Unix: suite |

**The terminal handed over.** On Windows `F4` has not waited for the editor
since P20.1: it goes through the shell verb, so `notepad` opens its own window
and a console editor its own console, and the panels stay live behind both. On
Unix there is no window to hand a console editor, and since P21.5 `F4` hands it
the terminal instead: the driver is stopped, the editor runs and is waited for,
and the driver is built again inside the same `IApplication` and draws the
whole screen. How, and why it reaches past the public API, is in
[File operations](FILE-OPERATIONS.md). A probe drove the real binary in tmux on
WSL (Ubuntu 24.04) with `EDITOR=vim`: a line added and saved, the window
resized while `vim` held it, the panels back at the new size with the file's
new length, a second `F4`, and an editor that is not there named in a dialog
over whole panels. A person ran it on 2026-10-12 in gnome-terminal under WSLg,
with `EDITOR=vim`: `vim` had the whole screen and every key, the panels came
back whole with the file at its new size, and a window resized while `vim` was
up came back at the size it had then.

**`Shift+Enter` was tried and does not arrive.** Under Windows Terminal the
modifier is dropped and the panel is handed a bare carriage return, which the
list reads as `Accepting` — so the key *runs* the entry instead of asking what
should open it. That is worse than a dead key, so `Alt+R` was added beside it
and is the binding named everywhere: `openWithKeys` holds both, exactly as
`hiddenKeys` holds `Alt+H` beside the `Ctrl+H` no terminal delivers either. The
plan foresaw this — a modified `Enter` is what it calls the least portable thing
phase 10 asks for — and P10.7 is where the remaining two commands get the same
treatment. gnome-terminal drops the modifier the same way, as the manual pass
of 2026-10-12 saw: the entry was run, not asked about.

**`Ctrl+PgDn` arrives, `Ctrl+Alt+Enter` does not.** Both were tried by hand
under Windows Terminal on 2026-09-04: the walk into a directory or an archive
answers to `Ctrl+PgDn`, and nothing at all happens on `Ctrl+Alt+Enter` —
`Alt+Enter` is the terminal's own full-screen toggle, and the combination with
`Ctrl` does not reach the application either. That is why P10.7 gave both
commands a letter of their own, `Alt+W` and `Alt+Shift+R`, and left the older
names bound beside them: a letter with `Alt` is the one shape of modified key
every terminal in the checklist has been seen to deliver. gnome-terminal
answered the same on 2026-10-12: `Ctrl+PgDn` walks in, and `Ctrl+Alt+Enter`
does nothing at all.

**The shifted letter was checked against the drivers, not only against the
suite.** `Alt+Shift+R` asks for a letter *and* a shift, and a driver that
reported the uppercase rune without the shift flag would hand the panel `Alt+R`
instead — the open-with popup rather than any rights raised, the same shape of
silent wrong answer `Shift+Enter` turned out to be. Terminal.Gui's two key
converters were asked directly, by reflection, outside any terminal: the
Windows one, which reads a console input record, and the net one, which reads a
`ConsoleKeyInfo`. Both spell `Alt+Shift+R` as the uppercase `R` with the shift
flag set and read it back as the same key, and both round-trip `Alt+W`, `Alt+R`
and `Alt+H` the same way. What is left manual is one step further out: whether
the terminal hands the driver those modifiers at all. `Alt+H` and `Alt+R` have
been doing so under Windows Terminal since P7.3 and P10.5, which is the whole
reason the letters were chosen. gnome-terminal hands over both of the others
as well: on 2026-10-12 `Alt+W` walked into a directory there, and `Alt+Shift+R`
arrived as the elevated run and not as the open-with popup.

**Administrator rights are a Windows-only answer, and Unix says so rather than
pretending.** On Windows `AsAdministrator` is one field — the `runas` verb on
the same shell execute every other start goes through — and the system, not
Marbas, does the asking; a prompt the user turns down comes back as
`Win32Exception` 1223 and becomes nothing at all on the screen, because refusing
is an answer and not a failure. Unix has no equivalent a full-screen terminal
application can ask for: `sudo`, `pkexec` and their kin either want a password
on the tty the panels are drawn on or want a desktop agent Marbas cannot count
on. `LaunchProcess` therefore refuses an `AsAdministrator` request before it
reaches a process and hands back the reason, which the same message box that
reports every other launch failure puts in front of the user. That refusal is
the one part of the elevated run the suite can prove on either platform, since
the platform is an argument rather than something read at the call.

**Which programs open a file is a question with two entirely different
answers.** Windows keeps them in the registry: `HKCU\…\FileExts\.ext\UserChoice`
is what the user picked, `HKCR\.ext` its default ProgId, `OpenWithProgids` under
both roots the rest, and `OpenWithList` the per-user open-with names — every one
of them resolved through a `shell\open\command` value. Unix keeps them in files:
`mime/globs` under the XDG data directories turns the extension into a MIME
type, `mimeapps.list` under the config directories turns that into `.desktop`
identifiers, and the `.desktop` files under `<data>/applications` say what each
one is called and what it runs. The two adapters therefore share only the type
they answer with and the command-line parser at the bottom, and neither reports
a failure: a platform that knows nothing answers an empty list.

**`Microsoft.Win32.Registry` needed no package after all.** The registry types
are in the shared framework on a plain `net10.0` target — they compile and load
on Linux and throw there if called, which nothing does, because
`Dependencies.handlersFor` picks the adapter by platform. The project therefore
still builds on one framework for both systems with no Windows-flavoured target
and no extra dependency in the single-file publish.

**Starting a program is where the two systems differ most.** Windows has one
answer for almost everything — `ShellExecuteEx`, which `UseShellExecute`
reaches — and it gives a console program a console of its own, so nothing it
prints can reach the panels; `runas` is the same call with the verb set, and it
is the only way a terminal application asks for administrator rights without a
password prompt on a screen it does not own. A window program is the
exception: the verb gives it no console, an Electron program then attaches to
Marbas's own, and under ConEmu (cmder) a fresh VS Code wrote its log over the
panels, while under Windows Terminal and conhost it wrote nothing. Since P21.6
a window program named by a start is resolved to its file and started without
the verb, its streams drained, as
[File operations](FILE-OPERATIONS.md) describes; a probe under ConEmu ran a
user menu entry `code.exe <file>` through the real adapter with a fresh VS Code
and found nothing in the console but what the probe wrote itself. Unix has no such verb. An executable entry is
started directly with both of its streams redirected and drained, because it
would otherwise inherit the terminal the panels are drawn on, and anything else
goes to `xdg-open`, which is the desktop's own idea of what opens a file. The
first launch on either system is what the rows above check by hand; the
adapter itself is out of the suite's reach, like every other one.

**A missing opener is named, not quoted.** A start that cannot find its file
fails on Linux with a `Win32Exception` of code 2, *No such file or directory*,
measured under WSL with `xdg-open` not installed; the same code says the
program a request named is gone, which is `ProgramNotFound`. Since P22.9
`LaunchProcess.failure` is told the start it described as well, and a start
whose file is `xdg-open` that fails with that code is `OpenerNotInstalled`:
*Nothing on this system opens files: 'xdg-open' is not installed, and the
package xdg-utils provides it.* A program started from its own file keeps the
runtime's reason, since there the file is the program and not an opener.
Describing and starting are two steps, so the mapping is tested on a
described start and a made exception, with no process; the published binary
showed the sentence for `Enter` on a document and `Alt+R` on a drive in tmux.

**On Linux the executable bit did not reach Marbas, and the first launch by
hand is what found it.** The manual pass of 2026-10-12 pressed `Enter` on a
shell script of mode `rwxrwxr-x` and was told that `xdg-open` could not be
started; `Alt+Shift+R` on the same file said it was not a program, and the
panel drew it as a plain file. The listing and the adapter both asked
`IFileSystemInfo.UnixFileMode`, and the wrapper of System.IO.Abstractions
answers `None` for every file — measured under WSL beside the runtime's own
`FileInfo` and beside `fs.File.GetUnixFileMode`, which both gave the mode, in
22.2.0 and again in 22.3.0, where the property is still one nothing fills.
`MockFileSystem` keeps the mode a test sets, so every row marked *suite* was
true of the logic and none of them had met a real disk.

**Since P22.2 the mode is asked through `fs.File.GetUnixFileMode`.** The
listing a panel shows asks it of every file, on Unix alone, and the launch of
the one file it starts; `Integration/Disk/ExecutableBitTests.fs` holds both to
a real disk, on the Linux leg, and is skipped on Windows. The published binary
was then driven in tmux under WSL over a directory holding a script of mode
`rwxr-xr-x` and a text file: the script is drawn in the colour of a program
and the text file is not, `Enter` on the script starts it — the file it
touches appears in the panel — and `Alt+Shift+R` on it answers that a terminal
application cannot ask this system for administrator rights. A mount that
marks every file executable makes every file a program, which
[Rough edges](../ROUGH-EDGES.md) records.

**Attributes are not the same bits everywhere.** The properties popup offers
read-only, hidden, system and archive because that is what `FileAttributes`
carries; on Linux only the read-only bit maps onto anything real (the write
permission), and .NET reports hidden for a name starting with a dot rather than
for a bit that can be set. `EntryProperties.write` folds the four flags onto the
attributes the entry already has and swallows nothing — a platform that refuses
one is reported as an operation failure — but *what* a tick does on Linux is a
manual row, since `MockFileSystem` accepts every flag the tests set. Walked on
2026-10-12, it is this: read-only takes the write bits away and is ticked at
the next opening, and unticking it gives back the owner's write bit alone;
hidden, system and archive are refused by nobody and kept by nobody — .NET
drops the three on Unix, so the tick is gone when the popup opens again and
no dialog says why.

**So the popup offers what the platform keeps.** Since P22.8
`EntryProperties.flagsKeptOn` answers the flags a platform holds on to — all
four on Windows, read-only alone on Unix — and both the popup and
`EntryProperties.write` take the platform: the popup shows a box for those
flags alone and folds only them back, and the write folds only them onto the
entry, so a flag it does not keep is left as the entry had it. A dot-name's
hidden, which .NET reports on Unix, therefore survives a write untouched. The
platform comes from `FileSystemPath.platformOf` on the file system the write
holds, and `Dependencies.create` hands the popup the same answer; `writeOn`
and `create` take it as a value, so either platform's popup and write are
tested on either leg.

## Decisions

**The roots a panel can be sent to are a value, not a platform check.**
`Drives.list` used to call `DriveInfo.GetDrives()` everywhere, which on Unix
meant a list of mount points — and no way at all to reach the home directory,
though P3.5 had asked for `/` and `~`. For a while it was `Roots.list fs choice`,
with `RootChoice` being `MountedDrives` or `RootAndHome home`, answering bare
paths. P13.12 made it `Drives.list fs home` again, answering `DriveRow`s — a
`Drive` with its label, kind and sizes, or the home directory — once the mounts
that are not storage were filtered out and the mount list became worth showing
on Unix too. `Dependencies.create` still decides whether there is a home row,
so neither platform is a special case below the composition root. The key keeps
its vocabulary — `RootRequested`, `PromptForRoot`, `leftRootKey` — while the
popup, since P13.13, is titled *Drives* and shows each row with its sizes.

## Differences that are meant to be there

- **The modifier row is Windows-only.** Holding `Ctrl` or `Alt` swaps the bottom
  bar for the commands that modifier reaches. It is polled through
  `GetAsyncKeyState`, a `user32.dll` import that is never reached off Windows —
  `Dependencies.create` binds `ModifierWatch.unsupported` there, so the P/Invoke
  is never bound, which the WSL run confirms by not crashing. No Unix driver in
  Terminal.Gui 2.4.17 reports a standalone modifier outside the kitty keyboard
  protocol, so there is nothing to poll.
- **`Ctrl+H` is `0x08` on a VT terminal**, the byte backspace already owns, so it
  cannot be told apart from `Backspace` outside the kitty protocol. `Alt+H`
  carries the label on the bar for exactly this reason.
- **Drive letters do not exist on Unix**, so the tests over them skip there
  rather than pretending. `MockFileSystem` emulates drives Windows-style and answers a
  duplicated `/:\` on Linux, which is a second reason not to assert on it.
- **A junction exists on Windows alone.** The `Alt+F6` popup on Unix offers the
  symbolic and the hard link and no junction row at all, because
  `NativeLinks.forPlatform` supplies no function to make one.
- **Windows makes a symbolic link only for an administrator or with Developer
  Mode on.** Linux lets any user make one. The refusal is mapped to a sentence
  saying which of the two to switch on, rather than passed through as an access
  error.
- **Dialogs and message boxes keep Terminal.Gui's own theme** on both
  platforms; the main screen and the viewer wear the classic colours.
- **Only the home trash is written on Unix.** The freedesktop spec puts a trash
  on every volume (`$topdir/.Trash-$uid`) so that deleting never crosses a
  device; `Trash.send` always writes `$XDG_DATA_HOME/Trash` and lets
  `Operations.moveToPath` copy the entry over when the volume differs. It ends up
  in the right place either way — it is slower for a large tree on another
  disk, and the desktop's *Restore* still works, because the original path is
  recorded in the `.trashinfo` file.
- **macOS would get the freedesktop trash, not `~/.Trash`.** Nothing publishes
  a macOS build today; when something does, the Finder trash is one more
  implementation of the same port.

## Running the checks

```
dotnet test Marbas.slnx          # both legs of the table marked "suite"
dotnet run --project Marbas      # everything marked "manual"
```

Before a release, run the *manual* rows against the published binary rather than
`dotnet run` — it is a different build (self-contained, single-file,
ready-to-run), and it is what a user gets:

```
dotnet publish Marbas/Marbas.fsproj -p:PublishProfile=win-x64
dotnet publish Marbas/Marbas.fsproj -p:PublishProfile=linux-x64
```

See [PACKAGING.md](PACKAGING.md) for what those profiles decide and how a tag
turns them into a release. [The manual pass](MANUAL-PASS.md) has the steps, one
per row, and `_probes/ManualPass` builds the directory they stand in.

Under WSL, build outputs are best kept away from the Windows ones:

```
dotnet test Marbas.slnx -p:ArtifactsPath=/tmp/marbas
```

### Driving the application without a person

The **WSL** rows above were checked by running Marbas in a pseudo-terminal and
reading the screen back, which takes three tricks worth writing down:

```sh
script -qec "stty rows 40 cols 120; dotnet run --project …" /dev/null \
  < <(sleep 6; printf "\033[8;40;120t\033[40;120R\033[?1;2c"; sleep 8; …) > run.log
```

`script` gives the process a pty it will draw into. `stty` gives that pty a
size, without which nothing is drawn at all. And the `printf` answers the
queries Terminal.Gui sends on startup — window size (`\e[18t`), cursor position
(`\e[6n`) and device attributes (`\e[0c`); a real terminal replies to those, a
bare pty does not, and until they are answered the driver renders an empty
screen. Strip the escapes from the log with `sed -r "s/\x1b\[[0-9;?]*[a-zA-Z]//g"`
and the panel contents are readable text.

Keys go in as their terminal sequences: `\033[21~` is `F10`, `\021` is `Ctrl+Q`,
`\033h` is `Alt+H`, and `Alt+F1` is `\033[1;3P` — *not* `\033\033OP`, which the
driver ignores. Whether the session file appeared afterwards is the cheapest
proof that a key reached the application at all, since quitting is what writes
it.
