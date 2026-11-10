#!/bin/bash
set -euo pipefail

# A real nightly dump holds the structure of the outbox table and none of its rows:
# a waiting letter keeps a live confirmation link there in plaintext, and the exclusion in
# backup-postgres.sh is the only thing that keeps it out of the dump and its offsite copy.
# The pattern there is case-folded unless quoted, and an unquoted one excludes nothing while
# the dump still succeeds, so only a dump read back proves it. Then the same dump passes
# verify-backup.sh and restores with an empty outbox table.
#
# Usage:
#   check-dump-excludes-outbox.sh
#
# PG_CONTAINER names the database container for a stand under another project name. Leaves
# the application database as it found it: the row it inserts and the database it restores
# into are removed on the way out.

SCRIPTS="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONTAINER="${PG_CONTAINER:-dm-pg}"
DATABASE="dm3"
RESTORED="dm3_dump_check"
# A queue no worker drains, so the row waits until the cleanup below removes it.
QUEUE="dump-check"
MARKER="dump-check-letter-$(date +%s)-$$"
WORK_DIR="$(mktemp -d)"

psql_as_postgres() {
    local database="$1"
    shift
    docker exec -i "$CONTAINER" psql -U postgres -d "$database" -tAX -v ON_ERROR_STOP=1 "$@"
}

cleanup() {
    psql_as_postgres "$DATABASE" -c "DELETE FROM \"OutboxEvents\" WHERE \"Queue\" = '$QUEUE'" > /dev/null || true
    psql_as_postgres postgres -c "DROP DATABASE IF EXISTS $RESTORED" > /dev/null || true
    rm -rf "$WORK_DIR"
}
trap cleanup EXIT

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

psql_as_postgres "$DATABASE" -c "
    INSERT INTO \"OutboxEvents\" (\"EventId\", \"Queue\", \"Payload\", \"OccurredUtc\")
    VALUES (gen_random_uuid(), '$QUEUE', '$MARKER', now())" > /dev/null
echo "The row the dump must leave out:"
psql_as_postgres "$DATABASE" -c "
    SELECT \"Id\", \"Queue\", \"Payload\" FROM \"OutboxEvents\" WHERE \"Queue\" = '$QUEUE'"

# Empty S3_BACKUP_BUCKET: a value set here wins over docker/.env, and the check is no reason
# to upload anything offsite.
BACKUP_DIR="$WORK_DIR/postgresql" CONTAINER_NAME="$CONTAINER" DATABASE_NAME="$DATABASE" \
    S3_BACKUP_BUCKET="" bash "$SCRIPTS/backup-postgres.sh"

DUMP_FILE="$(find "$WORK_DIR/postgresql" -name "*.sql.gz" -type f | head -1)"
[ -n "$DUMP_FILE" ] || fail "backup-postgres.sh left no dump in $WORK_DIR/postgresql"
DUMP="$WORK_DIR/dump.sql"
gunzip -c "$DUMP_FILE" > "$DUMP"

# Another table's rows: the history the migration wrote is in every database it ran on, so
# its data block shows the dump carries data at all.
grep -q '^COPY public."__EFMigrationsHistory" ' "$DUMP" ||
    fail "the dump holds no data block of __EFMigrationsHistory, so it proves nothing"
grep -q '^CREATE TABLE public."OutboxEvents" ' "$DUMP" ||
    fail "the dump lost the structure of OutboxEvents, a restore would have no outbox table"
if grep -q '^COPY public."OutboxEvents" ' "$DUMP"; then
    fail "the dump holds the data block of OutboxEvents: is the pattern in backup-postgres.sh quoted?"
fi
if grep -qF "$MARKER" "$DUMP"; then
    fail "the dump holds the body of the inserted outbox row"
fi
echo "ok: the dump holds the data of __EFMigrationsHistory and no data of OutboxEvents"

# The storage half of the verifier has nothing to read here and answers with a warning (1);
# an error (2) is the dump's.
set +e
PG_BACKUP_DIR="$WORK_DIR/postgresql" STORAGE_BACKUP_DIR="$WORK_DIR/storage" \
    TEXTFILE_DIR="$WORK_DIR/no-textfile-collector" bash "$SCRIPTS/verify-backup.sh" > "$WORK_DIR/verify.log"
VERIFY_EXIT=$?
set -e
cat "$WORK_DIR/verify.log"
if [ "$VERIFY_EXIT" -gt 1 ] || ! grep -q "Completeness: OK" "$WORK_DIR/verify.log"; then
    fail "verify-backup.sh refuses the dump (exit $VERIFY_EXIT)"
fi
echo "ok: verify-backup.sh accepts the dump"

psql_as_postgres postgres -c "CREATE DATABASE $RESTORED" > /dev/null
psql_as_postgres "$RESTORED" -q < "$DUMP" > /dev/null
restored="$(psql_as_postgres "$RESTORED" -c "
    SELECT (SELECT count(*) FROM \"OutboxEvents\"), (SELECT count(*) > 0 FROM \"__EFMigrationsHistory\")")"
if [ "$restored" != "0|t" ]; then
    fail "the restore gives outbox rows|migration history $restored, expected 0|t"
fi
echo "ok: the restore gives an empty outbox table"
