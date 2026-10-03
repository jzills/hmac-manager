#!/usr/bin/env bash
# check-core-released.sh
#
# Run before publishing HmacManager.StackExchangeRedis. The package references src/ as a project,
# and packing turns that reference into a NuGet dependency on HmacManager at the version in
# src/HmacManager.csproj. Fails unless that version has been released (its nuget/v tag exists)
# and src/ is still what was released under it.
#
# Without this the package can be tested against src/ code that no published HmacManager contains:
# it would declare a dependency on the previous release, and fail with a MissingMethodException for
# whoever installs it. Release HmacManager first, then the package.
#
# Requires the repository's tags to be present locally (checkout fetch-depth: 0).
set -euo pipefail

CSPROJ="${1:-src/HmacManager.csproj}"

version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$CSPROJ" | head -n 1)"
if [ -z "$version" ]; then
  echo "::error::No <Version> in $CSPROJ." >&2
  exit 1
fi

tag="nuget/v$version"
if ! git rev-parse -q --verify "refs/tags/$tag" >/dev/null; then
  echo "::error::HmacManager $version ($CSPROJ) has not been released: there is no $tag tag. Release HmacManager first." >&2
  exit 1
fi

# The README is excluded: it is rendered on nuget.org, and changes to it ship nothing a package can call.
pathspec=(src ':(exclude)src/README.md')
if ! git diff --quiet "$tag" HEAD -- "${pathspec[@]}"; then
  echo "::error::src/ has changed since $tag, so the HmacManager this package would depend on is not the one it was tested against. Release HmacManager first." >&2
  git diff --stat "$tag" HEAD -- "${pathspec[@]}" >&2
  exit 1
fi

echo "HmacManager $version is released as $tag, and src/ matches it."
