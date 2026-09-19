# Phase 12 — Watching the directories on screen

The tasks are in [Phase 12 — Watching the directories on screen](../Plan/PHASE-12-WATCHING-THE-DIRECTORIES.md).

**A twelfth phase made the panels follow the disk.** What they show is now what
the disk holds: a file written by another program, a directory removed from a
shell, the whole directory a panel stands in deleted, a memory stick pulled out
— each of them reaches the screen on its own, without `Ctrl+R`. The watch
follows the tab in front and no other, since switching to a tab already re-reads
it; an archive is watched by its file, because that is what the operating system
can report on; and the effect that arms it is raised by comparing where the
panels stood before and after a message, so no transition has to remember to ask
for it. A burst is collapsed: two hundred files written in one go cost one
listing, not two hundred. What the watcher cannot say — its own directory
deleted, its buffer overflowing, a drive removed, which no console application
can hear at all — is read as a reason to look again rather than as something to
report, with a two-second poll as the net underneath; a panel whose place is gone
steps up to the nearest directory that is still there, or onto a root that
exists, and says nothing, because the user did nothing to move it. Nothing is
re-read while a popup, the viewer or a batch's progress window is in front: that
wait is the same mechanism as the burst, so whatever happened underneath becomes
a single listing once the screen is the panels' own again.
