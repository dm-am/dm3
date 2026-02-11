#!/bin/bash
set -euo pipefail

BACKUP_DIR="${BACKUP_DIR:-/var/backups/minio}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
MINIO_ALIAS="${MINIO_ALIAS:-minio}"
BUCKET="${BUCKET_NAME:-dm-uploads}"

mkdir -p "$BACKUP_DIR/$TIMESTAMP"
echo "[$(date)] Starting MinIO backup..."

docker run --rm --network host \
  -v "$BACKUP_DIR/$TIMESTAMP:/backup" \
  minio/mc mirror "$MINIO_ALIAS/$BUCKET" /backup

BACKUP_SIZE=$(du -sh "$BACKUP_DIR/$TIMESTAMP" | cut -f1)
echo "[$(date)] Backup created: $TIMESTAMP/ ($BACKUP_SIZE)"

DELETED=$(find "$BACKUP_DIR" -maxdepth 1 -type d -mtime +$RETENTION_DAYS -not -path "$BACKUP_DIR" -exec rm -rf {} \; -print | wc -l)
echo "[$(date)] Cleaned up $DELETED backups older than $RETENTION_DAYS days"

# Optional: replicate to S3
if [ -n "${S3_BACKUP_BUCKET:-}" ]; then
    echo "[$(date)] Uploading to S3: $S3_BACKUP_BUCKET"
    aws s3 sync "$BACKUP_DIR/$TIMESTAMP" "s3://$S3_BACKUP_BUCKET/minio/$TIMESTAMP/" --quiet
    echo "[$(date)] S3 upload complete"
fi
