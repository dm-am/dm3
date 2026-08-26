#!/usr/bin/env bash
# Runs the seeder twice into the same empty database and diffs the result.
#
# This is the proof behind every future pixel baseline: a screenshot is a
# measurement only if the fixture under it is the same fixture twice. It is not
# an xUnit test because seeding writes to PostgreSQL and object storage
# at once, so the thing being proved does not exist until the stack is up. The
# regression guard is the test, and it runs on every build:
# test/DM.Architecture.Tests/SeedDeterminismShould.cs.
#
# It RESETS the dm3 databases of the local stack. They hold seeded development
# data and nothing else, which is the only reason this script is allowed to
# exist, but it is still a reset. Stop dmapi first if it is running: it holds
# connections to a database this drops.
#
#   bash scripts/verify-seed-reproducibility.sh --yes
#
# Knobs: SEED_EPOCH and SEED_RANDOM, both pinned by default. The epoch has to be
# pinned here even though the seeder defaults it to the clock - that default is
# what keeps a development site looking alive, and it is exactly what two runs
# must not do.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_ROOT/docker"

SEED_EPOCH="${SEED_EPOCH:-2026-06-15T12:00:00Z}"
SEED_RANDOM="${SEED_RANDOM:-20260730}"

if [ "${1:-}" != "--yes" ]; then
  echo "This drops and rebuilds the dm3 database in PostgreSQL."
  read -r -p "Continue? [y/N] " answer
  [ "$answer" = "y" ] || exit 1
fi

set -a
# shellcheck disable=SC1091
. ./.env
set +a

snapshots="$(mktemp -d)"
trap 'rm -rf "$snapshots"' EXIT

# The account the application connects as, spelled the way compose spells it.
# The database has to be created owned by it: created without an owner it
# belongs to postgres, and the migration then dies on "permission denied for
# schema public" before a single table exists.
APP_DB_USER="${DB_USER:-dm_app}"

reset_databases() {
  docker compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 \
    -c 'DROP DATABASE IF EXISTS dm3 WITH (FORCE)' \
    -c "CREATE DATABASE dm3 OWNER \"$APP_DB_USER\"" >/dev/null

  # --force-recreate, because a one-shot service that already exited is
  # "up to date" as far as compose is concerned, and the second run would then
  # be seeded into a database with no schema.
  docker compose up -d --force-recreate --wait migration >/dev/null
}

snapshot() {
  local target="$1"

  # The salt comes from a cryptographic generator and the hash follows it: the
  # only two values in the fixture that are not a function of the seed.
  # Flattened in place rather than filtered out of the dump, so the comparison
  # stays a plain diff with no exclusion list to grow.
  #
  # Both tables that carry a password, not just the one: an account waiting for
  # its activation letter is hashed by the same call the finished accounts are,
  # so leaving PendingRegistrations out left the pair unflattened in exactly one
  # row and the run reported a difference that was never the seed's.
  docker compose exec -T postgres psql -U postgres -d dm3 -v ON_ERROR_STOP=1 >/dev/null <<'SQL'
UPDATE "Users" SET "PasswordHash" = '', "Salt" = '';
UPDATE "PendingRegistrations" SET "PasswordHash" = '', "Salt" = '';
SQL

  # The \restrict guard pg_dump wraps its output in carries a nonce drawn per
  # dump, so the preamble differs between two dumps of the same bytes. Stripped
  # of its token rather than of the line: what the dump says it did stays
  # visible, and only the value nothing can reproduce goes.
  docker compose exec -T postgres \
    pg_dump -U postgres -d dm3 --data-only --column-inserts \
    | sed -E 's/^(\\(un)?restrict) .*/\1/' \
    | LC_ALL=C sort > "$target.pg"

}

docker compose up -d postgres rabbitmq minio >/dev/null
docker compose build migration seeder

for run in 1 2; do
  echo "--- run $run: epoch $SEED_EPOCH, seed $SEED_RANDOM"
  reset_databases
  # One command, not "users" then "content": the user step draws from the same
  # generator, so splitting the run shifts every content choice after it.
  docker compose run --rm -T \
    -e "DM_SeedEpochUtc=$SEED_EPOCH" \
    -e "DM_SeedRandomSeed=$SEED_RANDOM" \
    seeder all
  snapshot "$snapshots/run$run"
done

status=0
for store in pg; do
  if diff -u "$snapshots/run1.$store" "$snapshots/run2.$store" > "$snapshots/$store.diff"; then
    echo "OK: $store identical across both runs ($(wc -l < "$snapshots/run1.$store") lines)."
  else
    echo "DIFFERENT: $store"
    head -n 40 "$snapshots/$store.diff"
    status=1
  fi
done

if [ "$status" -ne 0 ]; then
  cat <<'EOF'

The seed is not reproducible yet, so no pixel baseline taken against it means
anything. Find what the differing rows have in common - a date, a name, an id,
a count - and follow it back to the draw that produced it.
EOF
fi

exit "$status"
