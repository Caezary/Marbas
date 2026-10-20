# Help

Phase 24. The user guide, read on GitHub and shown full screen under `F1`.

## The keys of the help

| Key | Alternative | Action |
|-----|-------------|--------|
| `F1` | | Open the help: in a panel, the viewer or a popup |
| `Tab` | | Next link on screen |
| `Shift+Tab` | | Previous link on screen |
| `Enter` | | Follow the link under the cursor |
| `Backspace` | `Alt+←` | Back to where the link was followed |
| `F7` | | Search the chapter on screen |
| `Shift+F7` | | Repeat the last search without the popup |
| `↑` `↓` `PgUp` `PgDn` `Home` `End` | | Scroll, as in the viewer |
| `Esc` | `F10` | Close the help |

`F1` has no spare. P24.1 measured it, `Tab`, `Shift+Tab`, `Backspace` and
`Alt+←` in the five terminals of the checklist, and every one arrived in every
place it is meant for; see
[Cross-platform](CROSS-PLATFORM.md#keyboard-and-terminal). `Ctrl+F1`, `Alt+S`
and `Alt+Y` were measured beside `F1` as spares and arrived as well; none is
bound. `F7`, `Shift+F7`, `Esc` and `F10` are the viewer's keys and were
measured for a full-screen window before.

`Backspace` and `Alt+←` mean other things in a panel, where they edit the quick
search and walk to the previous tab. The help is a full-screen window of its
own, so a key there never reaches a panel.

## The view is Marbas's own

Terminal.Gui 2.4.17 has a `Markdown` view. A spike on 2026-10-21 rendered a
chapter in it at 40, 80 and 120 columns. It walks links with `Tab` and
`Shift+Tab`, raises `LinkClicked` on `Enter` and scrolls to a heading by
GitHub's anchor, `notes-1` for a second `Notes` included. Five things rule it
out:

- A table's height does not follow its wrapped rows: at 80 columns its last
  rows are missing, and at 120 the bullets under it are blank until the view
  scrolls. At 40 columns words break inside, `Alternativ` / `e`.
- `[!NOTE]` and `[!WARNING]` both draw as a plain faint quote, with Markdig's
  alert extension too.
- A link wrapped over two lines is two `Tab` stops, each lighting half of it.
- Inline code has a fixed grey background, poorly legible on the blue panel.
- Nothing selects a range of text, so `F7` could not show a match.

The help lays out the chapter itself, as
[the plan](../Plan/PHASE-24-THE-GUIDE-AND-THE-HELP.md) decided. Markdig 1.3.2,
which Terminal.Gui already depends on, parses it, and its anchors are the ones
the spike saw `ScrollToAnchor` resolve.
