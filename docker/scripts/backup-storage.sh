#!/bin/bash
set -euo pipefail

# Nightly copy of the object storage bucket as a plain directory of files: readable without
# the store, countable by verify-backup.sh, restorable with restore-storage.sh. Runs as
# dm-backup, which may read and list only; its key goes into the throwaway container, while
# the AWS variables of docker/.env stay for the offsite replication at the end.

# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

BACKUP_DIR="${BACKUP_DIR:-/var/backups/storage}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
BUCKET="${STORAGE_BUCKET:-dm-uploads}"
BACKUP_USER="${STORAGE_BACKUP_USER:-dm-backup}"
# The loopback port docker-compose.yml publishes, reached from the host network.
# Both are overridable for a run against a stand that is reachable another way.
ENDPOINT="${STORAGE_ENDPOINT:-http://127.0.0.1:9000}"
NETWORK="${STORAGE_NETWORK:-host}"

if [ -z "${STORAGE_BACKUP_PASSWORD:-}" ]; then
    echo "[$(date)] ERROR: STORAGE_BACKUP_PASSWORD is not set (source docker/.env before running)" >&2
    exit 1
fi

mkdir -p "$BACKUP_DIR/$TIMESTAMP"
echo "[$(date)] Starting object storage backup..."

# Path-style addressing, or the client puts the bucket into a host name that does not
# exist. Checksums only where the protocol requires them. The keys are passed by name, so
# they appear in no process list.
AWS_ACCESS_KEY_ID="$BACKUP_USER" AWS_SECRET_ACCESS_KEY="$STORAGE_BACKUP_PASSWORD" \
docker run --rm --network "$NETWORK" \
  -v "$BACKUP_DIR/$TIMESTAMP:/backup" \
  -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY \
  -e AWS_DEFAULT_REGION=us-east-1 \
  -e AWS_REQUEST_CHECKSUM_CALCULATION=when_required \
  --entrypoint sh \
  amazon/aws-cli:2.39.7@sha256:bba262340b0bc7cbf3a8a48612531263b56f4b280c4f036a07816af1b271bda0 \
  -c 'aws configure set default.s3.addressing_style path && exec aws "$@"' aws \
  s3 sync "s3://$BUCKET" /backup --endpoint-url "$ENDPOINT" --only-show-errors

BACKUP_SIZE=$(du -sh "$BACKUP_DIR/$TIMESTAMP" | cut -f1)
BACKUP_FILES=$(find "$BACKUP_DIR/$TIMESTAMP" -type f | wc -l)
echo "[$(date)] Backup created: $TIMESTAMP/ ($BACKUP_SIZE, $BACKUP_FILES files)"

DELETED=$(find "$BACKUP_DIR" -maxdepth 1 -type d -mtime +"$RETENTION_DAYS" -not -path "$BACKUP_DIR" -exec rm -rf {} \; -print | wc -l)
echo "[$(date)] Cleaned up $DELETED backups older than $RETENTION_DAYS days"

# Optional: replicate to S3
if [ -n "${S3_BACKUP_BUCKET:-}" ]; then
    echo "[$(date)] Uploading to S3: $S3_BACKUP_BUCKET"
    aws s3 sync "$BACKUP_DIR/$TIMESTAMP" "s3://$S3_BACKUP_BUCKET/storage/$TIMESTAMP/" --quiet
    echo "[$(date)] S3 upload complete"
fi
