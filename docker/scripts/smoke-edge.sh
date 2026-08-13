#!/bin/bash
set -euo pipefail

# What the edge has to answer, in one place.
#
# The same six assertions run in two situations that must not drift apart: the
# pipeline, against the pair it builds before publishing an image, and an
# operator, against the public address after watchtower has replaced the
# containers. Written out twice, they were one edit away from a pipeline that
# checks something the operator does not - which is the shape of a check that
# passes forever while the thing it names is broken.
#
# Usage:
#   smoke-edge.sh [BASE_URL] [USER] [PASSWORD]
#
# BASE_URL defaults to the local edge. The credentials are the ones the stand
# publishes behind; omit them where the site is public, and the two assertions
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

# Liveness of the API itself, reachable without the stand credentials: this is
# what an external heartbeat polls, and it was answered by the SPA fallback with
# 200 before it had a location of its own.
expect "/_health" 200 "$BASE_URL/_health"

# Readiness, which is the one an external watcher is told to use: liveness
# answers green from a process that reaches neither database.
expect "/_ready" 200 "$BASE_URL/_ready"

# The edge answering for itself, so a green /_health cannot be the proxy's own.
expect "/nginx-health" 200 "$BASE_URL/nginx-health"

if [ -z "$SMOKE_USER" ]; then
    echo "no credentials given: the two assertions about the closed stand are skipped"
    exit 0
fi

# Everything else is behind basic auth, the SPA and the API alike - and it is
# closed at the edge rather than in the application, so both halves are asserted.
expect "/ without credentials" 401 "$BASE_URL/"
expect "/v1/boards without credentials" 401 "$BASE_URL/v1/boards"
expect "/ with credentials" 200 -u "$SMOKE_USER:$SMOKE_PASSWORD" "$BASE_URL/"
expect "/v1/boards with credentials" 200 -u "$SMOKE_USER:$SMOKE_PASSWORD" "$BASE_URL/v1/boards"
