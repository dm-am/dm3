#!/usr/bin/env bash
# Fails when the monitoring of the deployment would not do what it is written to
# do: a configuration Prometheus cannot read, a rule that does not fire on the
# state it was written for, an alert that reaches no receiver, or a receiver that
# does not start.
#
# Why all four and not just the parse: promtool is happy with rules that go
# nowhere, and that is what this repository ran - rule_files with no alerting
# section and no alertmanager to name in one. Every rule reached a firing state
# inside Prometheus and stayed there, so an outage was found by opening /alerts
# through a tunnel. An expression that reads the wrong series parses exactly as
# well as the right one, too: the backlog rule counted only the messages still
# waiting, so a worker holding a thousand of them read as an empty queue.
#
# Why a script and not the commands in the workflow: they ran in the
# compose-topology job and nowhere else. The workflow, scripts/gates.sh and
# scripts/hooks/pre-push all call this now, so there is one command and one
# version of both images.
set -euo pipefail

PROMETHEUS_IMAGE="prom/prometheus:v2.51.0"
ALERTMANAGER_IMAGE="prom/alertmanager:v0.27.0"
CONTAINER="alert-receiver-gate"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

# Git Bash rewrites anything that looks like a Unix path in an argument before
# the Windows docker client sees it, which turns the container side of -v and
# every path handed to promtool into a directory under the Git installation. The
# variable switches that off, and host() hands docker the native path it wants.
# On Linux and macOS cygpath does not exist and the value passes through.
export MSYS_NO_PATHCONV=1
host() {
  if command -v cygpath > /dev/null 2>&1; then
    cygpath -m "$1"
  else
    printf '%s' "$1"
  fi
}

fail() {
  echo "ERROR: $1" >&2
  exit 1
}

echo "the Prometheus configuration and its alert rules parse"
docker run --rm \
  -v "$(host "$ROOT/docker/prometheus.yml"):/etc/prometheus/prometheus.yml:ro" \
  -v "$(host "$ROOT/docker/prometheus/alerts.yml"):/etc/prometheus/alerts.yml:ro" \
  --entrypoint promtool "$PROMETHEUS_IMAGE" \
  check config /etc/prometheus/prometheus.yml

echo "the alert rules fire on what they are written for"
docker run --rm \
  -v "$(host "$ROOT/docker/prometheus"):/rules:ro" \
  -w /rules \
  --entrypoint promtool "$PROMETHEUS_IMAGE" \
  test rules alerts_test.yml

echo "every alert rule reaches a receiver the stack runs"
grep -q '^rule_files:' docker/prometheus.yml ||
  fail "prometheus.yml evaluates no rules of its own any more - this check is about the ones it does"

# The alerting section only, up to the next key at column zero: a "targets:" line
# taken from the whole file would match a scrape job and pass on a configuration
# that delivers nothing.
# `|| true` because the script runs under `set -e` and the last grep exits 1 when
# there is no alerting section at all - which is the very case the message below
# exists for. Without it the script died on the assignment, printed nothing, and
# left whoever broke it an exit code to guess from.
targets=$(sed -n '/^alerting:/,/^[^[:space:]#]/p' docker/prometheus.yml |
  grep -oE "targets:[[:space:]]*\[[^]]*\]" | grep -oE "[a-z0-9.-]+:[0-9]+" || true)
test -n "$targets" || fail "prometheus.yml declares rule_files and no alertmanager to post them to"

for target in $targets; do
  echo "  alert target: $target"
  grep -q "container_name: '${target%%:*}'" docker/docker-compose.yml ||
    fail "nothing in the stack answers to ${target%%:*}, so every notification is a connection refused"
done

echo "the alert receiver renders its configuration and starts"
# The one thing no test in the solution can do: start it. The configuration is
# rendered from the environment at container start, so a template that cannot
# produce a file alertmanager accepts is a receiver that is not there - and
# nothing else in the pipeline runs this container.
docker run --rm \
  -v "$(host "$ROOT/docker/prometheus/alertmanager.yml"):/etc/alertmanager/alertmanager.yml:ro" \
  -v "$(host "$ROOT/docker/alertmanager-init.sh"):/alertmanager-init.sh:ro" \
  --entrypoint /bin/sh "$ALERTMANAGER_IMAGE" \
  -c '/alertmanager-init.sh --render-only && amtool check-config /tmp/alertmanager.yml'

# A run interrupted between the start and the removal below leaves the name
# taken, and `docker run --name` then fails for a reason that has nothing to do
# with the configuration under test. CI gets a fresh machine and never meets it.
docker rm -f "$CONTAINER" > /dev/null 2>&1 || true

docker run -d --name "$CONTAINER" \
  -v "$(host "$ROOT/docker/prometheus/alertmanager.yml"):/etc/alertmanager/alertmanager.yml:ro" \
  -v "$(host "$ROOT/docker/alertmanager-init.sh"):/alertmanager-init.sh:ro" \
  --entrypoint /bin/sh "$ALERTMANAGER_IMAGE" /alertmanager-init.sh > /dev/null

ready=0
for _ in $(seq 1 20); do
  if docker exec "$CONTAINER" wget --spider -q http://localhost:9093/-/healthy; then
    ready=1
    break
  fi
  sleep 1
done

# Only when it did not come up: the logs of a receiver that answered its health
# probe say nothing the line below does not, and they bury the output above.
if [ "$ready" != 1 ]; then
  docker logs "$CONTAINER" || true
fi
docker rm -f "$CONTAINER" > /dev/null
test "$ready" = 1 || fail "the alert receiver does not start with the configuration in this repository"

echo "OK: the monitoring of the deployment parses, fires and delivers."
