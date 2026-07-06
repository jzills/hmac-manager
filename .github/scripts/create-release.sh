#!/usr/bin/env bash
# create-release.sh <TAG> <TITLE>
#
# Creates a GitHub Release for an already-pushed tag. Install-info markdown is
# read from stdin; a changelog scoped to the previous same-prefix tag is
# appended automatically. Idempotent: if the release already exists it is left
# untouched.
#
# Requires: gh authenticated via GH_TOKEN; env GITHUB_REPOSITORY; job permission
# `contents: write`; checkout `fetch-depth: 0` (so previous-tag.sh sees tags).
# jq is preinstalled on GitHub-hosted runners.
set -euo pipefail

TAG="${1:?usage: create-release.sh <TAG> <TITLE>}"
TITLE="${2:?usage: create-release.sh <TAG> <TITLE>}"
install_body="$(cat)"

if gh release view "$TAG" >/dev/null 2>&1; then
  echo "Release '$TAG' already exists — skipping."
  exit 0
fi

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
prev_tag="$("$script_dir/previous-tag.sh" "$TAG")"

gen_args=(--method POST "repos/${GITHUB_REPOSITORY}/releases/generate-notes" -f "tag_name=${TAG}")
if [ -n "$prev_tag" ]; then
  gen_args+=(-f "previous_tag_name=${prev_tag}")
fi
changelog="$(gh api "${gen_args[@]}" | jq -r '.body')"

notes="$(printf '%s\n\n%s\n' "$install_body" "$changelog")"

printf '%s' "$notes" | gh release create "$TAG" \
  --title "$TITLE" \
  --notes-file - \
  --latest \
  --verify-tag

echo "Created release '$TAG'."
