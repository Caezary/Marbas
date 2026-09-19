# Phase 2 — Directory listing (read-only)

Goal: panels show real directory contents.

- **P2.1 Directory reading module** — `DirectoryListing` module returning
  entries (name, kind, size, modified date) for a given path; errors returned
  as values, not exceptions. *Done when:* unit-testable function exists.
- **P2.2 Render entries in a panel** — hook the listing into each panel's
  list view; panel title shows the current path. *Done when:* both panels
  list the startup directory.
- **P2.3 Ordering and the ".." entry** — directories first, then files,
  alphabetically; ".." at the top for non-root directories. *Done when:*
  ordering matches the orthodox conventions.
- **P2.4 Entry formatting** — columns for name, size (dirs show `<DIR>`),
  and modification date. *Done when:* columns align and truncate gracefully.
