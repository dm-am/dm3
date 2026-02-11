#!/bin/bash
set -euo pipefail

# Install backup cron jobs for DM3
# Usage: sudo bash install-cron.sh [install_dir]

INSTALL_DIR="${1:-/opt/dm3}"
SCRIPT_DIR="$INSTALL_DIR/docker/scripts"
LOG_FILE="/var/log/dm3-backup.log"

if [ ! -d "$SCRIPT_DIR" ]; then
    echo "ERROR: Script directory not found: $SCRIPT_DIR" >&2
    echo "Usage: sudo bash install-cron.sh [install_dir]" >&2
    exit 1
fi

# Ensure log file exists and is writable
touch "$LOG_FILE"

# Install crontab entries
CRON_MARKER="# DM3 automated backups"
CRON_JOBS="$CRON_MARKER
0 2 * * * $SCRIPT_DIR/backup-postgres.sh >> $LOG_FILE 2>&1
0 3 * * * $SCRIPT_DIR/backup-mongodb.sh >> $LOG_FILE 2>&1
0 4 * * * $SCRIPT_DIR/backup-minio.sh >> $LOG_FILE 2>&1"

# Remove old DM3 cron entries if present, then add new ones
(crontab -l 2>/dev/null | grep -v "$CRON_MARKER" | grep -v "backup-postgres.sh" | grep -v "backup-mongodb.sh" | grep -v "backup-minio.sh"; echo "$CRON_JOBS") | crontab -

echo "Backup cron jobs installed:"
echo "  02:00 - PostgreSQL backup"
echo "  03:00 - MongoDB backup"
echo "  04:00 - MinIO backup"
echo "  Log file: $LOG_FILE"

# Setup logrotate
LOGROTATE_CONF="/etc/logrotate.d/dm3-backup"
cat > "$LOGROTATE_CONF" << 'EOF'
/var/log/dm3-backup.log {
    weekly
    rotate 4
    compress
    missingok
    notifempty
}
EOF

echo "Logrotate configured: $LOGROTATE_CONF"
