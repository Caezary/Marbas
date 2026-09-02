# Cross-platform checklist

P8.2. What has to hold on Windows and on Linux, how to check it, and what is
known to differ. Run it before a release and after anything that touches
`Adapters/`, `Roots`, `Settings` or a key binding.

Three kinds of evidence appear below:

- **suite** — an automated test asserts it; `dotnet test` on both platforms is
  the whole check, and `.github/workflows/ci.yml` runs both legs on every push.
- **WSL** — verified by running the built application under WSL (Ubuntu 24.04,
  .NET 10.0.110) on 2026-08-18, driving it through a pseudo-terminal and
  reading the screen back.
- **manual** — needs a person at a real terminal. These are the items where the
  answer is "does it *look* right", which nothing here can assert.

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
| `Alt+F1` / `Alt+F2` open the root chooser | lists ready drives, `C:\`, `D:\`… | lists `/` and the home directory | suite, WSL |
| Choosing a root opens it in that panel | same | same | suite, WSL |
| Cancelling changes nothing | same | same | suite |
| A root that cannot be read is reported, panel stays put | same | same | suite |
| A removable drive that is not ready is left out | yes | n/a | suite (Windows-only test) |

The Unix list is `RootAndHome`: the file-system root plus `$HOME`, which is what
P3.5 asked for. Windows gets `MountedDrives`. `Dependencies.create` picks
between them; everything below it takes the choice as a value, so both branches
are tested on both platforms. Three tests carry `[<WindowsFact>]` and skip
elsewhere, because drive letters exist nowhere else.

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
| What the trash does with them | recycle bin | `~/.local/share/Trash` | **manual** |
| A deleted entry can be put back by the desktop's own tool | Explorer's *Restore* | the file manager's *Restore* | **manual** |
| A trash that refuses is reported entry by entry, marks kept | same | same | suite |

`Shell` asks `deps.SendToTrash`, and `Dependencies.create` decides what that is:
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
| An archive made by another tool unpacks here with its tree and its times | 7-Zip, Explorer | `zip`, `tar` | **manual** |
| The name is deduced from the targets, the root from the drive | `diskCcontents.zip` | `rootcontents.zip` | suite (one test per platform) |
| Entry paths inside the archive are relative and separated by `/` | `docs\a.txt` is stored as `docs/a.txt` | already `/` | suite |
| A backslash inside a Linux file name stays in the entry name | n/a | it does not: `SharpZipLib`'s `ZipEntry` cleans the name, so `a\b.txt` is written as `a/b.txt` and the archive gains a level the disk never had | **manual** (seen while writing P9.7, never run on Linux) |
| A backslash in an entry another tool wrote is read as part of the name | same | same: `ArchiveListing` splits on `/` alone | suite |
| The zip opens in the system's own tool | Explorer, *Extract All* | `unzip`, the desktop's archive manager | **manual** |
| The tar.gz opens in the system's own tool | `tar -xzf` | `tar -xzf` | **manual** |
| A password-protected zip asks for the password there | Explorer cannot open AES zips at all; 7-Zip can | `7z x` | **manual** |
| Modification times survive the round trip | same | same | **manual** |

Everything up to the bytes is tested on `MockFileSystem`, both writers being
stream APIs, and one test reads a zip back with `System.IO.Compression` after
`SharpZipLib` wrote it — so the two halves of the suite already disagree if the
output is not a real zip. What no test can answer is whether the other side of
the platform fence likes it, which is why the round trips are manual. The one to
try first is the AES zip: Windows Explorer has never supported AES-encrypted
entries and will report the archive as empty or damaged rather than asking for a
password, which is a property of Explorer and not of the archive.

## Permissions

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| Deleting a read-only file reports access denied, file survives | same | same | suite |
| A file held open by another process is reported, not thrown | same | same | suite |
| An unreadable directory leaves the panel where it was | same | same | suite |
| A root-owned file refuses to be deleted and is named in the dialog | n/a | **manual** | manual |
| A directory without `+x` is reported when entered | n/a | **manual** | manual |

The suite provokes failure on the file itself — `MockFileData.Attributes` set to
`ReadOnly`, `AllowedFileShare` set to `None` — which covers the mapping. What it
cannot cover is a real Unix permission bit, so the last two rows are for a
person with a `sudo`-owned file to hand.

## Session

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| Quitting writes the session | `%APPDATA%\Marbas\settings.conf` | `~/.config/Marbas/settings.conf` | suite, WSL |
| Starting restores both panel paths, sorts and toggles | same | same | suite, WSL |
| Starting from a different working directory still restores | same | same | WSL |
| A stored path that no longer exists falls back to the working directory | same | same | suite |

`Settings.location` is what makes the Linux column true: .NET answers
`SpecialFolder.ApplicationData` with an **empty string** on a machine whose
`~/.config` does not exist yet, and the old code combined that empty string into
a *relative* path — so the session was written to `./Marbas/settings.conf`,
wherever the app happened to be started from. The fallback to `$HOME/.config` is
tested and was confirmed by running the app.

## Keyboard and terminal

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `F1`–`F10`, `Tab`, `Insert`, `Enter`, arrows | same | same | suite, WSL |
| `Ctrl+Q` quits and saves | yes | yes | suite, WSL |
| `Ctrl+R`, `Ctrl+F3`–`Ctrl+F6` | yes | **manual** | manual |
| `Alt+H` toggles hidden entries | yes | yes | suite, WSL |
| `Ctrl+H` toggles hidden entries | only where the terminal reports it as a key | only under the kitty keyboard protocol | known difference |
| The bar follows a held `Ctrl` / `Alt` | yes | **no, by design** | known difference |
| `+` / `-` open the pattern prompt | yes | **manual** | manual |
| `Shift+↑` / `Shift+↓` arrive as keys of their own and mark a run | **manual** | **manual** | suite (injected), manual |
| `Shift+PgUp` / `Shift+PgDn` and `Shift+←` / `Shift+→` mark a page | **manual** | **manual** | suite (injected), manual |
| The screen redraws when the terminal is resized | **manual** | **manual** | manual |
| The sixteen classic colours look right | **manual** | **manual** | manual |
| Directory / file / archive / program colours are told apart | **manual** | **manual** | manual |
| Bright blue file names stay readable on the blue panel | **manual** | **manual** | manual |
| The rule under the listing joins the panel border | **manual** | **manual** | manual |
| Arrows stop at the ends of a panel; only `Tab` leaves it | **manual** | **manual** | suite (as handled keys), manual |
| `←` / `→` jump by the page on screen | **manual** | **manual** | suite, manual |
| Typing opens the search bar and moves the cursor | **manual** | **manual** | suite, manual |
| The mouse selects an entry and clicks the key bar | **manual** | **manual** | manual |

`Ctrl+Q` deserves a note: it is `0x11`, which is also `XON`, so a terminal doing
software flow control could in principle eat it. Terminal.Gui puts the terminal
into raw mode, and the WSL run confirmed the key arrives and the session is
written — but `F10` is the binding to reach for if a particular terminal
disagrees.

## Viewing and editing

| Check | Windows | Linux | Evidence |
|-------|---------|-------|----------|
| `F3` opens the viewer, `F3`/`Esc` closes it | same | same | suite |
| `F4` in the viewer switches to a hex dump | same | same | suite |
| A UTF-16 file is shown as the text it is | same | same | suite |
| The viewer's arrows, page keys, `Home`/`End` and `F2` wrap | same | same | suite |
| `F4` starts the editor the extension is associated with | same | same | suite |
| `F4` falls back to the default editor, then `$EDITOR` | same | same | suite |
| `F4` falls back again when neither is set | `notepad` | `vi` | suite |
| `F4` on a directory opens its properties | same | same | suite |
| A flag written to a directory is read back | same | **manual** | suite (mock) |
| `Shift+F4` creates a file and opens it | same | same | suite |
| `F12` opens the settings popup | same | same | suite |
| The screen is whole again after the editor exits | windowed editors: yes | **manual, and see below** | manual |
| `Enter` walks into a directory and an archive | same | same | suite |
| `Enter` runs a document, an executable and an associated file | same | same | suite |
| `Ctrl+PgDn` reaches the panel as a key at all | **manual** | **manual** | **manual** |
| A started program leaves the panels un-scribbled | own console, shell verb | streams drained | **manual** |
| A started program does not hold the loop up | same | same | **manual** |
| A program that is not there is named, not thrown | `Win32Exception` 2 | `Win32Exception` 2 | **manual** |
| `Shift+Enter` reaches the panel as a key at all | **manual** | **manual** | **manual** |
| `Shift+Enter` on a directory opens the file manager | Explorer | the desktop's own | **manual** |
| The handlers of a common extension are listed | registry | XDG mime database | Windows: **manual**; Unix: suite |
| An extension the system knows nothing about lists nothing | same | same | Windows: **manual**; Unix: suite |

**The known one.** `F4` starts the editor as a child process sharing this
terminal, and Terminal.Gui's driver keeps reading input while it runs. A
windowed editor (`notepad`, VS Code) round-trips cleanly. A full-screen console
editor on Unix — `vi`, `nano` — shares the terminal with a driver that has not
let go of it, and the v2 API has no suspend/resume for a child process
(`IDriver.Suspend` is `SIGTSTP`, a different thing). The fix is to shut the
driver down around the process and re-initialise afterwards; it touches
`Adapters/EditorProcess.fs` only, and it is deliberately not done yet — it
would be dead weight on Windows, so it wants a switch of its own rather than an
unconditional detour.
Until then, `EDITOR=code -w`, a default editor named in the settings popup, or
any windowed editor is the way to use `F4` on Linux.

**`Ctrl+PgDn` and `Shift+Enter` are bindings whose delivery is not yet proved.**
The suite sends them straight to the panel's `KeyDown` handler, which proves the
binding and the transition but says nothing about whether a terminal encodes the
modifier at all — outside the kitty keyboard protocol many send a bare `PgDn`
or a bare carriage return, and Windows Terminal has its own claim on some `Ctrl`
combinations. `Shift+Enter` is the more doubtful of the two: a modified `Enter`
is exactly what the plan calls the least portable thing this phase asks for.
P10.7 is where every key this phase adds gets a second binding for that reason,
and where these rows are answered rather than listed.

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
answer for everything — `ShellExecuteEx`, which `UseShellExecute` reaches — and
it gives a console program a console of its own, so nothing it prints can reach
the panels; `runas` is the same call with the verb set, and it is the only way
a terminal application asks for administrator rights without a password prompt
on a screen it does not own. Unix has no such verb. An executable entry is
started directly with both of its streams redirected and drained, because it
would otherwise inherit the terminal the panels are drawn on, and anything else
goes to `xdg-open`, which is the desktop's own idea of what opens a file. The
first launch on either system is what the three rows above check by hand; the
adapter itself is out of the suite's reach, like every other one.

**Attributes are not the same bits everywhere.** The properties popup offers
read-only, hidden, system and archive because that is what `FileAttributes`
carries; on Linux only the read-only bit maps onto anything real (the write
permission), and .NET reports hidden for a name starting with a dot rather than
for a bit that can be set. `EntryProperties.write` folds the four flags onto the
attributes the entry already has and swallows nothing — a platform that refuses
one is reported as an operation failure — but *what* a tick does on Linux is a
manual row, since `MockFileSystem` accepts every flag the tests set.

## Decisions

**The roots a panel can be sent to are a value, not a platform check.**
`Drives.list` used to call `DriveInfo.GetDrives()` everywhere, which on Unix
meant a list of mount points — and no way at all to reach the home directory,
though P3.5 had asked for `/` and `~`. It is now `Roots.list fs choice` with
`RootChoice` being `MountedDrives` or `RootAndHome home`; `Dependencies.create`
picks one and `Shell.Dependencies` carries it, so both branches are ordinary
tested code and neither platform is a special case below the composition root.
The module was renamed from `Drives` because on Unix it does not answer
drives, and the vocabulary follows: `RootRequested`, `PromptForRoot`,
`leftRootKey`, and a dialog that says *Select root*.

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
- **Drive letters do not exist on Unix**, so three tests skip there rather than
  pretending. `MockFileSystem` emulates drives Windows-style and answers a
  duplicated `/:\` on Linux, which is a second reason not to assert on it.
- **Dialogs and message boxes keep Terminal.Gui's own theme** on both
  platforms; the main screen and the viewer wear the classic colours.
- **Only the home trash is written on Unix.** The freedesktop spec puts a trash
  on every volume (`$topdir/.Trash-$uid`) so that deleting never crosses a
  device; `Trash.send` always writes `$XDG_DATA_HOME/Trash` and lets
  `Operations.moveTo` copy the entry over when the volume differs. It ends up
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
turns them into a release.

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
