#!/usr/bin/env bash
# Fails when the files a server is deployed from stop describing one stand: an
# overlay that no longer parses, a build context pointing at a directory that
# was removed, or a systemd unit and an installer that name different compose
# files. None of that can fail a test in the solution, and all of it fails on
# the server instead.
#
# Why a script and not the commands in the workflow: they ran in the
# compose-topology job and nowhere else, so the first reader of a broken overlay
# was a red push. The whole job costs twenty-two seconds against the forty
# minutes of the local set, and leaving the cheap steps in CI would keep the
# class open anyway - editing a deployment file would still redden a push on the
# steps that stayed. The workflow, scripts/gates.sh and scripts/hooks/pre-push
# all call this now, so there is one command and one version of it.
#
# Failures are plain messages on stderr rather than GitHub's ::error::
# annotations: this text has to read in a terminal, and CI names the step that
# failed by itself.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT/docker"

# Placeholders whose only job is to be non-empty, and not secrets. Compose
# rejects an empty ${VAR:?} as firmly as a missing one, so every `config` below
# needs the whole set. Declared here rather than in the workflow job, where two
# steps carried identical copies of it: --env-file /dev/null is what keeps the
# answer about the compose files instead of about the .env of whoever runs this,
# and that decision is the same on a runner and on a developer machine.
export DM_CryptoConfiguration__KeyBase64=validation-placeholder
export POSTGRES_PASSWORD=validation-placeholder
export RABBITMQ_DEFAULT_PASS=validation-placeholder
export MINIO_ROOT_PASSWORD=validation-placeholder
export MINIO_APP_PASSWORD=validation-placeholder
export MINIO_IMGPROXY_PASSWORD=validation-placeholder
export GF_SECURITY_ADMIN_PASSWORD=validation-placeholder
export IMGPROXY_KEY=validation-placeholder
export IMGPROXY_SALT=validation-placeholder
export POP_UPSTREAM=https://validation-placeholder
export POP_UPSTREAM_HOST=validation-placeholder

# The two the compose files declare without ${...:?}. They cost nothing to fill
# and unfilled they print a warning per service that references them - fifteen
# lines that bury the output of the checks below on a screen.
export DM_APP_PASSWORD=validation-placeholder
export DM_EXPORTER_PASSWORD=validation-placeholder

fail() {
  echo "ERROR: $1" >&2
  exit 1
}

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

echo "the unit systemd runs names the same files and profiles as the installer"
# Profiles as well as files: watchtower lives behind the production profile, and
# a unit that drops it stops updating the stand at the first reboot with nothing
# in the logs to say so.
#
# `|| true` because grep exits 1 on no match and this script runs under
# pipefail: without it an extraction that found nothing kills the script on the
# assignment, which is exactly the case the two messages below are written for.
pattern='(\-f [a-z.-]+\.yml|--profile [a-z]+)'
installer=$(grep -oE "$pattern" setup-server.sh | sort -u || true)
unit=$(grep -oE "$pattern" dm3.service | sort -u || true)
echo "  installer: $(echo "$installer" | tr '\n' ' ')"
echo "  unit:      $(echo "$unit" | tr '\n' ' ')"

# Non-empty first, and named positively after that. Two empty extractions
# compare equal, so a switch to --file, to COMPOSE_FILE or to a path outside the
# character class above would have left this check green on nothing at all -
# while the failure it exists to catch came back.
test -n "$installer" || fail "no compose files or profiles found in docker/setup-server.sh"
test -n "$unit" || fail "no compose files or profiles found in docker/dm3.service"
printf '%s\n' "$installer" | grep -qx -- '-f docker-compose.preview.yml' ||
  fail "the installer no longer names the overlay the site lives in"
printf '%s\n' "$installer" | grep -qx -- '--profile production' ||
  fail "the installer no longer enables the profile watchtower lives behind"
test "$installer" = "$unit" ||
  fail "dm3.service and setup-server.sh disagree on the compose files or profiles"

echo "OK: the deployment files describe one stand."
