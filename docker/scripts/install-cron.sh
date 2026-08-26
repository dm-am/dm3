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

# Where verify-backup.sh leaves its verdict for node-exporter to publish. The
# exit code alone reached nobody: cron mails it, and the server has no MTA.
TEXTFILE_DIR="${TEXTFILE_DIR:-/var/lib/node_exporter/textfile}"
mkdir -p "$TEXTFILE_DIR"

# Install crontab entries
CRON_MARKER="# DM3 automated backups"
# verify-backup.sh runs last and exits non-zero when an artefact is missing,
# tiny or corrupted. A backup nobody verifies is not a backup: without this
# entry the first sign of trouble is a failed restore.
CRON_JOBS="$CRON_MARKER
0 2 * * * $SCRIPT_DIR/backup-postgres.sh >> $LOG_FILE 2>&1
0 4 * * * $SCRIPT_DIR/backup-minio.sh >> $LOG_FILE 2>&1
0 5 * * * $SCRIPT_DIR/verify-backup.sh >> $LOG_FILE 2>&1"

# Remove old DM3 cron entries if present, then add new ones
(crontab -l 2>/dev/null | grep -v "$CRON_MARKER" | grep -v "backup-postgres.sh" | grep -v "backup-minio.sh" | grep -v "verify-backup.sh"; echo "$CRON_JOBS") | crontab -

echo "Backup cron jobs installed:"
echo "  02:00 - PostgreSQL backup"
echo "  04:00 - MinIO backup"
echo "  05:00 - Verify last night's backups (non-zero exit on failure)"
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
