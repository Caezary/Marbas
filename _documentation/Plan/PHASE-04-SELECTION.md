# Phase 4 — Selection

Goal: mark files the orthodox way, as input for file operations.

- **P4.1 Single selection cursor info** — status line shows the entry under
  the cursor (full name, size, date). *Done when:* info updates as the cursor
  moves.
- **P4.2 Multi-selection** — `Insert`/`Space` toggles selection and advances
  the cursor; selected entries are highlighted; ".." is never selectable.
  *Done when:* multiple entries can be marked per panel.
- **P4.3 Selection summary & patterns** — count and total size of selected
  files shown in the panel; `+`/`-` (gray keys) select/deselect by wildcard
  pattern. *Done when:* `*.fs` can be selected in one step.
