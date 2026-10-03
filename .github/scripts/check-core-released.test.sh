#!/usr/bin/env bash
# Unit tests for check-core-released.sh — pure git, no network.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNDER_TEST="$SCRIPT_DIR/check-core-released.sh"

fail=0
assert_status() { # desc expected-status actual-status
  if [ "$2" = "$3" ]; then
    echo "ok   - $1"
  else
    echo "FAIL - $1 (expected exit $2, got $3)"
    fail=1
  fi
}

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
git -C "$TMP" init -q
git -C "$TMP" config user.email t@t.t
git -C "$TMP" config user.name t
mkdir -p "$TMP/src"
printf '<Project>\n  <PropertyGroup>\n    <Version>2.12.0</Version>\n  </PropertyGroup>\n</Project>\n' > "$TMP/src/HmacManager.csproj"
echo "code" > "$TMP/src/Code.cs"
echo "readme" > "$TMP/src/README.md"
git -C "$TMP" add -A
git -C "$TMP" commit -q -m init

run() { ( cd "$TMP" && "$UNDER_TEST" >/dev/null 2>&1 ) && echo 0 || echo 1; }

assert_status "the version in the csproj has no nuget tag -> fails" 1 "$(run)"

git -C "$TMP" tag nuget/v2.12.0
assert_status "tagged, src/ unchanged since -> passes" 0 "$(run)"

echo "readme, revised" > "$TMP/src/README.md"
git -C "$TMP" commit -q -am "docs: readme only"
assert_status "only src/README.md changed since the tag -> passes" 0 "$(run)"

echo "new code" > "$TMP/src/Code.cs"
git -C "$TMP" commit -q -am "feat: unreleased"
assert_status "src/ code changed since the tag -> fails" 1 "$(run)"

exit $fail
