#!/bin/bash
set -euo pipefail

# What the edge has to answer, in one place. The same assertions run in the pipeline,
# against the pair it builds before publishing an image, and by an operator against the
# public address after watchtower replaced the containers; written twice they would drift.
#
# Usage:
#   smoke-edge.sh [BASE_URL] [USER] [PASSWORD]
#
# BASE_URL defaults to the local edge. The credentials are the ones the stand
# publishes behind; omit them where the site is public, and the assertions
# that need them are skipped rather than failed.

BASE_URL="${1:-${SMOKE_BASE_URL:-http://localhost}}"
SMOKE_USER="${2:-${SMOKE_USER:-}}"
SMOKE_PASSWORD="${3:-${SMOKE_PASSWORD:-}}"

status() { curl -s -o /dev/null -w '%{http_code}' "$@"; }

expect() {
    local what="$1" want="$2"
    shift 2
    local got
    got="$(status "$@")"
    if [ "$got" != "$want" ]; then
        echo "FAIL: $what answered $got, expected $want" >&2
        exit 1
    fi
    echo "ok: $what -> $got"
}

# Liveness of the API itself, reachable without the stand credentials: an external
# heartbeat polls it, and the SPA fallback used to answer it with 200.
expect "/_health" 200 "$BASE_URL/_health"

# Readiness, the one an external watcher should use: liveness is green from a process
# that reaches neither database.
expect "/_ready" 200 "$BASE_URL/_ready"

# The edge answering for itself, so a green /_health cannot be the proxy's own.
expect "/nginx-health" 200 "$BASE_URL/nginx-health"

if [ -z "$SMOKE_USER" ]; then
    echo "no credentials given: the assertions about the closed stand are skipped"
    exit 0
fi

# Everything else is closed at the edge, not in the application, so both halves are asserted.
expect "/ without credentials" 401 "$BASE_URL/"
expect "/v1/boards without credentials" 401 "$BASE_URL/v1/boards"
expect "/ with credentials" 200 -u "$SMOKE_USER:$SMOKE_PASSWORD" "$BASE_URL/"
expect "/v1/boards with credentials" 200 -u "$SMOKE_USER:$SMOKE_PASSWORD" "$BASE_URL/v1/boards"
