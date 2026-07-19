#!/usr/bin/env bash
# Tests create-release.sh with a stubbed `gh` — no network.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

fail=0
assert_contains() { # desc haystack needle
  if printf '%s' "$2" | grep -qF -- "$3"; then
    echo "ok   - $1"
  else
    echo "FAIL - $1 (missing: $3)"
    fail=1
  fi
}
assert_absent() { # desc path
  if [ ! -e "$2" ]; then echo "ok   - $1"; else echo "FAIL - $1 (exists: $2)"; fail=1; fi
}

TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

# Throwaway repo so previous-tag.sh has real tags to read.
REPO="$TMP/repo"
mkdir -p "$REPO/.github/scripts"
git -C "$REPO" init -q
git -C "$REPO" config user.email t@t.t
git -C "$REPO" config user.name t
git -C "$REPO" commit -q --allow-empty -m init
git -C "$REPO" tag nuget/v2.6.0
git -C "$REPO" tag nuget/v2.7.0
cp "$SCRIPT_DIR/create-release.sh" "$SCRIPT_DIR/previous-tag.sh" "$REPO/.github/scripts/"

# Fake gh: records `release create` args + piped notes; toggles existence.
BIN="$TMP/bin"; mkdir -p "$BIN"
cat > "$BIN/gh" <<'GH'
#!/usr/bin/env bash
set -euo pipefail
case "$1 $2" in
  "release view")
    [ "${GH_TEST_RELEASE_EXISTS:-0}" = "1" ] && exit 0 || exit 1 ;;
  "api "*|"api")
    echo '{"body":"## Changes\n* thing by @x in #1\n\n**Full Changelog**: nuget/v2.6.0...nuget/v2.7.0"}' ;;
  "release create")
    printf '%s\n' "$*" > "$GH_TEST_LOG_DIR/create_args"
    cat > "$GH_TEST_LOG_DIR/create_notes" ;;
  *) echo "unexpected gh call: $*" >&2; exit 99 ;;
esac
GH
chmod +x "$BIN/gh"

export PATH="$BIN:$PATH"
export GITHUB_REPOSITORY="jzills/hmac-manager"
export GH_TEST_LOG_DIR="$TMP/log"; mkdir -p "$GH_TEST_LOG_DIR"

# --- Case 1: no existing release -> creates with correct args + notes ---
rm -f "$GH_TEST_LOG_DIR/create_args" "$GH_TEST_LOG_DIR/create_notes"
( cd "$REPO" && GH_TEST_RELEASE_EXISTS=0 \
    bash .github/scripts/create-release.sh "nuget/v2.7.0" "HmacManager (NuGet) v2.7.0" <<'BODY'
**HmacManager (NuGet)** — core library.
Install: `dotnet add package HmacManager --version 2.7.0`
BODY
)
args="$(cat "$GH_TEST_LOG_DIR/create_args" 2>/dev/null || true)"
notes="$(cat "$GH_TEST_LOG_DIR/create_notes" 2>/dev/null || true)"
assert_contains "creates the release tag"    "$args"  "nuget/v2.7.0"
assert_contains "passes the title"           "$args"  "HmacManager (NuGet) v2.7.0"
assert_contains "marks as latest"            "$args"  "--latest"
assert_contains "verifies the tag"           "$args"  "--verify-tag"
assert_contains "notes carry install body"   "$notes" "dotnet add package HmacManager --version 2.7.0"
assert_contains "notes carry the changelog"  "$notes" "Full Changelog"

# --- Case 2: release already exists -> skips, no create call ---
rm -f "$GH_TEST_LOG_DIR/create_args" "$GH_TEST_LOG_DIR/create_notes"
( cd "$REPO" && GH_TEST_RELEASE_EXISTS=1 \
    bash .github/scripts/create-release.sh "nuget/v2.7.0" "HmacManager (NuGet) v2.7.0" <<'BODY'
whatever
BODY
)
assert_absent "existing release untouched (no create call)" "$GH_TEST_LOG_DIR/create_args"

exit $fail
