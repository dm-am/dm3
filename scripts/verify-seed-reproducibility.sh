#!/usr/bin/env bash
# Runs the seeder twice into the same empty database and diffs the result.
#
# This is the proof behind every pixel baseline: a screenshot is a measurement only if the
# fixture under it is the same fixture twice. It is not an xUnit test because seeding writes
# to PostgreSQL and object storage at once, so it needs the stack up. The regression guard
# that runs on every build is test/DM.Architecture.Tests/SeedDeterminismShould.cs.
#
# It RESETS the dm3 databases of the local stack, which hold seeded development data and
# nothing else. Stop dmapi first: it holds connections to a database this drops.
#
#   bash scripts/verify-seed-reproducibility.sh --yes
#   bash scripts/verify-seed-reproducibility.sh --yes --compare <commit A> <commit B>
#   bash scripts/verify-seed-reproducibility.sh --yes --compare <commit A> WORKTREE
#
# The second form seeds commit A, then commit B, and diffs the two: the proof a refactoring
# of the seeder owes, since moving its code must leave every draw where it was. Each commit
# is taken out of git into a directory of its own and built into images tagged for this run,
# so neither the working tree nor the images the stack runs are touched. The compose file is
# the working tree's. WORKTREE in place of a commit is the working tree as it stands, built
# where it lies, so a change is compared before anybody commits it.
#
# Knobs: SEED_EPOCH and SEED_RANDOM, both pinned by default. The epoch is pinned here
# although the seeder defaults it to the clock, which keeps a development site looking alive
# and is exactly what two runs must not do.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=/dev/null
. "$REPO_ROOT/scripts/lib/common.sh"
cd "$REPO_ROOT/docker"

SEED_EPOCH="${SEED_EPOCH:-2026-06-15T12:00:00Z}"
SEED_RANDOM="${SEED_RANDOM:-20260730}"

# The name that stands for the working tree in place of a commit.
WORKTREE="WORKTREE"

usage() {
  echo "usage: $0 [--yes] [--compare <commit A> <commit B | $WORKTREE>]" >&2
}

confirmed=no
compare=()
while [ $# -gt 0 ]; do
  case "$1" in
    --yes)
      confirmed=yes
      ;;
    --compare)
      if [ $# -lt 3 ]; then
        usage
        exit 2
      fi
      compare=("$2" "$3")
      shift 2
      ;;
    *)
      usage
      exit 2
      ;;
  esac
  shift
done

# Both commits are resolved before anything is reset: a name that means nothing costs a
# message, not the development database.
for rev in ${compare[@]+"${compare[@]}"}; do
  if [ "$rev" != "$WORKTREE" ] && ! git -C "$REPO_ROOT" rev-parse --verify --quiet "$rev^{commit}" > /dev/null; then
    echo "Not a commit: $rev" >&2
    exit 2
  fi
done

if [ "$confirmed" != "yes" ]; then
  echo "This drops and rebuilds the dm3 database in PostgreSQL."
  read -r -p "Continue? [y/N] " answer
  [ "$answer" = "y" ] || exit 1
fi

set -a
# shellcheck disable=SC1091
. ./.env
set +a

snapshots="$(mktemp -d)"
# The images a comparison builds go with the run, one pair per commit. The migration
# container outlives the run on the last commit's image, and an image a container still
# uses is not removed, so it is removed first.
built_images=()
trap '
  if [ "${#built_images[@]}" -gt 0 ]; then
    compose rm -f -s migration > /dev/null 2>&1 || true
    docker image rm "${built_images[@]}" > /dev/null 2>&1 || true
  fi
  rm -rf "$snapshots"' EXIT

# The account the application connects as, spelled the way compose spells it. The database
# is created owned by it, or the migration dies on "permission denied for schema public".
APP_DB_USER="${DB_USER:-dm_app}"

# The compose files of the run in progress: the working tree's alone, or with the overlay
# that builds one commit's code.
compose_files=()
compose() {
  docker compose ${compose_files[@]+"${compose_files[@]}"} "$@"
}

reset_databases() {
  compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 \
    -c 'DROP DATABASE IF EXISTS dm3 WITH (FORCE)' \
    -c "CREATE DATABASE dm3 OWNER \"$APP_DB_USER\"" >/dev/null

  # --force-recreate: a one-shot service that already exited is "up to date" to compose, and
  # the second run would be seeded into a database with no schema.
  compose up -d --force-recreate --wait migration >/dev/null
}

snapshot() {
  local target="$1"

  # The salt, the hash and the secret of a second factor follow a cryptographic generator:
  # the only three values in the fixture that are not a function of the seed. The secret is
  # encrypted under a nonce drawn per call, so the same plain secret is new bytes every run.
  # Flattened in place, so the comparison stays a plain diff with no exclusion list to grow.
  # Both tables that carry a password: an account waiting for its activation letter is
  # hashed by the same call.
  compose exec -T postgres psql -U postgres -d dm3 -v ON_ERROR_STOP=1 >/dev/null <<'SQL'
UPDATE "Users" SET "PasswordHash" = '', "Salt" = '';
UPDATE "PendingRegistrations" SET "PasswordHash" = '', "Salt" = '';
UPDATE "UserTwoFactors" SET "Secret" = '';
SQL

  # The \restrict guard of pg_dump carries a nonce drawn per dump, so the preamble differs
  # between two dumps of the same bytes. The token is stripped, not the line.
  compose exec -T postgres \
    pg_dump -U postgres -d dm3 --data-only --column-inserts \
    | sed -E 's/^(\\(un)?restrict) .*/\1/' \
    | LC_ALL=C sort > "$target.pg"

}

# Seeds the database once from whatever the current compose files build, and keeps the dump
# under the given name.
seed_and_snapshot() {
  local name="$1"

  echo "--- $name: epoch $SEED_EPOCH, seed $SEED_RANDOM"
  reset_databases
  # One command, not "users" then "content": the user step draws from the same generator.
  compose run --rm -T \
    -e "DM_SeedEpochUtc=$SEED_EPOCH" \
    -e "DM_SeedRandomSeed=$SEED_RANDOM" \
    seeder all
  snapshot "$snapshots/$name"
}

# Takes one commit out of git, or the working tree as it lies, and points the migration and
# the seeder at it. The images get tags of their own, since building a commit into the
# stack's dm-api image would leave the site on that commit.
use_commit() {
  local rev="$1" name="$2"
  local tree="$snapshots/tree-$name"
  local overlay="$snapshots/compose-$name.yml"

  if [ "$rev" = "$WORKTREE" ]; then
    tree="$REPO_ROOT"
  else
    mkdir -p "$tree"
    git -C "$REPO_ROOT" archive --format=tar "$rev" | tar -x -C "$tree"
  fi

  cat > "$overlay" <<YAML
services:
  migration:
    image: dm-seed-compare-api:$name
    build:
      context: $(host "$tree")
  seeder:
    image: dm-seed-compare-seeder:$name
    build:
      context: $(host "$tree")
YAML

  compose_files=(-f docker-compose.yml -f "$(host "$overlay")")
  built_images+=("dm-seed-compare-api:$name" "dm-seed-compare-seeder:$name")
}

# What a side of the comparison is called in the output and in its image tags.
side_name() {
  if [ "$1" = "$WORKTREE" ]; then
    echo "worktree"
  else
    git -C "$REPO_ROOT" rev-parse --short "$1"
  fi
}

compose up -d --wait storage seaweedfs >/dev/null
compose up -d postgres rabbitmq >/dev/null

if [ "${#compare[@]}" -eq 0 ]; then
  compose build migration seeder
  first="run1"
  second="run2"
  seed_and_snapshot "$first"
  seed_and_snapshot "$second"
else
  first="a-$(side_name "${compare[0]}")"
  second="b-$(side_name "${compare[1]}")"
  for side in 0 1; do
    if [ "$side" -eq 0 ]; then name="$first"; else name="$second"; fi
    use_commit "${compare[$side]}" "$name"
    compose build migration seeder
    seed_and_snapshot "$name"
  done
fi

status=0
if diff -u "$snapshots/$first.pg" "$snapshots/$second.pg" > "$snapshots/pg.diff"; then
  echo "OK: $first and $second produced an identical dump ($(wc -l < "$snapshots/$first.pg") lines)."
else
  echo "DIFFERENT: the dumps of $first and $second do not match."
  head -n 40 "$snapshots/pg.diff"
  status=1
fi

if [ "$status" -ne 0 ]; then
  if [ "${#compare[@]}" -eq 0 ]; then
    cat <<'EOF'

The seed is not reproducible yet, so no pixel baseline taken against it means
anything. Find what the differing rows have in common - a date, a name, an id,
a count - and follow it back to the draw that produced it.
EOF
  else
    cat <<'EOF'

The two commits seed different fixtures. If the change between them was meant to
leave the seed alone, a draw moved: find what the differing rows have in common
and follow it back to the call that now runs earlier or later than it did.
EOF
  fi
fi

exit "$status"
