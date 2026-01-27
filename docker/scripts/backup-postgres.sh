#!/bin/bash
set -e
BACKUP_DIR="${BACKUP_DIR:-/var/backups/postgresql}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
CONTAINER="${CONTAINER_NAME:-dm-pg}"
DATABASE="${DATABASE_NAME:-dm3.5}"

mkdir -p "$BACKUP_DIR"
echo "[$(date)] Starting PostgreSQL backup..."
docker exec "$CONTAINER" pg_dump -U postgres "$DATABASE" | gzip > "$BACKUP_DIR/${DATABASE}_$TIMESTAMP.sql.gz"
echo "[$(date)] Backup created: ${DATABASE}_$TIMESTAMP.sql.gz"

# Cleanup old backups
find "$BACKUP_DIR" -name "*.sql.gz" -mtime +$RETENTION_DAYS -delete
echo "[$(date)] Cleaned up backups older than $RETENTION_DAYS days"
