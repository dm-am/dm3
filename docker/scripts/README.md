# Backup Scripts

This directory contains automated backup scripts for DM3 infrastructure components.

## Available Scripts

- **backup-postgres.sh** - Backs up PostgreSQL database using pg_dump
- **backup-mongodb.sh** - Backs up MongoDB using mongodump
- **backup-minio.sh** - Backs up MinIO object storage using mc mirror

## Usage

### Manual Backup

```bash
# PostgreSQL
./backup-postgres.sh

# MongoDB
./backup-mongodb.sh

# MinIO
./backup-minio.sh
```

### Custom Configuration

Override environment variables:

```bash
# PostgreSQL
BACKUP_DIR=/custom/path RETENTION_DAYS=60 ./backup-postgres.sh

# MongoDB
CONTAINER_NAME=my-mongo RETENTION_DAYS=45 ./backup-mongodb.sh

# MinIO
BACKUP_DIR=/mnt/storage BUCKET_NAME=my-bucket ./backup-minio.sh
```

## Automated Backups

See [docs/infrastructure/BACKUP_STRATEGY.md](../../docs/infrastructure/BACKUP_STRATEGY.md) for cron setup and full backup strategy documentation.

## Permissions

Make scripts executable:

```bash
chmod +x backup-*.sh
```

## Requirements

- Docker must be running
- Containers must be named as expected (dm-pg, dm-mongo) or override via environment variables
- Sufficient disk space in backup directories
- For MinIO backups, mc client must be configured with alias
