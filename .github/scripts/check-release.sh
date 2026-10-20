#!/usr/bin/env bash
set -euo pipefail

tag=$1
semver='^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)(-[0-9A-Za-z.-]+)?$'

if [[ ! $tag =~ $semver ]]; then
  echo "Tag '$tag' is not v<major>.<minor>.<patch>[-<pre-release>]." >&2
  exit 1
fi

pre_release=${BASH_REMATCH[4]}
version=${tag#v}

if ! git merge-base --is-ancestor "$tag^{commit}" origin/main; then
  echo "Tag '$tag' points at a commit that is not on main." >&2
  exit 1
fi

bash "$(dirname "$0")/release-notes.sh" "$version" > /dev/null

if [[ -n $pre_release ]]; then
  prerelease=true
else
  prerelease=false
fi

echo "Tag '$tag' releases $version (pre-release: $prerelease)."
echo "prerelease=$prerelease" >> "${GITHUB_OUTPUT:-/dev/null}"
