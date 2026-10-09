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
mkdir -p "$TMP/src/HmacManager" "$TMP/src/HmacManager.StackExchangeRedis"
printf '<Project>\n  <PropertyGroup>\n    <Version>2.12.0</Version>\n  </PropertyGroup>\n</Project>\n' > "$TMP/src/HmacManager/HmacManager.csproj"
echo "code" > "$TMP/src/HmacManager/Code.cs"
echo "package" > "$TMP/src/HmacManager.StackExchangeRedis/Package.cs"
echo "readme" > "$TMP/src/HmacManager/README.md"
git -C "$TMP" add -A
git -C "$TMP" commit -q -m init

run() { ( cd "$TMP" && "$UNDER_TEST" >/dev/null 2>&1 ) && echo 0 || echo 1; }

assert_status "the version in the csproj has no nuget tag -> fails" 1 "$(run)"

git -C "$TMP" tag nuget/v2.12.0
assert_status "tagged, src/ unchanged since -> passes" 0 "$(run)"

echo "readme, revised" > "$TMP/src/HmacManager/README.md"
git -C "$TMP" commit -q -am "docs: readme only"
assert_status "only the library's README changed since the tag -> passes" 0 "$(run)"

echo "package, revised" > "$TMP/src/HmacManager.StackExchangeRedis/Package.cs"
git -C "$TMP" commit -q -am "feat: the redis package itself"
assert_status "only the Redis package changed since the tag -> passes" 0 "$(run)"

echo "new code" > "$TMP/src/HmacManager/Code.cs"
git -C "$TMP" commit -q -am "feat: unreleased"
assert_status "library code changed since the tag -> fails" 1 "$(run)"

# A release tagged before the library moved into src/HmacManager/ has no such directory to compare.
OLD="$(mktemp -d)"
trap 'rm -rf "$TMP" "$OLD"' EXIT
git -C "$OLD" init -q
git -C "$OLD" config user.email t@t.t
git -C "$OLD" config user.name t
printf '<Project>\n  <PropertyGroup>\n    <Version>2.12.0</Version>\n  </PropertyGroup>\n</Project>\n' > "$OLD/HmacManager.csproj"
git -C "$OLD" add -A
git -C "$OLD" commit -q -m "the old layout"
git -C "$OLD" tag nuget/v2.12.0
mkdir -p "$OLD/src/HmacManager"
git -C "$OLD" mv HmacManager.csproj src/HmacManager/HmacManager.csproj
git -C "$OLD" commit -q -m "the new layout"
old_layout="$( ( cd "$OLD" && "$UNDER_TEST" >/dev/null 2>&1 ) && echo 0 || echo 1)"
assert_status "the tag predates src/HmacManager/ -> fails" 1 "$old_layout"
old_message="$( ( cd "$OLD" && "$UNDER_TEST" 2>&1 ) || true)"
case "$old_message" in
  *"predates"*) echo "ok   - and says the tag predates the directory" ;;
  *) echo "FAIL - the message does not say the tag predates the directory: $old_message"; fail=1 ;;
esac

exit $fail
