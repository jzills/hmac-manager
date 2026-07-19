#!/usr/bin/env bash

set -euo pipefail

BRANCH="$1"

case "$BRANCH" in
  release/service/v*)
    PREFIX="service"
    VERSION="${BRANCH#release/service/v}"
    ;;
  release/chart/v*)
    PREFIX="chart"
    VERSION="${BRANCH#release/chart/v}"
    ;;
  release/operator/v*)
    PREFIX="operator"
    VERSION="${BRANCH#release/operator/v}"
    ;;
  release/npm/v*)
    PREFIX="npm"
    VERSION="${BRANCH#release/npm/v}"
    ;;
  release/v*)
    PREFIX="nuget"
    VERSION="${BRANCH#release/v}"
    ;;
  *)
    echo "Unrecognized release branch: $BRANCH (expected release/vX.Y.Z, release/service/vX.Y.Z, release/operator/vX.Y.Z, release/chart/vX.Y.Z, or release/npm/vX.Y.Z)" >&2
    exit 1
    ;;
esac

if ! echo "$VERSION" | grep -qE '^[0-9]+\.[0-9]+\.[0-9]+$'; then
  echo "Invalid version format: $BRANCH" >&2
  exit 1
fi

echo "$PREFIX/v$VERSION"
