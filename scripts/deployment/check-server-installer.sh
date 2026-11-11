#!/usr/bin/env bash
# Fails when the two guarantees the server installer is written around stop holding: that it
# refuses a directory it would install over, and that it carries a new variable of the
# template into an environment file that already exists. Both are about a machine that is not
# this one, and neither can fail a test in the solution. Nothing here needs docker or a
# network (grep, a temporary directory and the installer's own scripts), so it costs under a
# second.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/common.sh"
cd "$ROOT"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

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

# The layout spans three directories, and a stand in any one of them is a stand.
for occupied in opt etc state; do
  mkdir -p "$WORK/three/$occupied-case/opt" "$WORK/three/$occupied-case/etc" "$WORK/three/$occupied-case/state"
  touch "$WORK/three/$occupied-case/$occupied/held"
  if bash docker/scripts/require-empty-install-dir.sh "$WORK/three/$occupied-case/opt" \
    "$WORK/three/$occupied-case/etc" "$WORK/three/$occupied-case/state" 2> "$WORK/guard.err"; then
    fail "the guard accepts a layout whose $occupied directory is not empty"
  fi
  if grep -q 'дерев' "$WORK/guard.err"; then
    fail "the guard still sends the operator to a tree: updates come as signed releases"
  fi
done
if ! grep -q 'dm3-release status' "$WORK/guard.err" || ! grep -q 'dm3-release maintain' "$WORK/guard.err"; then
  fail "the guard does not name dm3-release status and maintain, which is how a server is updated"
fi
bash docker/scripts/require-empty-install-dir.sh "$WORK/three/none/opt" "$WORK/empty" "$WORK/three/none/state" ||
  fail "the guard refuses a layout that is absent or empty"

# Order is the whole point: asked after apt, the answer arrives on a machine the
# installer has already changed.
#
# `|| true` because grep exits 1 on no match and this script runs under pipefail:
# without it a line that is no longer there kills the script on the assignment,
# and the messages below are what say which one went missing.
guard=$(grep -n 'require-empty-install-dir.sh' docker/setup-server.sh | head -1 | cut -d: -f1 || true)
apt=$(grep -n 'apt install' docker/setup-server.sh | head -1 | cut -d: -f1 || true)
layout=$(grep -n 'install-release.sh' docker/setup-server.sh | grep -v ':\s*#' | head -1 | cut -d: -f1 || true)
test -n "$guard" || fail "setup-server.sh never asks the guard"
test -n "$apt" || fail "setup-server.sh no longer installs the packages the guard precedes"
test -n "$layout" || fail "setup-server.sh no longer hands over to install-release.sh"
test "$guard" -lt "$apt" || fail "the guard runs after the packages are installed"
test "$apt" -lt "$layout" || fail "the layout is installed before the packages it needs"
grep -q 'require-empty-install-dir.sh" /opt/dm3 /etc/dm3 /var/lib/dm3' docker/setup-server.sh ||
  fail "setup-server.sh does not ask the guard about all three directories of the layout"
if grep -v '^\s*#' docker/setup-server.sh | grep -q -e 'git clone' -e 'docker compose.* up' -e 'DM_REF'; then
  fail "setup-server.sh still clones a tree or starts compose itself; the layout part and dm3-release do that"
fi

echo "the layout installer refuses to start without the signer's key and the tag"
# Before it touches anything: the same machine-wide paths a wrong call would change.
for call in "" "--tag v1.0.0" "--signers $WORK/signers" "--tag v1.0.0 --signers" \
  "--tag v1.0.0 --signers $WORK/missing-signers" "--tag v1.0.0 --signers $WORK/signers --mail-host relay.example"; do
  : > "$WORK/signers"
  printf 'release namespaces="git" ssh-ed25519 AAAA\n' > "$WORK/signers"
  # shellcheck disable=SC2086 # the call is a list of words on purpose
  if env DM3_ROOT="$WORK/layout/opt" DM3_ETC="$WORK/layout/etc" DM3_STATE="$WORK/layout/state" \
    bash docker/scripts/install-release.sh $call > /dev/null 2> "$WORK/install.err"; then
    fail "install-release.sh ran with \"$call\""
  fi
  grep -q -e '--signers' -e '--tag' -e '--mail-port' "$WORK/install.err" ||
    fail "install-release.sh refused \"$call\" for another reason than its arguments: $(cat "$WORK/install.err")"
  test ! -e "$WORK/layout" || fail "install-release.sh created the layout before refusing \"$call\""
done

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
cp docker/scripts/init-env.sh docker/scripts/_paths.sh "$WORK/env/scripts/"
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

echo "the operator's tools write into the checkout from a checkout and beside the releases from a release"
# A release directory is read-only and replaced by the next release, so a file written
# into it is lost or refused. From a checkout every tool must keep today's paths, which
# setup-server.sh, dm.sh and dm.ps1 still rely on.
#
# The logical path, not the resolved one: rel/current has to stay rel/current, or the
# cron lines would name one release forever.
BASE="$(cd "$WORK" && pwd)"
mkdir -p "$BASE/co/docker" "$BASE/opt/rel/v1-abc-123/docker"
cp -R docker/scripts "$BASE/co/docker/"
cp docker/.env.example "$BASE/co/docker/"
cp -R docker/scripts "$BASE/opt/rel/v1-abc-123/docker/"
cp docker/.env.example "$BASE/opt/rel/v1-abc-123/docker/"
printf "RELEASE_TAG=v1\n" > "$BASE/opt/rel/v1-abc-123/release.env"
ln -s v1-abc-123 "$BASE/opt/rel/current"

# Prints what _paths.sh decides for the copy of docker/scripts in $1.
layout_of() {
  # shellcheck disable=SC2016 # expanded by the shell that bash -c starts
  env -u DM_LAYOUT_ROOT SCRIPTS="$1" bash -c \
    '. "$SCRIPTS/_paths.sh" && printf "%s\n" "$DM_ENV_FILE" "$DM_EDGE_DIR" "$DM_CRON_SCRIPTS_DIR"'
}

expect_layout() {
  local from="$1" env_file="$2" edge_dir="$3" cron_dir="$4" actual
  actual="$(layout_of "$from")" || fail "_paths.sh cannot be sourced from $from"
  test "$(sed -n 1p <<< "$actual")" = "$env_file" ||
    fail "from $from the environment file is $(sed -n 1p <<< "$actual"), expected $env_file"
  test "$(sed -n 2p <<< "$actual")" = "$edge_dir" ||
    fail "from $from the edge files go to $(sed -n 2p <<< "$actual"), expected $edge_dir"
  if [ -n "$cron_dir" ]; then
    test "$(sed -n 3p <<< "$actual")" = "$cron_dir" ||
      fail "from $from cron runs $(sed -n 3p <<< "$actual"), expected $cron_dir"
  fi
}

expect_layout "$BASE/co/docker/scripts" "$BASE/co/docker/.env" "$BASE/co/docker/nginx" ""
expect_layout "$BASE/opt/rel/v1-abc-123/docker/scripts" \
  "$BASE/opt/shared/env" "$BASE/opt/shared/edge" "$BASE/opt/rel/current/docker/scripts"
expect_layout "$BASE/opt/rel/current/docker/scripts" \
  "$BASE/opt/shared/env" "$BASE/opt/shared/edge" "$BASE/opt/rel/current/docker/scripts"

# The installer writes shared/ before any release directory exists, from a checkout.
override="$(DM_LAYOUT_ROOT="$BASE/opt" bash -c '. "$1/_paths.sh" && echo "$DM_ENV_FILE"' _ "$BASE/co/docker/scripts")"
test "$override" = "$BASE/opt/shared/env" ||
  fail "DM_LAYOUT_ROOT does not win over the location: the environment file is $override"

echo "init-env.sh writes the shared environment file from a release and keeps the operator's choices"
# Read-only as on the server, where a write into the release is the fault this proves absent.
chmod -R a-w "$BASE/opt/rel/v1-abc-123"
trap 'chmod -R u+w "$WORK"; rm -rf "$WORK"' EXIT
before="$(find "$BASE/opt/rel" | sort)"
shared="$BASE/opt/shared/env"
release_init="$BASE/opt/rel/current/docker/scripts/init-env.sh"

# Some filesystems (NTFS under Git Bash) keep no modes, so the mode is asserted where
# the file system can hold one, which is every server.
touch "$WORK/mode-probe" && chmod 600 "$WORK/mode-probe"
modes_held=0
[ "$(stat -c %a "$WORK/mode-probe")" = 600 ] && modes_held=1

expect_private() {
  if [ "$modes_held" = 1 ]; then
    test "$(stat -c %a "$shared")" = 600 ||
      fail "the shared environment file has mode $(stat -c %a "$shared"), expected 600: it holds every credential"
  fi
}

if env -u DM_LAYOUT_ROOT bash "$release_init" server > /dev/null 2>&1; then
  fail "init-env.sh server accepted a release without MAIL_HOST"
fi
test -f "$shared" || fail "init-env.sh server from a release did not write $shared"
expect_private
generated="$(sed -n 's/^POSTGRES_PASSWORD=//p' "$shared")"
if [ -z "$generated" ] || grep -q "^POSTGRES_PASSWORD=${generated}$" docker/.env.example; then
  fail "init-env.sh server from a release left the template's credentials in $shared"
fi
grep -q "^DM_SCHEMA_REGIME=reset$" "$shared" ||
  fail "init-env.sh server wrote no DM_SCHEMA_REGIME=reset into $shared"
grep -q "^DM_RELEASE_HEADROOM_MIB=512$" "$shared" ||
  fail "init-env.sh server wrote no DM_RELEASE_HEADROOM_MIB=512 into $shared"
test "$(find "$BASE/opt/rel" | sort)" = "$before" ||
  fail "init-env.sh wrote into the release directory"

# The operator answers MAIL_HOST and switches the regime; the rerun must keep both.
tmp="$(mktemp)"
sed 's/^DM_SCHEMA_REGIME=.*/DM_SCHEMA_REGIME=migrate/' "$shared" > "$tmp"
cat "$tmp" > "$shared" && rm -f "$tmp"
printf "MAIL_HOST=relay.example\n" >> "$shared"
env -u DM_LAYOUT_ROOT bash "$release_init" server > /dev/null ||
  fail "init-env.sh server refused a shared file with MAIL_HOST set"
grep -q "^DM_SCHEMA_REGIME=migrate$" "$shared" ||
  fail "init-env.sh server overwrote the schema regime the operator chose"
expect_private

# local on a server is a developer stand with Development switched on.
if env -u DM_LAYOUT_ROOT bash "$release_init" local > /dev/null 2>&1; then
  fail "init-env.sh local ran from a release directory"
fi

# The installer's first run: the relay it was given reaches a file the run creates, so the
# credentials are generated in the same run and no second run meets the guard.
fresh="$BASE/fresh/shared/env"
DM_LAYOUT_ROOT="$BASE/fresh" MAIL_HOST=relay.example MAIL_PORT=2525 \
  bash "$BASE/co/docker/scripts/init-env.sh" server > /dev/null ||
  fail "init-env.sh server refused a first run given MAIL_HOST in the environment"
if ! grep -q "^MAIL_HOST=relay.example$" "$fresh" || ! grep -q "^MAIL_PORT=2525$" "$fresh"; then
  fail "init-env.sh server did not write the relay of the environment into $fresh"
fi
test -n "$(sed -n 's/^STORAGE_APP_PASSWORD=//p' "$fresh")" ||
  fail "init-env.sh server given the relay generated no credentials"
printf "MAIL_HOST=chosen.example\n" > "$BASE/fresh/kept"
grep -v '^MAIL_HOST=' "$fresh" >> "$BASE/fresh/kept" && cat "$BASE/fresh/kept" > "$fresh"
DM_LAYOUT_ROOT="$BASE/fresh" MAIL_HOST=other.example bash "$BASE/co/docker/scripts/init-env.sh" server > /dev/null
grep -q "^MAIL_HOST=chosen.example$" "$fresh" ||
  fail "init-env.sh server replaced a relay the file already named with the one of the environment"

echo "the edge tools write only where _paths.sh says"
# They cannot run here (docker run httpd, certbot), so the paths above are what proves
# their destination, and this proves they take it.
for tool in init-htpasswd.sh init-ssl.sh; do
  # shellcheck disable=SC2016 # the pattern holds a literal $
  grep -q '^\. "$SCRIPT_DIR/_paths.sh"' "docker/scripts/$tool" ||
    fail "$tool does not source _paths.sh, so from a release it writes into the release"
  if grep -q 'DOCKER_DIR/nginx' "docker/scripts/$tool"; then
    fail "$tool still writes under docker/nginx rather than under \$DM_EDGE_DIR"
  fi
done
# shellcheck disable=SC2016 # the pattern holds a literal $
grep -q '^HTPASSWD_FILE="$DM_EDGE_DIR/.htpasswd"' docker/scripts/init-htpasswd.sh ||
  fail "init-htpasswd.sh writes the credentials somewhere other than \$DM_EDGE_DIR"
# shellcheck disable=SC2016 # the patterns hold a literal $
if ! grep -q '^WEBROOT="$DM_EDGE_DIR/acme"' docker/scripts/init-ssl.sh ||
  ! grep -q '^TLS_DIR="$DM_EDGE_DIR/tls"' docker/scripts/init-ssl.sh; then
  fail "init-ssl.sh writes the challenge or the TLS server somewhere other than \$DM_EDGE_DIR"
fi

echo "OK: the server installer and the operator's tools hold what they are written around."
