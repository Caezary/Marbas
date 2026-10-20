#!/usr/bin/env bash
set -euo pipefail

tag=$1
version=${tag#v}
repository=${GITHUB_REPOSITORY:-Caezary/Marbas}
changelog="$(dirname "$0")/../../CHANGELOG.md"

if [[ ! -f $changelog ]]; then
  echo "CHANGELOG.md not found." >&2
  exit 1
fi

awk -v version="$version" '
  /^## / {
    if (found) exit
    if ($2 == version) { found = 1; next }
  }
  found && NF == 0 { if (printed) blanks++; next }
  found {
    while (blanks > 0) { print ""; blanks-- }
    print
    printed = 1
  }
  END { exit !found }
' "$changelog" || {
  echo "CHANGELOG.md has no section '## $version'." >&2
  exit 1
}

previous=$(git describe --tags --abbrev=0 --match 'v*' --exclude 'v*-*' "$tag^" 2>/dev/null || true)

if [[ -n $previous ]]; then
  echo
  echo "**Full changelog**: https://github.com/$repository/compare/$previous...$tag"
fi
