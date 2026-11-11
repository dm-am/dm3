#!/usr/bin/env bash
# Fails when the files a server is deployed from stop describing one stand: an overlay that
# no longer parses, a build context pointing at a removed directory, a systemd unit that stops
# going through the release script, a rollout twin that drifted from its primary,
# a connection budget past the server's ceiling. None of that can fail a test in the
# solution, and all of it fails on the server instead.
#
# Failures are plain messages on stderr rather than GitHub's ::error:: annotations: the text
# has to read in a terminal.
#
# shellcheck disable=SC2016 # the node programs below are single-quoted so the shell leaves them be
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/common.sh"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/compose-placeholders.sh"
cd "$ROOT/docker"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

echo "the deployed overlay combination parses"
docker compose --env-file /dev/null -f docker-compose.yml config --quiet
docker compose --env-file /dev/null -f docker-compose.yml -f docker-compose.preview.yml config --quiet
docker compose --env-file /dev/null -f docker-compose.yml -f docker-compose.preview.yml -f docker-compose.pop.yml config --quiet

echo "every build context the overlay names exists"
# Take everything after the key rather than a whitespace-split field: compose
# resolves contexts to absolute paths, and a path may contain spaces.
docker compose --env-file /dev/null -f docker-compose.yml -f docker-compose.preview.yml config \
  | sed -n 's/^[[:space:]]*context:[[:space:]]*//p' | sort -u | while read -r ctx; do
      echo "  build context: $ctx"
      test -d "$ctx" || { echo "ERROR: build context does not exist: $ctx" >&2; exit 1; }
    done

echo "the unit boots through dm3-release, whose one compose line names both files and no profile but rollout"
# The unit used to carry a compose line of its own, which had to agree with the installer's by
# hand. Now there is one line, in the release script, and the unit only names its verbs. A
# profile other than rollout there would start services the releases do not account for.
#
# `|| true` because grep exits 1 on no match and this script runs under pipefail: without it
# a line that is no longer there kills the script on the assignment, which is exactly the case
# the messages below are written for.
grep -qx 'ExecStart=/usr/local/sbin/dm3-release boot' dm3.service ||
  fail "dm3.service does not start the stand through dm3-release boot"
grep -qx 'ExecStop=/usr/local/sbin/dm3-release stop' dm3.service ||
  fail "dm3.service does not stop the stand through dm3-release stop"
if grep -q 'docker compose' dm3.service; then
  fail "dm3.service runs compose itself, a second definition of the stand"
fi
entry_code=$(grep -v '^[[:space:]]*#' scripts/release-entry.sh || true)
compose_lines=$(grep -c 'docker compose' <<< "$entry_code" || true)
test "$compose_lines" = 1 || fail "release-entry.sh calls docker compose on $compose_lines lines, not through one command line"
grep 'docker compose' <<< "$entry_code" |
  grep -qF -- '-f "$dir/docker/docker-compose.yml" -f "$dir/docker/docker-compose.preview.yml"' ||
  fail "the compose line of release-entry.sh does not name the base file and the overlay the site lives in"
profiles=$(grep -oE -- '--profile [a-z]+' <<< "$entry_code" | sort -u || true)
test "$profiles" = "--profile rollout" ||
  fail "release-entry.sh enables profiles other than rollout: $(tr '\n' ' ' <<< "$profiles")"

# The resolved model, as the release reads it. node rather than jq: jq is not on every
# developer machine, node is.
resolved() {
  local out=$1; shift
  docker compose --env-file /dev/null "$@" config --format json > "$WORK/$out.json"
}
PAIR=(-f docker-compose.yml -f docker-compose.preview.yml)
resolved site "${PAIR[@]}" --profile rollout
resolved origin "${PAIR[@]}" -f docker-compose.pop-origin.yml --profile rollout
resolved pop "${PAIR[@]}" -f docker-compose.pop.yml

# Runs a node program against resolved models; it prints one line per broken rule. The
# program arrives through the environment, so neither shell nor node quoting touches it, and
# its own arguments follow the directory of the models.
judge() {
  local program=$1; shift
  local broken
  broken=$(PROGRAM="$program" node -e '
    const fs = require("fs");
    const load = name => JSON.parse(fs.readFileSync(process.argv[1] + "/" + name + ".json", "utf8"));
    const problems = [];
    const expect = (ok, message) => { if (!ok) problems.push(message); };
    eval(process.env.PROGRAM);
    process.stdout.write(problems.join("\n"));
  ' "$(host "$WORK")" "$@")
  if [ -n "$broken" ]; then
    printf 'ERROR: %s\n' "$broken" >&2
    exit 1
  fi
}

echo "the twins of a rollout differ from their primaries only in name, port, profile and the key"
judge '
  const canon = value => Array.isArray(value) ? value.map(canon)
    : value && typeof value === "object"
      ? Object.fromEntries(Object.keys(value).sort().map(key => [key, canon(value[key])]))
      : value;
  const essence = service => {
    const copy = structuredClone(service);
    for (const key of ["container_name", "ports", "profiles"]) delete copy[key];
    if (copy.environment) delete copy.environment.DM_PeriodicServices__Enabled;
    return JSON.stringify(canon(copy));
  };
  for (const model of ["site", "origin"]) {
    const services = load(model).services;
    for (const [primary, twin, name] of [["dmapi", "dmapi-next", "dm-api-next"], ["dmfront", "dmfront-next", "dm-front-next"]]) {
      const p = services[primary], t = services[twin];
      if (!p || !t) { expect(false, `${model}: ${primary} or ${twin} is missing`); continue; }
      expect(essence(p) === essence(t), `${model}: ${twin} differs from ${primary} beyond name, port, profile and the key`);
      expect(t.container_name === name, `${model}: ${twin} is not named ${name}`);
      expect((t.ports ?? []).length === 0, `${model}: ${twin} publishes a port, which collides with ${primary}`);
      expect(JSON.stringify(t.profiles) === "[\"rollout\"]", `${model}: ${twin} is not behind the rollout profile alone`);
      expect(!p.profiles, `${model}: ${primary} sits behind a profile, so a plain up stops starting it`);
    }
    expect(services["dmapi-next"]?.environment?.DM_PeriodicServices__Enabled === "false",
      `${model}: the API twin runs the periodic jobs, which then run twice`);
    expect(!("DM_PeriodicServices__Enabled" in (services.dmapi?.environment ?? {})),
      `${model}: the primary sets the key, and its default is the one that runs the jobs`);
  }
'

echo "a plain up starts no twin"
plain=$(docker compose --env-file /dev/null "${PAIR[@]}" config --services)
if printf '%s\n' "$plain" | grep -qE -- '-next$'; then
  fail "a twin is outside the rollout profile, so every plain up starts a second API or frontend"
fi

echo "the connection pools fit the server's ceiling at the peak of a rollout"
# The figure and what it covers are in docker-compose.yml, beside x-workload-env.
judge '
  const RESERVE = 20;
  const services = load("site").services;
  const pool = name => Number(/MaxPoolSize=(\d+)/.exec(services[name]?.environment?.DM_ConnectionStrings__Rdb ?? "")?.[1] ?? NaN);
  const peak = ["dmapi", "dmapi-next", "dm-mail-worker", "dm-notification-worker"].map(pool).reduce((a, b) => a + b, 0);
  const ceiling = Number(/max_connections=(\d+)/.exec((services.postgres.command ?? []).join(" "))?.[1] ?? NaN);
  expect(Number.isFinite(peak), "a pool of the API, its twin or a worker is not stated");
  expect(Number.isFinite(ceiling), "postgres sets no max_connections, and its default is below the peak");
  expect(peak + RESERVE <= ceiling, `the pools reach ${peak} with ${RESERVE} held back, past max_connections=${ceiling}`);
  expect(pool("migration") <= pool("dmapi-next"),
    "the migration pool is larger than the twin pool it is counted in place of");
  expect((services.migration.environment.DM_ConnectionStrings__Rdb ?? "").includes("Options=-c lock_timeout=5s"),
    "the migration waits on any lock the running API holds, and every request queues behind it");
'

echo "the API, its twin and both workers stop with a grace longer than the host's"
judge '
  const services = load("site").services;
  for (const name of ["dmapi", "dmapi-next", "dm-mail-worker", "dm-notification-worker"])
    expect(services[name]?.stop_grace_period === "35s", `${name} is killed before its requests in flight end`);
'

echo "the edge publishes the site only, links the primaries itself, and not at the point of presence"
judge '
  const edge = load("site").services.nginx;
  const published = (edge.ports ?? []).map(port => String(port.published)).sort();
  expect(JSON.stringify(published) === "[\"443\",\"80\"]", `the edge publishes ${published}, not 80 and 443 alone`);
  const command = (edge.command ?? []).join(" ");
  expect(command.includes("upstreams/api-primary.conf") && command.includes("upstreams/front-primary.conf"),
    "the edge does not link the primaries at start, so a restart keeps whatever a rollout left");
  expect(load("pop").services.nginx.command == null,
    "the point of presence inherits the edge command and skips rendering its own template");
'

echo "the edge's machine files follow DM_EDGE_DIR, and docker/nginx without it"
DM_EDGE_DIR=/srv/dm3/shared/edge resolved edge-dir "${PAIR[@]}"
judge '
  const sources = model => Object.fromEntries((load(model).services.nginx.volumes ?? [])
    .map(volume => [volume.target, String(volume.source).replace(/\\/g, "/")]));
  const machine = { "/etc/nginx/.htpasswd": "/.htpasswd", "/etc/nginx/tls": "/tls", "/var/www/certbot": "/acme" };
  for (const [target, file] of Object.entries(machine)) {
    expect((sources("edge-dir")[target] ?? "").endsWith("/dm3/shared/edge" + file),
      `${target} does not come from DM_EDGE_DIR`);
    expect((sources("site")[target] ?? "").endsWith("/docker/nginx" + file),
      `${target} no longer comes from docker/nginx when DM_EDGE_DIR is unset`);
  }
'

echo "the application images take the release's digest, and the IMAGE_TAG form without it"
DIGEST="sha256:$(printf '0%.0s' {1..64})"
DM_IMAGE_API="registry.test/dm-api@$DIGEST" \
DM_IMAGE_FRONT="registry.test/dm-front@$DIGEST" \
DM_IMAGE_WORKER_MAIL="registry.test/dm-worker-mail@$DIGEST" \
DM_IMAGE_WORKER_NOTIFICATION="registry.test/dm-worker-notification@$DIGEST" \
  resolved digests "${PAIR[@]}" --profile rollout
judge '
  const images = {
    "dm-api": ["migration", "dmapi", "dmapi-next"],
    "dm-front": ["dmfront", "dmfront-next"],
    "dm-worker-mail": ["dm-mail-worker"],
    "dm-worker-notification": ["dm-notification-worker"],
  };
  for (const [image, names] of Object.entries(images))
    for (const name of names) {
      expect(load("digests").services[name]?.image === `registry.test/${image}@${process.argv[2]}`,
        `${name} ignores the digest the release names`);
      expect(load("site").services[name]?.image === `ghcr.io/dm-am/${image}:latest`,
        `${name} lost the IMAGE_TAG form the stand and the point of presence run`);
    }
' "$DIGEST"

echo "every target Prometheus scrapes is a container of the stack"
mapfile -t targets < <(sed -n "s/.*targets: *\[\(.*\)\].*/\1/p" prometheus.yml \
  | tr ',' '\n' | tr -d " '\"" | cut -d: -f1 | grep -vx localhost || true)
test "${#targets[@]}" -gt 1 || fail "no scrape targets found in docker/prometheus.yml"
judge '
  const names = new Set(Object.values(load("site").services).map(service => service.container_name));
  for (const host of process.argv.slice(2))
    expect(names.has(host), `prometheus.yml scrapes ${host}, which no service of the stack is named`);
' "${targets[@]}"

echo "two release directories exported from one commit resolve to one stand"
# The release runs compose with the project directory through rel/current and the files of
# the release itself, so a relative mount must land on one path whatever release is live, or
# a release would recreate every service that mounts a file, PostgreSQL included.
LAYOUT="$WORK/layout"
for release in a b; do
  mkdir -p "$LAYOUT/rel/$release"
  git -C "$ROOT" archive HEAD docker | tar -x -C "$LAYOUT/rel/$release"
  printf 'DM_IMAGE_API=registry.test/dm-api@%s\n' "$DIGEST" > "$LAYOUT/rel/$release/release.env"
done
: > "$LAYOUT/env"
for release in a b; do
  # A checkout on Windows may copy instead of linking; the paths compose sees are the same.
  rm -rf "$LAYOUT/rel/current"
  ln -s "$release" "$LAYOUT/rel/current"
  docker compose --project-directory "$LAYOUT/rel/current/docker" \
    --env-file "$LAYOUT/env" --env-file "$LAYOUT/rel/$release/release.env" \
    -f "$LAYOUT/rel/$release/docker/docker-compose.yml" \
    -f "$LAYOUT/rel/$release/docker/docker-compose.preview.yml" \
    config --format json > "$WORK/export-$release.json"
done
cmp -s "$WORK/export-a.json" "$WORK/export-b.json" ||
  fail "two exports of one commit resolve differently, so every release would recreate what they mount"
grep -q "registry.test/dm-api@$DIGEST" "$WORK/export-a.json" ||
  fail "the release.env given second does not reach the resolved model"

echo "OK: the deployment files describe one stand."
