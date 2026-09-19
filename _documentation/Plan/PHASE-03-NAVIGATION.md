# Phase 3 — Navigation

Goal: independent browsing in each panel.

- **P3.1 Panel state** — each panel tracks its own current directory;
  a shared app-state record holds both panels plus which one is active.
  *Done when:* state model exists and drives rendering.
- **P3.2 Enter a directory** — `Enter` on a directory (or "..") navigates
  into it and refreshes the listing; cursor lands on the directory you came
  from when going up. *Done when:* full up/down navigation works in both
  panels independently.
- **P3.3 Error handling** — inaccessible directories (permissions, removed
  media) show a message box and leave the panel where it was. *Done when:*
  no navigation action can crash the app.
- **P3.4 Manual refresh** — `Ctrl+R` re-reads the active panel. *Done when:*
  external changes appear after refresh.
- **P3.5 Drive / root selection (Windows)** — a dialog (`Alt+F1`/`Alt+F2`)
  to switch a panel to another drive; on Unix, jump to `/` and `~`.
  *Done when:* panels can reach any drive.
