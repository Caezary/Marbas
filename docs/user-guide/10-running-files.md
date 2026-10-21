# Running files and the user menu

`Enter` runs the file under the cursor, `Alt+R` chooses the program for it,
and the user menu runs commands of your own. Marbas does not wait for what it
starts: the panels take keys again at once, and what the program changes on
the disk shows up in them by itself.

## Running a file

| Key | Alternative | Action |
|-----|-------------|--------|
| `Enter` | | Run the file under the cursor |
| `Alt+R` | | Choose the program to open the file with |
| `Alt+Shift+R` | | Run the program as administrator |

- `Enter` starts the program the extension is *Opened with* in
  [Setup](12-settings.md#setup). Without one, the file goes to the system,
  which starts a program and opens a document or a shortcut.
- On Linux a file with its execute bit set is started itself; anything else
  goes to `xdg-open`.
- The program starts in the directory of the panel. Only the entry under the
  cursor runs; the marks are ignored.
- `Alt+R` on a file lists the programs the system knows for its extension and
  has a field for a program of your own. A tick keeps the choice as the
  program the extension is *Opened with*.
- `Alt+R` on a directory opens it in the system file manager, and on `..` the
  directory the panel shows.
- `Alt+Shift+R` raises the rights of the program under the cursor, so the
  system asks first. Declining runs nothing and says nothing. A document or a
  directory is refused.
- A file inside an archive does not run: it would have to be unpacked first.
- A file started is a row of the *History* tab of
  [History](13-history.md#the-history-popup).

> [!NOTE]
> On Windows a console program gets a console window of its own, and a window
> program's output is thrown away, so it cannot write over the panels.

> [!WARNING]
> On Linux `Alt+Shift+R` is refused: a terminal program cannot ask the system
> for administrator rights. Without `xdg-open`, from the package xdg-utils,
> documents, directories and drives cannot be handed over, and Marbas says so.

## The user menu

| Key | Alternative | Action |
|-----|-------------|--------|
| `F2` | | Open the user menu |

The menu lists the commands you have defined, each on a letter or a digit of
its own, with lines between groups.

| Key | Alternative | Action |
|-----|-------------|--------|
| `A` … `Z` | `0` … `9` | Run the entry on that letter or digit |
| `↑` `↓` | | Move between the entries |
| `Enter` | | Run the entry under the cursor |
| `Esc` | | Close the menu, running nothing |

- A letter runs its entry in either case, and the menu closes.
- `↑` and `↓` step over the lines and stop at the first and the last entry.
- A command that ran is a row of the *History* tab of
  [History](13-history.md#the-history-popup): `Enter` there runs it again
  exactly as it ran, and `Alt+G` sends the panel to the directory it ran in.

### What an entry holds

An entry is a label, its letter, a command line, a way of running and whether
it runs as administrator. The command line names a program and its arguments;
words in double quotes count as one. It may hold these:

| Write | For |
|-------|-----|
| `%n` | the name under the cursor |
| `%p` | its full path |
| `%s` | the full path of every marked entry, or of the one under the cursor |
| `%d` | the panel's directory |
| `%Ln` `%Lp` `%Ls` `%Ld` | the same of the left panel |
| `%Rn` `%Rp` `%Rs` `%Rd` | the same of the right panel |
| `%f` | a file listing the marked entries, one per line |
| `%%` | a percent sign |

- Without `L` or `R` a placeholder speaks of the active panel.
- Every path goes in as one argument, whatever spaces or quotes it holds. `%s`
  puts each entry in as an argument of its own.
- A `%` followed by anything else stays as you typed it.
- A command is refused, with a dialog saying why, when a placeholder names
  something inside an archive, when `..` is under the cursor and nothing is
  marked, or when `%d` names a panel of search results.
- An entry starts its program directly, or hands the line to the shell, which
  either closes its window when the command ends or keeps it open to read.
  The shell is `cmd /c` and `cmd /k` on Windows, `sh -c` on Linux.
- As administrator, the system asks first, as for `Alt+Shift+R`.
- The program starts in the directory of the active panel, or in the one
  holding the archive when the panel is inside one.

### Changing the menu

A bare letter runs an entry, so the keys that change the menu are keys a
letter cannot be.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Ins` | | Add an entry, or a line, under the cursor |
| `F4` | | Change the entry under the cursor |
| `Del` | | Remove the entry, or a line beside it |
| `Ctrl+↑` | | Move the entry up a row |
| `Ctrl+↓` | | Move the entry down a row |

- `Ins` asks for the label, the letter, the command line, how it runs and
  *As administrator*. It can add a line above or below the entry instead.
- `Del` asks first, and offers a line right above or below the entry as well.
  The answer offered is *Cancel*.
- An entry moved past a line goes to the other side of it.
- A letter another entry has, a blank label and a blank command line are
  refused, and the popup stays open saying why.
- Every change is written at once, so two copies of Marbas changing the menu
  keep each other's entries. The file can be edited by hand as well, see
  [The files Marbas keeps](15-files-marbas-keeps.md#the-user-menu).
