#!/bin/bash
set -euo pipefail

# Install backup cron jobs for DM3
# Usage: sudo bash install-cron.sh [install_dir]
#   install_dir applies from a checkout (default /opt/dm3). From a release it is ignored: the
#   lines go through rel/current, so they follow every release without a reinstall.

# shellcheck source=docker/scripts/_paths.sh
. "$(dirname "${BASH_SOURCE[0]}")/_paths.sh" "${1:-}"
SCRIPT_DIR="$DM_CRON_SCRIPTS_DIR"
LOG_FILE="/var/log/dm3-backup.log"
# Movable for check-server-installer.sh, which runs this without root.
LOG_FILE="${DM3_BACKUP_LOG:-$LOG_FILE}"

if [ ! -d "$SCRIPT_DIR" ]; then
    echo "ERROR: Script directory not found: $SCRIPT_DIR" >&2
    echo "Usage: sudo bash install-cron.sh [install_dir]" >&2
    exit 1
fi

# Ensure log file exists and is writable
touch "$LOG_FILE"

# Where verify-backup.sh leaves its verdict for node-exporter: cron mail reached nobody,
# since the server has no MTA.
TEXTFILE_DIR="${TEXTFILE_DIR:-/var/lib/node_exporter/textfile}"
mkdir -p "$TEXTFILE_DIR"

# Install crontab entries
CRON_MARKER="# DM3 automated backups"
# verify-backup.sh runs last: a backup nobody verifies shows its first fault at the restore.
# Through bash rather than by the executable bit, which an archive or a Windows checkout can
# drop without anybody seeing the job fail.
CRON_JOBS="$CRON_MARKER
0 2 * * * bash $SCRIPT_DIR/backup-postgres.sh >> $LOG_FILE 2>&1
0 4 * * * bash $SCRIPT_DIR/backup-storage.sh >> $LOG_FILE 2>&1
0 5 * * * bash $SCRIPT_DIR/verify-backup.sh >> $LOG_FILE 2>&1"

# Remove old DM3 cron entries if present, then add new ones. crontab -l fails on a root with no
# table and grep on a table of DM3's lines only; under pipefail either would end the subshell
# before the echo and install an empty table.
({ crontab -l 2>/dev/null || true; } | { grep -v -e "$CRON_MARKER" -e "backup-postgres.sh" -e "backup-storage.sh" -e "verify-backup.sh" || true; }
    echo "$CRON_JOBS") | crontab -

echo "Backup cron jobs installed:"
echo "  02:00 - PostgreSQL backup"
echo "  04:00 - Object storage backup"
echo "  05:00 - Verify last night's backups (non-zero exit on failure)"
echo "  Log file: $LOG_FILE"

# Setup logrotate
LOGROTATE_CONF="${DM3_LOGROTATE_CONF:-/etc/logrotate.d/dm3-backup}"
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
