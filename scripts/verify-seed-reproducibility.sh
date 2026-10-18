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
#   bash scripts/verify-seed-reproducibility.sh --yes --compare <commit A> <commit B>
#   bash scripts/verify-seed-reproducibility.sh --yes --compare <commit A> WORKTREE
#
# The second form seeds commit A, then commit B, and diffs the two. It is the
# proof a refactoring of the seeder owes: moving its code must leave every draw
# where it was, and only a fixture built by the old code and one built by the
# new can show that. Each commit is taken out of git into a directory of its own
# and built into images tagged for this run, so neither the working tree nor the
# images the stack runs are touched. The compose file is the working tree's:
# the commits differ in the code they seed with, not in the services around it.
#
# WORKTREE in place of a commit is the working tree as it stands, built where it
# lies into images of its own: a change is compared before anybody commits it.
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

# Both commits are resolved before anything is reset: a name that means nothing
# should cost a message, not the development database.
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
# The images a comparison builds go with the run: one pair per commit, and
# nothing else refers to them once the migration container is gone. That
# container outlives the run on the last commit's image, and an image a
# container still uses is not removed: each run would leave one behind, and the
# stack's migration would stay on the code of commit B.
built_images=()
trap '
  if [ "${#built_images[@]}" -gt 0 ]; then
    compose rm -f -s migration > /dev/null 2>&1 || true
    docker image rm "${built_images[@]}" > /dev/null 2>&1 || true
  fi
  rm -rf "$snapshots"' EXIT

# The account the application connects as, spelled the way compose spells it.
# The database has to be created owned by it: created without an owner it
# belongs to postgres, and the migration then dies on "permission denied for
# schema public" before a single table exists.
APP_DB_USER="${DB_USER:-dm_app}"

# The compose files of the run in progress: the working tree's alone, or with
# the overlay that builds one commit's code.
compose_files=()
compose() {
  docker compose ${compose_files[@]+"${compose_files[@]}"} "$@"
}

# A path docker can read. Git Bash hands native programs the arguments it
# recognises as paths already converted, but not a path written inside a file,
# and the overlay below is such a file; on Linux and macOS this is the identity.
native_path() {
  if command -v cygpath > /dev/null 2>&1; then
    cygpath -m "$1"
  else
    printf '%s\n' "$1"
  fi
}

reset_databases() {
  compose exec -T postgres psql -U postgres -d postgres -v ON_ERROR_STOP=1 \
    -c 'DROP DATABASE IF EXISTS dm3 WITH (FORCE)' \
    -c "CREATE DATABASE dm3 OWNER \"$APP_DB_USER\"" >/dev/null

  # --force-recreate, because a one-shot service that already exited is
  # "up to date" as far as compose is concerned, and the second run would then
  # be seeded into a database with no schema.
  compose up -d --force-recreate --wait migration >/dev/null
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
  compose exec -T postgres psql -U postgres -d dm3 -v ON_ERROR_STOP=1 >/dev/null <<'SQL'
UPDATE "Users" SET "PasswordHash" = '', "Salt" = '';
UPDATE "PendingRegistrations" SET "PasswordHash" = '', "Salt" = '';
SQL

  # The \restrict guard pg_dump wraps its output in carries a nonce drawn per
  # dump, so the preamble differs between two dumps of the same bytes. Stripped
  # of its token rather than of the line: what the dump says it did stays
  # visible, and only the value nothing can reproduce goes.
  compose exec -T postgres \
    pg_dump -U postgres -d dm3 --data-only --column-inserts \
    | sed -E 's/^(\\(un)?restrict) .*/\1/' \
    | LC_ALL=C sort > "$target.pg"

}

# Seeds the database once from whatever the current compose files build, and
# keeps the dump under the given name.
seed_and_snapshot() {
  local name="$1"

  echo "--- $name: epoch $SEED_EPOCH, seed $SEED_RANDOM"
  reset_databases
  # One command, not "users" then "content": the user step draws from the same
  # generator, so splitting the run shifts every content choice after it.
  compose run --rm -T \
    -e "DM_SeedEpochUtc=$SEED_EPOCH" \
    -e "DM_SeedRandomSeed=$SEED_RANDOM" \
    seeder all
  snapshot "$snapshots/$name"
}

# Takes one commit out of git, or the working tree as it lies, and points the
# migration and the seeder at it. The images get tags of their own: the stack's
# dm-api image is the one dmapi runs, and building a commit into it would leave
# the site on that commit.
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
      context: $(native_path "$tree")
  seeder:
    image: dm-seed-compare-seeder:$name
    build:
      context: $(native_path "$tree")
YAML

  compose_files=(-f docker-compose.yml -f "$(native_path "$overlay")")
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

compose up -d postgres rabbitmq minio >/dev/null

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

# One comparison, not a loop over stores. The loop is what is left of the days
# there were two of them, and a loop that walks a single item reads as though a
# second store might appear in it.
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
