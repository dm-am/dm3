#!/bin/bash
set -e
BACKUP_DIR="${BACKUP_DIR:-/var/backups/minio}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
MINIO_ALIAS="${MINIO_ALIAS:-minio}"
BUCKET="${BUCKET_NAME:-dm-uploads}"

mkdir -p "$BACKUP_DIR/$TIMESTAMP"
echo "[$(date)] Starting MinIO backup..."
docker run --rm --network host \
  -v "$BACKUP_DIR/$TIMESTAMP:/backup" \
  minio/mc mirror "$MINIO_ALIAS/$BUCKET" /backup
echo "[$(date)] Backup created: $TIMESTAMP/"

# Keep only last 7 days
find "$BACKUP_DIR" -maxdepth 1 -type d -mtime +7 -exec rm -rf {} \;
echo "[$(date)] Cleaned up backups older than 7 days"
