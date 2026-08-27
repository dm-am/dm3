#!/usr/bin/env bash
# Fails when the two guarantees the server installer is written around stop
# holding: that it refuses a directory it would install over, and that it
# carries a new variable of the template into an environment file that already
# exists.
#
# Both are about a machine that is not this one, and neither can fail a test in
# the solution: the first changes a running stand before it is discovered, the
# second is discovered by a stand that does not come back up. Nothing here needs
# docker or a network - it is grep, a temporary directory and the installer's own
# scripts - so the whole file costs under a second.
#
# Why a script and not the commands in the workflow: they ran in the
# compose-topology job and nowhere else. The workflow, scripts/gates.sh and
# scripts/hooks/pre-push all call this now, so there is one command and one
# version of it.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

fail() {
  echo "ERROR: $1" >&2
  exit 1
}

echo "the installer refuses a directory it would install over"
# The installer's first act, and the one that has to hold: run twice it reached a
# clone into a non-empty directory and died there, having already installed
# packages and restarted the docker daemon of a running stand.
mkdir -p "$WORK/empty" "$WORK/occupied"
bash docker/scripts/require-empty-install-dir.sh "$WORK/empty" ||
  fail "the guard refuses an empty directory, so the installer can never run"

touch "$WORK/occupied/docker-compose.yml"
if bash docker/scripts/require-empty-install-dir.sh "$WORK/occupied" 2> /dev/null; then
  fail "the guard accepts a directory that already holds a stand"
fi

# Order is the whole point: asked after apt, the answer arrives on a machine the
# installer has already changed.
#
# `|| true` because grep exits 1 on no match and this script runs under pipefail:
# without it a line that is no longer there kills the script on the assignment,
# and the three messages below are what say which one went missing.
guard=$(grep -n 'require-empty-install-dir.sh' docker/setup-server.sh | head -1 | cut -d: -f1 || true)
clone=$(grep -n 'git clone' docker/setup-server.sh | head -1 | cut -d: -f1 || true)
apt=$(grep -n 'apt install' docker/setup-server.sh | head -1 | cut -d: -f1 || true)
test -n "$guard" || fail "setup-server.sh never asks the guard"
test -n "$clone" || fail "setup-server.sh no longer clones the tree the guard precedes"
test -n "$apt" || fail "setup-server.sh no longer installs the packages the guard precedes"
test "$guard" -lt "$clone" || fail "the guard runs after the clone it exists to precede"
test "$guard" -lt "$apt" || fail "the guard runs after the packages are installed"

echo "the installer of the environment file carries a new variable over"
# A variable added to .env.example reaches nobody by itself: docker/.env is
# machine-local and never regenerated, so the next release refuses to start on a
# stand that worked yesterday. That is how splitting the Postgres roles stopped
# every local reset - the two passwords it needs were in the template and in
# nobody's file. init-env.sh carries them over in local mode, and this is the
# only thing that says so.
#
# In a copy of the tree rather than in place: init-env.sh writes the .env next to
# the template it is run from, and the one in docker/ is the environment file of
# the machine running this.
mkdir -p "$WORK/env/scripts"
cp docker/scripts/init-env.sh "$WORK/env/scripts/"
cp docker/.env.example "$WORK/env/"

# A file written before the newest variable existed, which is the case.
grep -v "^DM_APP_PASSWORD=" docker/.env.example > "$WORK/env/.env"
bash "$WORK/env/scripts/init-env.sh" local > /dev/null

grep -q "^DM_APP_PASSWORD=" "$WORK/env/.env" ||
  fail "init-env.sh left a variable of .env.example out of an existing .env; every local stand breaks on the next one added"

# And it must not touch a value somebody already chose.
printf "GF_SECURITY_ADMIN_PASSWORD=chosen-by-hand\n" >> "$WORK/env/.env"
bash "$WORK/env/scripts/init-env.sh" local > /dev/null
grep -q "^GF_SECURITY_ADMIN_PASSWORD=chosen-by-hand" "$WORK/env/.env" ||
  fail "init-env.sh overwrote a value that was already in .env"

echo "OK: the server installer holds both guarantees."
