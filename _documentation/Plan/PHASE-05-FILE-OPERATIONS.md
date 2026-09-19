# Phase 5 — File operations

Goal: the manager actually manages files. Every operation acts on the
selection (or the cursor entry when nothing is selected), targets the
opposite panel where applicable, asks for confirmation, and refreshes both
panels afterwards.

- **P5.1 Operations module** — copy/move/rename/delete/mkdir implemented
  in a UI-free module over `System.IO.Abstractions`, returning per-item
  results. *Done when:* covered by unit tests against an in-memory FS.
- **P5.2 F7 make directory** — prompt for a name, create in the active
  panel. *Done when:* new directory appears and gets the cursor.
- **P5.3 F8 delete** — confirmation dialog (extra warning for non-empty
  directories), recursive delete. *Done when:* delete works with errors
  reported per item.
- **P5.4 F5 copy** — copy selection to the other panel's directory, with
  overwrite confirmation (Yes / No / All / Cancel). *Done when:* files and
  directory trees copy correctly.
- **P5.5 F6 move / rename** — move selection to the other panel; when the
  target edit-box is given a bare new name, it acts as rename. *Done when:*
  move and rename both work.
- **P5.6 Progress & cancellation** — long operations run off the UI thread
  with a progress dialog and `Esc` cancellation. *Done when:* copying a
  large tree shows progress and can be cancelled safely.
