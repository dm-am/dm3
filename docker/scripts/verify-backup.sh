#!/bin/bash
set -euo pipefail

MAX_AGE_HOURS="${MAX_AGE_HOURS:-48}"
MIN_SIZE_BYTES="${MIN_SIZE_BYTES:-1024}"
EXIT_CODE=0

check_backup_dir() {
    local DIR="$1"
    local LABEL="$2"
    local PATTERN="$3"

    if [ ! -d "$DIR" ]; then
        echo "WARNING: $LABEL backup directory not found: $DIR"
        EXIT_CODE=1
        return
    fi

    LATEST=$(find "$DIR" -name "$PATTERN" -type f -printf '%T@ %p\n' 2>/dev/null | sort -rn | head -1)

    if [ -z "$LATEST" ]; then
        echo "ERROR: No $LABEL backups found in $DIR"
        EXIT_CODE=2
        return
    fi

    LATEST_FILE=$(echo "$LATEST" | cut -d' ' -f2)
    LATEST_TIME=$(echo "$LATEST" | cut -d' ' -f1 | cut -d. -f1)
    NOW=$(date +%s)
    AGE_HOURS=$(( (NOW - LATEST_TIME) / 3600 ))
    FILE_SIZE=$(stat -c%s "$LATEST_FILE" 2>/dev/null || stat -f%z "$LATEST_FILE" 2>/dev/null)

    echo "--- $LABEL ---"
    echo "  Latest: $(basename "$LATEST_FILE")"
    echo "  Age: ${AGE_HOURS}h"
    echo "  Size: $(du -h "$LATEST_FILE" | cut -f1)"

    if [ "$AGE_HOURS" -gt "$MAX_AGE_HOURS" ]; then
        echo "  WARNING: Backup is older than ${MAX_AGE_HOURS}h!"
        EXIT_CODE=1
    fi

    if [ "$FILE_SIZE" -lt "$MIN_SIZE_BYTES" ]; then
        echo "  ERROR: Backup is suspiciously small (${FILE_SIZE} bytes)!"
        EXIT_CODE=2
    fi

    # Verify gzip integrity
    if echo "$LATEST_FILE" | grep -q '\.gz$'; then
        if gunzip -t "$LATEST_FILE" 2>/dev/null; then
            echo "  Integrity: OK"
        else
            echo "  ERROR: Backup file is corrupted!"
            EXIT_CODE=2
        fi
    fi
}

echo "=== Backup Verification ==="
echo "Max age: ${MAX_AGE_HOURS}h | Min size: ${MIN_SIZE_BYTES} bytes"
echo ""

check_backup_dir "${PG_BACKUP_DIR:-/var/backups/postgresql}" "PostgreSQL" "*.sql.gz"
echo ""
check_backup_dir "${MONGO_BACKUP_DIR:-/var/backups/mongodb}" "MongoDB" "*.archive.gz"
echo ""

echo "=== Result ==="
if [ "$EXIT_CODE" -eq 0 ]; then
    echo "All backups OK"
elif [ "$EXIT_CODE" -eq 1 ]; then
    echo "Warnings detected (see above)"
else
    echo "ERRORS detected (see above)"
fi

exit $EXIT_CODE
