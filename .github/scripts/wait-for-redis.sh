#!/usr/bin/env bash
# wait-for-redis.sh [HOST] [PORT] [SECONDS]
#
# Waits until Redis answers PING, so a suite that connects in a one-time setup does not run while
# the container is still starting: there, one refused connection fails every test in the fixture.
# Speaks RESP over bash's /dev/tcp, so the runner needs no redis-cli.
set -euo pipefail

HOST="${1:-127.0.0.1}"
PORT="${2:-6379}"
SECONDS_TO_WAIT="${3:-30}"

for _ in $(seq 1 "$SECONDS_TO_WAIT"); do
  # A subshell, because a failed exec redirection would otherwise end this script.
  if (
    exec 3<>"/dev/tcp/$HOST/$PORT" &&
      printf 'PING\r\n' >&3 &&
      read -r -t 2 reply <&3 &&
      [[ "$reply" == +PONG* ]]
  ) 2>/dev/null; then
    echo "Redis at $HOST:$PORT answered PING."
    exit 0
  fi
  sleep 1
done

echo "::error::Redis at $HOST:$PORT did not answer PING within $SECONDS_TO_WAIT seconds." >&2
exit 1
