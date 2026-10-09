#!/usr/bin/env bash
# check-core-released.sh
#
# Run before publishing HmacManager.StackExchangeRedis. The package references src/HmacManager as a
# project, and packing turns that reference into a NuGet dependency on HmacManager at the version in
# src/HmacManager/HmacManager.csproj. Fails unless that version has been released (its nuget/v tag
# exists) and src/HmacManager/ is still what was released under it. Changes elsewhere, the Redis
# package's own included, do not count.
#
# Without this the package can be tested against library code that no published HmacManager contains:
# it would declare a dependency on the previous release, and fail with a MissingMethodException for
# whoever installs it. Release HmacManager first, then the package.
#
# Requires the repository's tags to be present locally (checkout fetch-depth: 0).
set -euo pipefail

CSPROJ="${1:-src/HmacManager/HmacManager.csproj}"
LIBRARY="$(dirname "$CSPROJ")"

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

# Releases from before the library moved into its own directory have nothing at this path to compare.
if ! git cat-file -e "$tag:$LIBRARY" 2>/dev/null; then
  echo "::error::$tag predates $LIBRARY/, so it cannot be the HmacManager this package was built against. Release HmacManager first." >&2
  exit 1
fi

# The README is excluded: it is rendered on nuget.org, and changes to it ship nothing a package can call.
pathspec=("$LIBRARY" ":(exclude)$LIBRARY/README.md")
if ! git diff --quiet "$tag" HEAD -- "${pathspec[@]}"; then
  echo "::error::$LIBRARY/ has changed since $tag, so the HmacManager this package would depend on is not the one it was tested against. Release HmacManager first." >&2
  git diff --stat "$tag" HEAD -- "${pathspec[@]}" >&2
  exit 1
fi

echo "HmacManager $version is released as $tag, and $LIBRARY/ matches it."
