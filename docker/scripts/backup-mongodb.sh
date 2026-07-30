#!/bin/bash
set -euo pipefail

# Cron дает почти пустое окружение: без этого ночной запуск не видел ни пароля
# MinIO, ни ключей offsite-репликации.
# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

BACKUP_DIR="${BACKUP_DIR:-/var/backups/mongodb}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
CONTAINER="${CONTAINER_NAME:-dm-mongo}"

mkdir -p "$BACKUP_DIR"
echo "[$(date)] Starting MongoDB backup..."

BACKUP_FILE="$BACKUP_DIR/mongodb_$TIMESTAMP.archive.gz"
docker exec "$CONTAINER" mongodump --archive | gzip > "$BACKUP_FILE"

BACKUP_SIZE=$(du -h "$BACKUP_FILE" | cut -f1)
echo "[$(date)] Backup created: mongodb_$TIMESTAMP.archive.gz ($BACKUP_SIZE)"

if [ ! -s "$BACKUP_FILE" ]; then
    echo "[$(date)] ERROR: Backup file is empty!" >&2
    exit 1
fi

DELETED=$(find "$BACKUP_DIR" -name "*.archive.gz" -mtime +$RETENTION_DAYS -delete -print | wc -l)
echo "[$(date)] Cleaned up $DELETED backups older than $RETENTION_DAYS days"

# Optional: replicate to S3
if [ -n "${S3_BACKUP_BUCKET:-}" ]; then
    echo "[$(date)] Uploading to S3: $S3_BACKUP_BUCKET"
    aws s3 cp "$BACKUP_FILE" "s3://$S3_BACKUP_BUCKET/mongodb/" --quiet
    echo "[$(date)] S3 upload complete"
fi
