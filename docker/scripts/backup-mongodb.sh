#!/bin/bash
set -e
BACKUP_DIR="${BACKUP_DIR:-/var/backups/mongodb}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
CONTAINER="${CONTAINER_NAME:-dm-mongo}"

mkdir -p "$BACKUP_DIR"
echo "[$(date)] Starting MongoDB backup..."
docker exec "$CONTAINER" mongodump --archive | gzip > "$BACKUP_DIR/mongodb_$TIMESTAMP.archive.gz"
echo "[$(date)] Backup created: mongodb_$TIMESTAMP.archive.gz"

find "$BACKUP_DIR" -name "*.archive.gz" -mtime +$RETENTION_DAYS -delete
echo "[$(date)] Cleaned up backups older than $RETENTION_DAYS days"
