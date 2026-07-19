#!/usr/bin/env bash
# previous-tag.sh <TAG>
#
# Prints the release tag immediately preceding <TAG> within the SAME prefix
# stream (e.g. the prior `nuget/v*` for a `nuget/v*` tag), or an empty string
# when <TAG> is the first release of its prefix. Used to scope auto-generated
# release changelogs so one artifact's notes are not polluted by another's.
#
# Requires the repository's tags to be present locally (checkout fetch-depth: 0).
set -euo pipefail

TAG="${1:?usage: previous-tag.sh <TAG>}"

# Prefix = the stream name before "/v" (e.g. "nuget/v2.7.0" -> "nuget").
PREFIX="${TAG%/v*}"

prev=""
seen_current=0
while IFS= read -r t; do
  [ -z "$t" ] && continue
  if [ "$seen_current" -eq 1 ]; then
    prev="$t"
    break
  fi
  [ "$t" = "$TAG" ] && seen_current=1
done < <(git tag --list "${PREFIX}/v*" --sort=-v:refname)

printf '%s\n' "$prev"
