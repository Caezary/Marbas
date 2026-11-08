# Security

## Supported versions

Only the latest release receives fixes. Marbas is before 1.0, so a fix ships
as the next release rather than as a patch to an older one.

## Reporting a vulnerability

Report it through a
[private advisory](https://github.com/Caezary/Marbas/security/advisories/new),
not in a public issue or pull request. Without a GitHub account, write to
marbas.tui@gmail.com instead. Include:

- the version (`Marbas --version`), the system and the terminal;
- what an attacker controls — an archive, a file name, a file's contents, a
  directory on a shared disk — and what it makes Marbas do;
- the steps, or a file that reproduces it.

The advisory stays private until a release carries the fix, and then names the
reporter unless asked otherwise.

## What counts

Marbas reads archives, file names and file contents made by someone else, and
runs commands on them. Examples of what to report:

- an archive that writes outside the target directory when unpacked, or
  replaces a file through a link inside it;
- a file name that runs a command, or adds an argument, through an
  association, the editor or the user menu;
- a copy, move or delete that follows a link out of the entries it was given;
- a file that crashes or hangs the viewer or the search, beyond being slow on
  a large input;
- the working directory under the temporary directory readable or writable by
  another user.

A command the user wrote into the user menu or `Setup` runs with the user's
rights by design. A flaw in Terminal.Gui, .NET or an archive library belongs
upstream; report it here as well when Marbas exposes it.
