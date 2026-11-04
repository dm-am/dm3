#!/usr/bin/env bash
# Fails when the monitoring of the deployment would not do what it is written to do: a
# configuration Prometheus cannot read, a rule that does not fire on the state it was
# written for, an alert that reaches no receiver, or a receiver that does not start.
#
# All four and not just the parse, because promtool is happy with rules that go nowhere
# (rule_files with no alerting section and no alertmanager to name in one), and an
# expression that reads the wrong series parses as well as the right one.
set -euo pipefail

CONTAINER="alert-receiver-gate"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/common.sh"
cd "$ROOT"

# The images the stack runs, read from the compose file: the answer has to be about what is
# deployed, not about an older release of the tools.
PROMETHEUS_IMAGE="$(grep -oE 'prom/prometheus:[A-Za-z0-9.-]+' docker/docker-compose.yml | head -1)"
ALERTMANAGER_IMAGE="$(grep -oE 'prom/alertmanager:[A-Za-z0-9.-]+' docker/docker-compose.yml | head -1)"
test -n "$PROMETHEUS_IMAGE" || fail "docker-compose.yml names no prom/prometheus image"
test -n "$ALERTMANAGER_IMAGE" || fail "docker-compose.yml names no prom/alertmanager image"

# MSYS_NO_PATHCONV: Git Bash would otherwise rewrite the container side of -v and every
# path handed to promtool into a directory under the Git installation (host() hands docker
# the native path).
export MSYS_NO_PATHCONV=1

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
