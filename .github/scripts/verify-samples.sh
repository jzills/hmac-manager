#!/usr/bin/env bash
#
# Builds every sample and runs the two that can assert on themselves end to end.
#
# This exists because nothing in CI referenced samples/ at all, and it showed:
# the JavaScript client sample depended on a tarball that was gitignored and had
# never been committed, so `npm install` failed on any clean checkout; one sample
# was pinned to a released package four minor versions behind the source it sits
# next to; and the run command in every README named a directory that held no
# project. All three are the kind of break that only a run catches.
#
# Usable locally: bash .github/scripts/verify-samples.sh

set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SAMPLES="$REPO_ROOT/samples"
LOGS="$(mktemp -d)"

PIDS=()

cleanup() {
    for pid in ${PIDS+"${PIDS[@]}"}; do
        kill "$pid" 2>/dev/null || true
    done
}
trap cleanup EXIT

fail() {
    echo "FAIL: $*" >&2
    echo "--- logs ---" >&2
    for log in "$LOGS"/*; do
        [ -f "$log" ] || continue
        echo "== $log" >&2
        cat "$log" >&2
    done
    exit 1
}

start() {
    # start <log-name> <command...>
    local name=$1
    shift
    "$@" > "$LOGS/$name.log" 2>&1 &
    PIDS+=($!)
}

wait_for() {
    # wait_for <url> — any HTTP response will do, including 401
    local url=$1
    for _ in $(seq 1 60); do
        if curl -sS -o /dev/null "$url" 2>/dev/null; then
            return 0
        fi
        sleep 1
    done
    fail "timed out waiting for $url"
}

expect() {
    # expect <file> <needle>
    grep -qF -- "$2" "$1" || fail "expected '$2' in $1"
}

echo "==> Building every sample project"
while IFS= read -r project; do
    echo "    $project"
    dotnet build "$project" --verbosity quiet --nologo
done < <(find "$SAMPLES" -name "*.csproj" | sort)

echo "==> Building the TypeScript client the Node samples link to"
(cd "$REPO_ROOT/client/lib" && npm ci --silent && npm run build)

# ---------------------------------------------------------------------------
# Node API sample: a Node API verifying, called by a Node client and a .NET one.
# ---------------------------------------------------------------------------
echo "==> Node API sample"
NODE_SAMPLE="$SAMPLES/NodeApiAuthentication"

# npm install, not ci — the dependency is a file: link and no lockfile is committed.
(cd "$NODE_SAMPLE/Api" && npm install --silent --no-audit --no-fund)
(cd "$NODE_SAMPLE/NodeClient" && npm install --silent --no-audit --no-fund)

start node-api node "$NODE_SAMPLE/Api/server.js"
wait_for http://localhost:5200/api/weatherforecast

(cd "$NODE_SAMPLE/NodeClient" && node index.js) > "$LOGS/node-client.log" 2>&1 ||
    fail "the Node client exited non-zero"

expect "$LOGS/node-client.log" "GET : 200"
expect "$LOGS/node-client.log" "POST: 200"
# The two properties the sample exists to demonstrate.
expect "$LOGS/node-client.log" "replay 2: 401"
expect "$LOGS/node-client.log" "tampered: 401"

dotnet run --project "$NODE_SAMPLE/DotnetClient" > "$LOGS/dotnet-client.log" 2>&1 ||
    fail "the .NET client exited non-zero"

# The crossing that only exists because both implementations build the same
# signing content: a request signed by the .NET library, verified in Node.
expect "$LOGS/dotnet-client.log" "GET : 200"
expect "$LOGS/dotnet-client.log" "POST: 200"

expect "$LOGS/node-api.log" "rejected: replayed"
expect "$LOGS/node-api.log" "rejected: signature-mismatch"

echo "    ok"

# ---------------------------------------------------------------------------
# JavaScript client sample: the npm package signing against the .NET API.
# ---------------------------------------------------------------------------
echo "==> JavaScript client sample"
JS_SAMPLE="$SAMPLES/WebToApiAuthenticationWithJavaScriptClient"

(cd "$JS_SAMPLE/Client" && npm install --silent --no-audit --no-fund)

start js-api dotnet run --project "$JS_SAMPLE/Api"
wait_for http://localhost:5140/api/weatherforecast

(cd "$JS_SAMPLE/Client" && node index.js) > "$LOGS/js-client.log" 2>&1 ||
    fail "the JavaScript client exited non-zero"

expect "$LOGS/js-client.log" "GET : 200"
expect "$LOGS/js-client.log" "POST: 200"
expect "$LOGS/js-client.log" "(tampered): 401"

echo "    ok"
echo "==> All samples built; both Node samples round-tripped"
