#!/bin/bash
set -euo pipefail

# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

BACKUP_DIR="${BACKUP_DIR:-/var/backups/postgresql}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
CONTAINER="${CONTAINER_NAME:-dm-pg}"
DATABASE="${DATABASE_NAME:-dm3}"

mkdir -p "$BACKUP_DIR"
echo "[$(date)] Starting PostgreSQL backup..."

BACKUP_FILE="$BACKUP_DIR/${DATABASE}_$TIMESTAMP.sql.gz"

# Written aside and moved into place: a dump that dies halfway must not leave a truncated
# file with the newest timestamp for a restore to reach first. Age, size and gunzip -t
# all pass on a well-formed prefix of a dump.
TMP_FILE="$(mktemp "$BACKUP_DIR/.${DATABASE}_$TIMESTAMP.XXXXXX")"
trap 'rm -f "$TMP_FILE"' EXIT

docker exec "$CONTAINER" pg_dump -U postgres "$DATABASE" | gzip > "$TMP_FILE"

if [ ! -s "$TMP_FILE" ]; then
    echo "[$(date)] ERROR: Backup file is empty!" >&2
    exit 1
fi

# pg_dump writes this marker last, so a file cut short is refused here, not at the restore.
if ! gunzip -c "$TMP_FILE" | tail -c 4096 | grep -q "PostgreSQL database dump complete"; then
    echo "[$(date)] ERROR: Backup is truncated — pg_dump did not finish!" >&2
    exit 1
fi

mv "$TMP_FILE" "$BACKUP_FILE"
trap - EXIT

BACKUP_SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
echo "[$(date)] Backup created: ${DATABASE}_$TIMESTAMP.sql.gz ($BACKUP_SIZE)"

# Cleanup old backups
DELETED=$(find "$BACKUP_DIR" -name "*.sql.gz" -mtime +"$RETENTION_DAYS" -delete -print | wc -l)

# The dot-file a dump is written into: a killed process leaves it behind, and no pattern
# above sweeps it.
find "$BACKUP_DIR" -name ".${DATABASE}_*" -mtime +1 -delete
echo "[$(date)] Cleaned up $DELETED backups older than $RETENTION_DAYS days"

# Optional: replicate to S3
if [ -n "${S3_BACKUP_BUCKET:-}" ]; then
    echo "[$(date)] Uploading to S3: $S3_BACKUP_BUCKET"
    aws s3 cp "$BACKUP_FILE" "s3://$S3_BACKUP_BUCKET/postgresql/" --quiet
    echo "[$(date)] S3 upload complete"
fi
