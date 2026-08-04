#!/bin/bash
set -euo pipefail

# Cron дает почти пустое окружение: без этого ночной запуск не видел ни пароля
# MinIO, ни ключей offsite-репликации.
# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

BACKUP_DIR="${BACKUP_DIR:-/var/backups/minio}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BUCKET="${BUCKET_NAME:-dm-uploads}"
MINIO_ENDPOINT="${MINIO_ENDPOINT:-http://localhost:9000}"
MINIO_ROOT_USER="${MINIO_ROOT_USER:-minio}"

if [ -z "${MINIO_ROOT_PASSWORD:-}" ]; then
    echo "[$(date)] ERROR: MINIO_ROOT_PASSWORD is not set (source docker/.env before running)" >&2
    exit 1
fi

mkdir -p "$BACKUP_DIR/$TIMESTAMP"
echo "[$(date)] Starting MinIO backup..."

# The alias has to be created inside the throwaway container: a fresh mc image
# knows no aliases, so mirroring from a bare "minio/bucket" reference could
# never work. Credentials go in as separate env values rather than inside a
# URL, because passwords containing @ or : break URL parsing.
docker run --rm --network host \
  -v "$BACKUP_DIR/$TIMESTAMP:/backup" \
  -e "MINIO_ENDPOINT=$MINIO_ENDPOINT" \
  -e "MINIO_ROOT_USER=$MINIO_ROOT_USER" \
  -e "MINIO_ROOT_PASSWORD=$MINIO_ROOT_PASSWORD" \
  -e "BUCKET=$BUCKET" \
  --entrypoint sh \
  minio/mc -c 'mc alias set src "$MINIO_ENDPOINT" "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" > /dev/null && mc mirror "src/$BUCKET" /backup'

BACKUP_SIZE=$(du -sh "$BACKUP_DIR/$TIMESTAMP" | cut -f1)
echo "[$(date)] Backup created: $TIMESTAMP/ ($BACKUP_SIZE)"

DELETED=$(find "$BACKUP_DIR" -maxdepth 1 -type d -mtime +"$RETENTION_DAYS" -not -path "$BACKUP_DIR" -exec rm -rf {} \; -print | wc -l)
echo "[$(date)] Cleaned up $DELETED backups older than $RETENTION_DAYS days"

# Optional: replicate to S3
if [ -n "${S3_BACKUP_BUCKET:-}" ]; then
    echo "[$(date)] Uploading to S3: $S3_BACKUP_BUCKET"
    aws s3 sync "$BACKUP_DIR/$TIMESTAMP" "s3://$S3_BACKUP_BUCKET/minio/$TIMESTAMP/" --quiet
    echo "[$(date)] S3 upload complete"
fi
