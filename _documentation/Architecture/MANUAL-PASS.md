# The manual pass

P21.18. How the rows of [Cross-platform](CROSS-PLATFORM.md) marked *manual* are
walked by a person: what to prepare, what to press, what should be seen, and
how the answer is written down. A row is manual when its question is how
something looks, what a real terminal or desktop hands over, or what another
program makes of a file Marbas wrote — nothing a test or a probe can answer.

Walk it against the published binaries, before a release and after anything
that touches the colours, a key binding or `Adapters/`.

## Preparing

```
dotnet publish Marbas/Marbas.fsproj -p:PublishProfile=win-x64
dotnet publish Marbas/Marbas.fsproj -p:PublishProfile=linux-x64
```

The binaries land in `Marbas/bin/publish/<runtime>/`. The Linux one is copied
to the machine it is walked on and given its executable bit.

`_probes/ManualPass` builds the directory the steps stand in, once per
platform. Each script takes the directory to make as its one argument,
`marbas-manual` in the home directory when none is given, refuses a directory
that is already there and removes nothing.

```
pwsh _probes/ManualPass/fixture.ps1        # Windows
sh _probes/ManualPass/fixture.sh           # Linux
```

| Directory | Holds | For |
|-----------|-------|-----|
| `kinds` | a directory, a plain file, an archive, a program, a link and a dotfile | the colours |
| `many` | 120 files and `zebra.txt` | the arrows, the pages, the marks, the quick search |
| `trash` | a file, and a directory with a file in it | the trash and the way back out of it |
| `foreign` | archives made by `tar`, `zip` and 7-Zip, each holding one tree, and the trees under `src` | unpacking what another tool wrote |
| `pack` | `tree`, the same tree | packing for another tool to open |
| `out` | nothing | where archives are written and unpacked |
| `edit` | one file | `F4` |
| `launch` | a document, a file of an extension nobody knows, and programs that wait, print or say what rights they hold | starting programs |
| `attrs` | `flags-dir` | the properties popup |
| `backslash` | `a\b.txt`, on Linux only | a backslash in a name packed into a zip |
| `perm` | a directory without its `x` bit and one only root may write to, on Linux only | real permission bits |

Every file and directory of a tree is dated `2001-02-03 04:05:06`, local time,
so a date that survived is told at a glance from one that was made today. The
Linux script asks `sudo` for the one directory only root may write to, and says
what to type when `sudo` did not run; it also says which archives it left out
because `zip` or 7-Zip is not installed.

Start Marbas with `--debug`, so that a step that goes wrong leaves its trail in
`marbas.log` beside the settings. The pass is run with the settings the person
already has: the steps add what they need — a program for one extension, a few
entries of the user menu — and the last section says what to take out again.

## Writing down what was seen

Answer by step: its number and what happened, where that is not what the
*Should* column says. A step is one of four things afterwards.

- **As it should be.** The row keeps its claim in that platform's cell, and its
  *Evidence* cell gains the date: `manual 2026-10-12`. The bold **manual**,
  which marks a row nobody has walked, goes.
- **Different, and that is how it is.** A key a terminal keeps for itself, a
  tool that cannot open what it was given. The cell says what was seen instead,
  with the terminal or the tool named.
- **Wrong.** The cell says what was seen, and the defect is an entry in
  [Rough edges](../ROUGH-EDGES.md).
- **Not walked.** No desktop at hand, a tool not installed. The row stays bold
  and says what it is waiting for.

Name the terminal with the answers: the colours and the shifted keys are the
terminal's doing, and two terminals on one system need not agree.

## On Windows

Run the steps in the terminal Marbas is used in. W24 wants cmder as well, the
one terminal the defect it checks was ever seen in.

### The look

| Step | Do | Should | Row |
|------|----|--------|-----|
| W1 | Start Marbas and look at the whole screen. | Both panels blue with bright cyan frames, the cursor bar black on cyan, the two rows under the panels light grey on black, the key bar black on cyan. | *The sixteen classic colours look right* |
| W2 | Go into `kinds`, with the hidden entries shown (`Alt+H`). | `folder` white, `plain.txt` bright blue, `archive.zip` bright magenta, `program.exe` bright green, `junction` bright cyan — five colours, none mistaken for another. | *Directory / file / archive / program colours are told apart* |
| W3 | Read `plain.txt` and the two hidden names, with the cursor elsewhere. | Bright blue on the blue panel reads without effort. | *Bright blue file names stay readable on the blue panel* |
| W4 | Look at the line between the listing and the footer of each panel. | It runs from the left border to the right one with no gap and no stray character. Say whether it meets the border in a joint (`╟`, `├`) or only touches it. | *The rule under the listing joins the panel border* |
| W5 | Look at the top right corner; `Alt+F11`; `Alt+Shift+F11`. | `[1/1]`, then `[2/2]`, then `[1/1]` again, legible beside the right panel's title. | *The tab counter is legible in the top right corner* |
| W6 | Drag the terminal window narrower, wider, shorter and taller; maximise it and bring it back. | The screen is drawn whole at every size: nothing left over from the size before, the bar spread over the new width. | *The screen redraws when the terminal is resized* |

### The keys and the mouse

In `many`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| W7 | Hold `↓` past the last entry, `↑` past the first; then `Tab`. | The cursor stops at each end and the other panel never takes it; `Tab` does. | *Arrows stop at the ends of a panel; only `Tab` leaves it* |
| W8 | `→` three times, `←` three times. | Each press moves the cursor by the page on screen, in the same panel. | *`←` / `→` jump by the page on screen* |
| W9 | `Shift+↓` three times, `Shift+↑` once. | Each press marks or unmarks one entry, in yellow, and the footer counts them. | *`Shift+↑` / `Shift+↓` arrive as keys of their own and mark a run* |
| W10 | `Shift+PgDn`, `Shift+PgUp`, `Shift+→`, `Shift+←`. | Each marks or unmarks a page. Say which of the four the terminal handed over. | *`Shift+PgUp` / `Shift+PgDn` and `Shift+←` / `Shift+→` mark a page* |
| W11 | Type `zeb`; then `Esc`. | A search bar opens with what was typed and the cursor lands on `zebra.txt`; `Esc` closes the bar. | *Typing opens the search bar and moves the cursor* |
| W12 | Click an entry in the panel that is not active; click `F3` on the bar with the cursor on a file. | The cursor and the double border go where the click was; the viewer opens. | *The mouse selects an entry and clicks the key bar* |

### The trash

In `trash`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| W13 | `F8` on `to-trash.txt` and on `to-trash-dir`, confirming each; open the Recycle Bin. | Both are in it, each with `trash` as the place it came from. | *What the trash does with them* |
| W14 | *Restore* both from the Recycle Bin. | Both are back in the panel without a key, the directory with `inner.txt` in it. | *A deleted entry can be put back by the desktop's own tool* |

### Archives

One panel in `foreign`, the other in `out`; then one in `pack`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| W15 | Mark the three archives of `foreign`; `Shift+F2`. | `out` holds `from-tar`, `from-7zip-zip` and `from-7zip-7z`, each with `a.txt` and `sub\b.txt`, every one dated `2001-02-03 04:05`. | *An archive made by another tool unpacks here with its tree and its times* |
| W16 | In `pack`, on `tree`: `Shift+F1`, zip, no password. In Explorer, *Extract All* on `out\tree.zip`. | Explorer opens it and extracts `tree\a.txt` and `tree\sub\b.txt`. | *The zip opens in the system's own tool* |
| W17 | `Shift+F1` again, tar.gz. In a shell in `out`: `tar -tvzf tree.tar.gz`, then `mkdir from-marbas-tar` and `tar -xzf tree.tar.gz -C from-marbas-tar`. | The listing names `tree/a.txt` and `tree/sub/b.txt`; the extraction ends without a word. | *The tar.gz opens in the system's own tool* |
| W18 | `Shift+F1`, zip, password `marbas`, named `locked.zip`. Open it in Explorer; then `7z x locked.zip -ofrom-marbas-locked` in a shell. | Explorer cannot open an AES zip and says the archive is empty or damaged — its doing, not the archive's. 7-Zip asks for the password and extracts with it. | *A password-protected zip asks for the password there* |
| W19 | Compare *Date modified* of the files and directories extracted in W16 and W17 with `2001-02-03 04:05`. | The same to the minute, for the files and for the directories. | *Modification times survive the round trip* |

7-Zip's own listing (`7z l`) applies today's offset from UTC to every date, so
across a change of daylight-saving time it shows the hour one off. The files it
extracts are what to compare, and the hour is part of the comparison: the
fixture is dated in winter so that a pass walked in summer shows a tool that
got the offset wrong.

### Editing

In `edit`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| W20 | `F12`: extension `log`, program `C:\Windows\System32\edit.exe`, used for *Edit*, `Add`. Then `F4` on `edit-me.log`, and move the cursor in the panel. | The editor opens in a console window of its own, and the panel behind it takes keys at once. | *A console editor gets a window of its own* |
| W21 | In the editor add a line, save (`Ctrl+S`) and quit (`Ctrl+Q`). | The size of `edit-me.log` in the panel changes without a key. | *A file saved in the editor shows in the panel* |

### Starting programs

In `launch`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| W22 | `Enter` on `waits.cmd`; with its window open, move the cursor and `F3` a file in the panel; then close the window. | A console window of its own; the panels take keys the whole time. | *A started program does not hold the loop up* |
| W23 | `Enter` on `document.txt`. | It opens in the program the system has for it, and nothing is written over the panels. | *A started program leaves the panels un-scribbled* |
| W24 | Under cmder, with VS Code not running: `F2` and the letter of an entry whose command is `code.exe %p`, on `document.txt`. Wait ten seconds. | VS Code opens the file and not one character lands on the panels. | *A started program leaves the panels un-scribbled* |
| W25 | `F2`, and with `Ins` add: `D`, `notepad.exe %p`, *Directly*; `S`, `echo %n & timeout /t 3`, *Shell, window closes*; `K`, `echo %n`, *Shell, window stays*. Run each on `document.txt`. | `D` opens Notepad on the file. `S` shows the name in a window that closes by itself after three seconds. `K` shows the name in a window that stays, with a prompt standing in `launch`. | *A user menu entry is started the way it names* |
| W26 | Add `A`, `cmd.exe /k cd`, *Directly*, *As administrator*. Run it and answer *Yes*. | The system asks first; the window that opens is an administrator's. Say which directory it prints. | *A user menu entry runs as administrator* |
| W27 | Add `X`, `no-such-program-zzqx %p`, *Directly*. Run it. | *Cannot run command* names the program that could not be found; the panels take keys after it. | *A program that is not there is named, not thrown* |
| W28 | `Alt+Shift+R` on `elevated.cmd`; answer *Yes*. | The system asks first; the window says `High Mandatory Level` and the directory it started in. | *An executable starts with administrator rights* |
| W29 | `Alt+Shift+R` on `elevated.cmd`; answer *No*. | Nothing at all: no window, no dialog, the panels as they were. | *A prompt the user turns down leaves no trace* |
| W30 | One level up, `Alt+R` on `launch`. | Explorer opens on `launch`; neither panel moves. | *`Alt+R` on a directory opens the file manager* |
| W31 | `Alt+F1`, then `Alt+R` on a drive. | Explorer opens on that drive; neither panel moves. | *`Alt+R` in the popup opens the drive in the system file manager* |
| W32 | `Alt+R` on `document.txt`; `Esc`. | The popup lists the programs the system knows for `.txt`, Notepad among them. | *The handlers of a common extension are listed* |
| W33 | `Alt+R` on `unknown.zzqx`; `Esc`. | The list is empty; the field for a path of one's own is there. | *An extension the system knows nothing about lists nothing* |

### After quitting

Not a row of the checklist: P21.8 left it to a person.

| Step | Do | Should |
|------|----|--------|
| W34 | `Alt+C` on a file, `F10`, paste into Notepad. Start again, mark two files, `Alt+Shift+C`, `F10`, paste into an Explorer window. | The name is pasted as text, and the two files are copied, with Marbas no longer running. |

## On Linux

Install `zip`, `unzip` and 7-Zip before the fixture is made, or `foreign` holds
`tar`'s archive alone and L25 has nothing else to unpack. Copy the binary,
`chmod +x Marbas`, and start it as `EDITOR=vim ./Marbas
--debug`. A step marked **desktop** needs a session with a window manager and a
file manager — GNOME, KDE, Xfce. A terminal under WSLg has neither, and those
steps are not walked there.

Without a Linux machine, the rest is walked under WSLg in a terminal installed
inside the distribution, which is a real Linux terminal with a window, a mouse
and a close button of its own:

```
wsl -d Ubuntu-24.04 -- gnome-terminal --wait
```

Keep a second shell of the distribution open beside it. The distribution shuts
down once nothing is attached to it, and takes `/tmp` — and so what L46 looks
for — with it. A Linux build started in a Windows terminal instead is a
different pass: the keys, the colours and the mouse are then that terminal's.

### The look

| Step | Do | Should | Row |
|------|----|--------|-----|
| L1 | Start Marbas and look at the whole screen. | As W1. | *The sixteen classic colours look right* |
| L2 | Go into `kinds`, with the hidden entries shown (`Alt+H`). | `folder` white, `plain.txt` bright blue, `archive.tar.gz` bright magenta, `program` bright green, `link` bright cyan. | *Directory / file / archive / program colours are told apart* |
| L3 | Read `plain.txt` and `.dotfile`, with the cursor elsewhere. | As W3. | *Bright blue file names stay readable on the blue panel* |
| L4 | Look at the line between the listing and the footer. | As W4. | *The rule under the listing joins the panel border* |
| L5 | Look at the top right corner; `Alt+F11`; `Alt+Shift+F11`. | As W5. | *The tab counter is legible in the top right corner* |
| L6 | Resize the terminal window every way. | As W6. | *The screen redraws when the terminal is resized* |

### The keys and the mouse

In `many`, then in `kinds` and `launch`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| L7 | Hold `↓` past the last entry, `↑` past the first; then `Tab`. | As W7. | *Arrows stop at the ends of a panel; only `Tab` leaves it* |
| L8 | `→` three times, `←` three times. | As W8. | *`←` / `→` jump by the page on screen* |
| L9 | `Shift+↓` three times, `Shift+↑` once. | As W9. | *`Shift+↑` / `Shift+↓` arrive as keys of their own and mark a run* |
| L10 | `Shift+PgDn`, `Shift+PgUp`, `Shift+→`, `Shift+←`. | As W10. A terminal that scrolls its own history on the shifted page keys keeps them. | *`Shift+PgUp` / `Shift+PgDn` and `Shift+←` / `Shift+→` mark a page* |
| L11 | Type `zeb`; then `Esc`. | As W11. | *Typing opens the search bar and moves the cursor* |
| L12 | Click an entry in the other panel; click `F3` on the bar. | As W12. | *The mouse selects an entry and clicks the key bar* |
| L13 | One level up, `F3` on `many` so that its size replaces `<DIR>`; then `Ctrl+R`. | `<DIR>` is back: the panel was read again. | *`Ctrl+R`, `Ctrl+F3`–`Ctrl+F6`* |
| L14 | `Ctrl+F3`, `Ctrl+F4`, `Ctrl+F5`, `Ctrl+F6`, each twice. | The title names the sort — name, extension, date, size — and the second press turns its arrow. | *`Ctrl+R`, `Ctrl+F3`–`Ctrl+F6`* |
| L15 | In `many`: `+`, `*.txt`, `Enter`; then `-`, `*.txt`, `Enter`. | Each key opens the pattern prompt; the first marks every file, the second unmarks them. | *`+` / `-` open the pattern prompt* |
| L16 | In `kinds`: `Ctrl+PgDn` on `folder`, back, `Ctrl+PgDn` on `archive.tar.gz`. | The panel walks into each. | *`Ctrl+PgDn` reaches the panel as a key at all* |
| L17 | In `launch`: `Shift+Enter` on `document.txt`. | The open-with popup. Say so if the file is run instead, which is what a terminal that drops the `Shift` leads to. | *`Shift+Enter` reaches the panel as a key at all* |
| L18 | `Ctrl+Alt+Enter` on `sleeper`. | A dialog saying a terminal application cannot ask this system for administrator rights. Say so if nothing happens, or if `sleeper` is run. | *`Ctrl+Alt+Enter` reaches the panel as a key at all* |
| L19 | `Alt+W` on a directory; `Alt+Shift+R` on `sleeper`. | The panel walks in; the dialog of L18, and not the open-with popup. | [Rough edges](../ROUGH-EDGES.md), *`Alt+W` and `Alt+Shift+R` have not been pressed on Linux yet* |

### Drives

| Step | Do | Should | Row |
|------|----|--------|-----|
| L20 | `Alt+F1`; `Esc`; `Alt+F2`. | *Drives* opens over the left panel, then over the right: the mounts that hold files, then the home directory. On a GNOME desktop neither key arrives; say what the desktop did with them. | *`Alt+F1` / `Alt+F2` open Drives* |
| L21 | Look at the rows of the popup. | Root, kind, size and free space in columns that line up, and no column of labels. | *Each row shows root, label, kind, size and free space in aligned columns* |
| L22 | **desktop.** `Alt+R` on a row of the popup. | The file manager opens on that mount; neither panel moves. | *`Alt+R` in the popup opens the drive in the system file manager* |

### The trash

In `trash`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| L23 | `F8` on `to-trash.txt` and on `to-trash-dir`. In a shell: `ls ~/.local/share/Trash/files ~/.local/share/Trash/info`. | Both are under `files`, and `info` holds a `.trashinfo` for each, naming the path it came from. | *What the trash does with them* |
| L24 | **desktop.** Open the trash in the file manager and *Restore* both. Without one, `gio trash --list` and `gio trash --restore` with the address it prints. | Both are back in the panel without a key. Say which tool put them back, or that `gio` listed nothing. | *A deleted entry can be put back by the desktop's own tool* |

### Archives

One panel in `foreign`, the other in `out`; then one in `pack` and in
`backslash`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| L25 | Mark the archives of `foreign`; `Shift+F2`. | `out` holds a tree per archive, each with `a.txt` and `sub/b.txt`, every one dated `2001-02-03 04:05`. | *An archive made by another tool unpacks here with its tree and its times* |
| L26 | In `pack`, on `tree`: `Shift+F1`, zip, no password. In a shell in `out`: `unzip -l tree.zip`, then `unzip tree.zip -d from-marbas-zip`. **desktop:** open it in the archive manager as well. | The listing names `tree/a.txt` and `tree/sub/b.txt`, and both tools extract them. | *The zip opens in the system's own tool* |
| L27 | `Shift+F1` again, tar.gz. `tar -tvzf tree.tar.gz`, then `mkdir from-marbas-tar` and `tar -xzf tree.tar.gz -C from-marbas-tar`. | As W17. | *The tar.gz opens in the system's own tool* |
| L28 | `Shift+F1`, zip, password `marbas`, named `locked.zip`. `7z x locked.zip -ofrom-marbas-locked`. | 7-Zip asks for the password and extracts with it. `unzip` knows no AES and says so, which is `unzip`'s doing. | *A password-protected zip asks for the password there* |
| L29 | `ls -lR --time-style=long-iso from-marbas-zip from-marbas-tar`. | Every file and directory is dated `2001-02-03 04:05`. | *Modification times survive the round trip* |
| L30 | In `backslash`, on `a\b.txt`: `Shift+F1`, zip. `unzip -l` the archive, and `Enter` on it in the panel. | The defect the row describes: the entry is stored as `a/b.txt`, and the panel shows a directory `a` that was never on the disk. Say so if the name came through whole. | *A backslash inside a Linux file name stays in the entry name* |

### Permissions

In `perm`, as an ordinary user.

| Step | Do | Should | Row |
|------|----|--------|-----|
| L31 | In `root-owned`: `F8` on `file.txt`, then `Shift+F8`. | Both are refused in a dialog that names the file, and the file stays. | *A root-owned file refuses to be deleted and is named in the dialog* |
| L32 | `Enter` on `no-enter`. | A dialog says access to it is denied, and the panel stays in `perm`. | *A directory without `+x` is reported when entered* |

### Properties

In `attrs`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| L33 | `F4` on `flags-dir`, tick read-only, accept; `F4` again. Then tick hidden, system and archive, one at a time. Untick read-only at the end. | Read-only is ticked when the popup opens again, and `ls -ld flags-dir` shows no `w`. Say what each of the other three does: nothing, a refusal in a dialog, or a tick that is gone when the popup opens again. | *A flag written to a directory is read back* |

### The editor

In `edit`, with `EDITOR=vim`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| L34 | `F4` on `edit-me.txt`. Move with the arrows, `i`, type a line, `Esc`, `:wq`. | `vim` has the whole screen and every key; none reaches the panels. | *A full-screen console editor gets every key and gives the terminal back* |
| L35 | Look at the screen when `vim` has closed. | Both panels whole, no line of `vim` left, the cursor where it was, and the file at its new size. | *A file saved in the editor shows in the panel* |
| L36 | `F4` again; resize the terminal window while `vim` is up; `:q`. | The panels come back whole at the new size and take keys. | *The screen is whole again after the editor exits, at the size it has then* |

### Starting programs

In `launch`.

| Step | Do | Should | Row |
|------|----|--------|-----|
| L37 | `Enter` on `noisy`, which prints to both of its streams for five seconds. | Not one character lands on the panels. | *A started program leaves the panels un-scribbled* |
| L38 | `Enter` on `sleeper`, which sleeps for thirty seconds; move the cursor at once. | The panels take keys at once. | *A started program does not hold the loop up* |
| L39 | `F2`, and with `Ins` add: `D`, `touch direct-ran`, *Directly*; `S`, `echo %n > shell-ran`, *Shell, window closes*; `K`, `echo %n > keep-ran`, *Shell, window stays*. Run each on `document.txt`. | `direct-ran`, `shell-ran` and `keep-ran` appear in the panel without a key, the last two holding `document.txt`; no window opens and nothing is printed. | *A user menu entry is started the way it names* |
| L40 | Add `A`, `touch admin-ran`, *Directly*, *As administrator*. Run it. | A dialog says a terminal application cannot ask this system for administrator rights; no `admin-ran` appears. | *A user menu entry runs as administrator* |
| L41 | Add `X`, `no-such-program-zzqx`, *Directly*. Run it. | *Cannot run command* names the program that could not be found; the panels take keys after it. | *A program that is not there is named, not thrown* |
| L42 | **desktop.** One level up, `Alt+R` on `launch`. | The desktop's file manager opens on `launch`; neither panel moves. Without `xdg-open`, say what the dialog says. | *`Alt+R` on a directory opens the file manager* |

### What a desktop keeps

| Step | Do | Should | Row |
|------|----|--------|-----|
| L43 | **desktop**, GNOME. `Alt+F1`, `Alt+F2`, `Alt+F6`, `Alt+F7`, `Alt+F8`. | None reaches Marbas: the desktop opens its menu and its run dialog, cycles its windows, and starts moving and resizing the window. Say which of the five did arrive. | *`Alt+F1`, `Alt+F2` and `Alt+F6` reach the application on a desktop*; *`Alt+F7` reaches the application on a desktop*; *`Alt+F8`* |
| L44 | **desktop**, GNOME. `Alt+Shift+F`; `Esc`; `Alt+B`. | *Find*, then *Favourites*: the stand-ins arrive where the function keys do not. | *`Alt+Shift+F` opens Find wherever `Alt+F7` does not* |
| L45 | **desktop**, KDE or Xfce. `Alt+F7`; then `Alt+F12`, `Alt+Shift+H`, `Ctrl+PgUp`, `Ctrl+PgDn`; in the history popup `Alt+G`, `Del`, `Shift+Del`; in a field `Alt+↓` and `F4`; then `Alt+F8`, `F2`, `Alt+K`, `Alt+0`. | Each does in Marbas what the [user guide](../USER-GUIDE.md) says. Name every one the desktop kept, and what it did with it. | the three rows marked *manual for KDE and Xfce* |
| L46 | With Marbas running, close the terminal window by its button. Start Marbas again and, a few seconds later, `ls ${TMPDIR:-/tmp}/Marbas`. | One directory, the new session's: the one the closed session left is gone. | *The directory of a session closed with the console window's button is gone after the next start* |

## Clearing up

- The settings popup keeps `log` → `edit.exe` under *Edited with:* until it is
  removed there, and the user menu keeps the entries the steps added until
  `Del` takes them out. Take them out from the top: `Del` on the bottom row
  ends the session, a defect [Rough edges](../ROUGH-EDGES.md) holds since the
  first pass. The Linux steps add no row to the settings popup: their editor
  comes from `EDITOR` in the environment.
- The fixture is removed by hand. On Linux `perm/no-enter` wants its `x` bit
  back first (`chmod u+x`), and `perm/root-owned` wants `sudo`.
- `--debug` is gone with the session; `marbas.log` keeps its last megabyte.

## The passes so far

| Date | Binaries | Windows | Linux | Not walked |
|------|----------|---------|-------|------------|
| 2026-10-12 | phase 21, at P21.17 | cmder, W1–W34 | gnome-terminal 3.52.0 under WSLg, Ubuntu 24.04 | L43–L45, for want of a desktop; the window of L22 and L42, the file manager of L24 and the archive manager of L26, the same; the `zip` and 7-Zip archives of L25, not installed when the fixture was made; L37 and L38 as written, since no executable is started there |

What the first pass found other than the steps said is of two kinds. Two are a
terminal keeping a key, `Shift+Enter` and `Ctrl+Alt+Enter` under
gnome-terminal, as Windows Terminal does. The rest are defects, each an entry in
[Rough edges](../ROUGH-EDGES.md): the rule that does not join the border; a
directory's time lost through an archive, and a file's hour through a zip;
`Alt+F11` taken under WSLg; the executable bit never read on Linux, which is
what L2, L19, L37 and L38 all ran into; the three flags of the properties popup
that do nothing there; the backslash turned into a level, which L30 was written
to confirm; and `Del` on the bottom row of the user menu, which ended the
session on both systems while the steps were being cleared up. What the log and
the disk held was read beside the answers, and in three places it said more
than the walker had: the hour of the files, the directories unpacked from
another tool's archive, and the colour of a program on Linux.
