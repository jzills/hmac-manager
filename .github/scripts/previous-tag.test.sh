#!/usr/bin/env bash
# Unit tests for previous-tag.sh — pure git, no network, no gh.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
UNDER_TEST="$SCRIPT_DIR/previous-tag.sh"

fail=0
assert_eq() { # desc expected actual
  if [ "$2" = "$3" ]; then
    echo "ok   - $1"
  else
    echo "FAIL - $1 (expected='$2' actual='$3')"
    fail=1
  fi
}

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT
git -C "$TMP" init -q
git -C "$TMP" config user.email t@t.t
git -C "$TMP" config user.name t
git -C "$TMP" commit -q --allow-empty -m init
for tag in nuget/v2.6.0 nuget/v2.7.0 nuget/v2.10.0 service/v1.2.0 chart/v0.2.0 chart/v0.3.0; do
  git -C "$TMP" tag "$tag"
done

run() { ( cd "$TMP" && "$UNDER_TEST" "$1" ); }

assert_eq "nuget/v2.10.0 -> prior nuget by semver (2.10 > 2.7)" "nuget/v2.7.0" "$(run nuget/v2.10.0)"
assert_eq "nuget/v2.7.0 -> prior nuget"                          "nuget/v2.6.0" "$(run nuget/v2.7.0)"
assert_eq "chart/v0.3.0 -> prior chart, not cross-stream"        "chart/v0.2.0" "$(run chart/v0.3.0)"
assert_eq "nuget/v2.6.0 is oldest nuget -> empty"                ""             "$(run nuget/v2.6.0)"
assert_eq "service/v1.2.0 is only service release -> empty"      ""             "$(run service/v1.2.0)"

exit $fail
