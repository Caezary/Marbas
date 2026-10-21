# Tabs

A tab is a whole pair of panels and the choice of the active one. Switching
tabs changes both panels at once.

| Key | Alternative | Action |
|-----|-------------|--------|
| `F11` | `Alt+T` | List the tabs |
| `Alt+F11` | `Alt+Z` | Open a new tab |
| `Alt+Shift+F11` | `Alt+Shift+Z` | Close the tab in front |
| `Alt+←` | | Go to the previous tab |
| `Alt+→` | | Go to the next tab |

- A new tab opens on the directories on screen, with their cursors and without
  their marks.
- The last tab cannot be closed.
- `Alt+←` and `Alt+→` wrap round at the ends.
- The top right corner says which tab is in front and how many there are,
  such as `[2/3]`. A path in the right panel's title that no longer fits is
  shortened to `C:\...\parent\current`.
- Coming back to a tab reads both of its panels again. A directory that has
  gone is reported, and the tab is switched all the same.
- Opening, switching and closing a tab save the session.

## The list of tabs

One row per tab: its number and where both of its panels stand.

| Key | Alternative | Action |
|-----|-------------|--------|
| `Enter` | | Go to the tab under the cursor |
| `Esc` | | Close the list and stay |

> [!NOTE]
> Windows Terminal and the Windows console keep `F11` for full screen; press
> `Alt+T`. gnome-terminal under WSLg keeps `Alt+F11` for full screen and does
> not pass on `Alt+Shift+F11`; press `Alt+Z` and `Alt+Shift+Z`.
