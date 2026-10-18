#!/bin/bash
set -euo pipefail

# Nightly copy of the object storage bucket as a plain directory of files.
#
# A directory rather than a dump of the volume: it is readable without the store
# that wrote it, verify-backup.sh counts its files, and a restore into this store
# or any other S3 is one command, the line in docs/guides/DEPLOYMENT.md.
#
# Runs as dm-backup, the account that may read and list and nothing else, so
# the key on this host cannot change a single object. The key goes into the
# throwaway container only. The AWS variables of docker/.env, which _env.sh
# exports, stay what they are for: the offsite replication at the end.

# Cron дает почти пустое окружение: без этого ночной запуск не видел ни пароля
# хранилища, ни ключей offsite-репликации.
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

# Path-style addressing is set inside the container: without it the client puts
# the bucket into the host name, and no such host exists. Checksums are sent
# only where the protocol requires them: newer clients add their own by default,
# and this store was tested with the required ones only. The keys are passed by
# name, so they appear in no process list.
AWS_ACCESS_KEY_ID="$BACKUP_USER" AWS_SECRET_ACCESS_KEY="$STORAGE_BACKUP_PASSWORD" \
docker run --rm --network "$NETWORK" \
  -v "$BACKUP_DIR/$TIMESTAMP:/backup" \
  -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY \
  -e AWS_DEFAULT_REGION=us-east-1 \
  -e AWS_REQUEST_CHECKSUM_CALCULATION=when_required \
  --entrypoint sh \
  amazon/aws-cli:2.37.9@sha256:92de75724b6a746951f0e8b915d86bbccd7cb55aff96cd0cb4f7017272160780 \
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
