# Phase 28 — Branches and remotes

Draft: written before phase 25 was built, and expected to change with what it
teaches.

Goal: branches, tags and remotes are listed and changed from popups, the
current branch is fetched, pushed and fast-forwarded, and `F9` outside a
repository can clone one. This is the phase that talks to other machines, so
it is also the one that settles how git asks for credentials.

Four decisions run through the whole phase.

**A network call runs in the background and never asks in the terminal.** It
runs under the progress window, which shows the last line of git's
`--progress` and stops the process on `Esc`. `GIT_TERMINAL_PROMPT=0` keeps git
from asking for a user name or a password; when neither `GIT_SSH_COMMAND` nor
`core.sshCommand` is set, `ssh -o BatchMode=yes` keeps ssh from asking for a
passphrase or a host key. A credential helper with a window of its own — Git
Credential Manager on Windows — still shows it.

**A failed authentication offers the terminal.** A call that fails because it
could not authenticate — git's standard error read for the few sentences git
and ssh use — offers *Run again in the terminal*, which hands the terminal to
`git` the way P20.1 hands it to an editor on Unix, with the prompts on, and
takes it back when git ends. On Windows it is the hand-over P25.1 tried, or a
console window of its own if that failed.

**The defaults run on `Enter`; the options are in the same dialog.** `F5` and
`Shift+F5` open a dialog with the defaults chosen, as `F5` in a files tab opens
one with the target filled in, so the usual case is the key and `Enter`.

**A list is a popup that changes what it lists.** Branches, tags and remotes
are each one popup, built as the favourites popup is: `Enter` acts on the row,
`Ins` adds, `F4` changes, `Del` removes after asking, and the popup reads its
list again after each change.

- **P28.1 Run a network call in the background** — the progress window over a
  git process, its last progress line, stopping it, the environment above,
  telling an authentication failure from any other, and *Run again in the
  terminal*. *Done when:* the environment and the failure sentences are
  proved, stopping leaves no process behind, and the hand-over is tried by
  hand against a repository that needs a passphrase, on Windows and under WSL.
- **P28.2 Fetch with F5** — the remote of the current branch's upstream, or
  `origin`, without pruning; *All remotes*, *Prune* and *Tags* as options.
  *Done when:* the arguments of each option and of a branch without an
  upstream are proved, and a fetch from a local bare repository is tried in
  `Integration/Disk`.
- **P28.3 Push with Shift+F5** — the current branch to its upstream; a branch
  without one fails with git's words. *Force with lease* and *Tags* as
  options; a plain force is not offered. *Done when:* the arguments, the
  failure without an upstream and a push to a local bare repository are
  proved.
- **P28.4 Fast-forward with Shift+F9** — the current branch to its upstream
  through `git merge --ff-only @{u}`; *Another branch* picks one from the list
  and runs `git fetch . <upstream>:<branch>`, which moves it without a
  checkout and refuses what is not a fast-forward. Neither touches the
  network. *Done when:* both, and a refusal, are proved on a repository.
- **P28.5 Manage branches with F6** — local and remote branches in two
  sections, with the upstream and how far ahead and behind each one is; `Ins`
  creates one from the row under the cursor or from `HEAD`, `F4` renames,
  `Del` deletes and asks again with `-D` when the branch is not merged. *Done
  when:* the parser of `for-each-ref`, each change and the second question are
  proved, and the popup is driven through a real `Application`.
- **P28.6 Switch branches with F6** — `Enter` on a local branch runs
  `git switch`, on a remote one `git switch --track`. A dirty work tree asks
  first: *Stash and switch*, *Switch carrying the changes*, *Cancel*; a stash
  made this way is kept and named in the message. *Done when:* each answer's
  calls are proved, and a switch git refuses keeps the work tree as it was.
- **P28.7 Manage tags with Shift+F6** — the tags with what they point at and
  whether they are annotated; `Ins` creates one at `HEAD` or a typed revision,
  lightweight or with a message; `Del` deletes it locally and offers to delete
  it on the remote. *Done when:* each change is proved, and the popup is
  driven.
- **P28.8 Manage remotes with Shift+F4** — each remote with its fetch and push
  URL; `Ins` adds, `F4` renames or changes the URL, `Del` removes. *Done when:*
  each change is proved, and the popup is driven.
- **P28.9 Clone with F9 outside a repository** — the question of P26.9 gains
  *Clone*: the URL, in a field that remembers, the directory named after it as
  git names it, a branch, a depth and *Submodules too*. A directory that
  exists and is not empty is refused before anything runs. The panel lands on
  the new directory and its repository tab opens. *Done when:* the name
  derived from each form of URL, the refusal and a clone of a local
  repository are proved.

Every task updates [the user guide](../../docs/user-guide/README.md),
[What works today](../Progress/WHAT-WORKS.md), [Git](../Architecture/GIT.md),
[UI and input](../Architecture/UI-AND-INPUT.md) and
[Cross-platform](../Architecture/CROSS-PLATFORM.md) for what it adds.

## Left for a later phase

- Setting the upstream on the first push.
- Answering git's and ssh's prompts inside Marbas, through `GIT_ASKPASS` and
  `SSH_ASKPASS` pointing at Marbas itself.
- `pull`, which is left out on purpose: `F5` and then `Shift+F9` do the same in
  two steps that can each be seen.
