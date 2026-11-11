#!/bin/bash
set -euo pipefail

# Puts a copy made by backup-storage.sh back into the object storage bucket.
#
# The application key (dm-app) writes, and it has no List right: so `cp --recursive` and not
# `sync`, which reads the bucket listing first. The key goes into the throwaway container by
# name, since a value in the arguments of `docker run` is visible in `ps` to every user of the
# server. The type of a file is taken by aws-cli from its extension: the copy keeps no
# metadata, so the cache header of the uploads is set again.
#
# Usage: restore-storage.sh <directory of the copy>
# For example: bash docker/scripts/restore-storage.sh /var/backups/storage/20260101_030000

# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

BUCKET="${STORAGE_BUCKET:-dm-uploads}"
APP_USER="${STORAGE_APP_USER:-dm-app}"
ENDPOINT="${STORAGE_ENDPOINT:-http://127.0.0.1:9000}"
NETWORK="${STORAGE_NETWORK:-host}"

if [ "$#" -ne 1 ] || [ ! -d "$1" ]; then
    echo "Использование: restore-storage.sh <каталог копии>" >&2
    echo "Например: bash $0 /var/backups/storage/20260101_030000" >&2
    exit 1
fi

if [ -z "${STORAGE_APP_PASSWORD:-}" ]; then
    echo "ОШИБКА: STORAGE_APP_PASSWORD не задан (docker/.env не прочитан)." >&2
    exit 1
fi

BACKUP="$(cd "$1" && pwd)"

AWS_ACCESS_KEY_ID="$APP_USER" AWS_SECRET_ACCESS_KEY="$STORAGE_APP_PASSWORD" \
docker run --rm --network "$NETWORK" \
  -v "$BACKUP:/backup" \
  -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY \
  -e AWS_DEFAULT_REGION=us-east-1 \
  -e AWS_REQUEST_CHECKSUM_CALCULATION=when_required \
  --entrypoint sh \
  amazon/aws-cli:2.39.7@sha256:bba262340b0bc7cbf3a8a48612531263b56f4b280c4f036a07816af1b271bda0 \
  -c 'aws configure set default.s3.addressing_style path && exec aws "$@"' aws \
  s3 cp /backup "s3://$BUCKET" --recursive --endpoint-url "$ENDPOINT" \
  --cache-control "public, max-age=31536000, immutable"

echo "Копия $BACKUP возвращена в бакет $BUCKET."
