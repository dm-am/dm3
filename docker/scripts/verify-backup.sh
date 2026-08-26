#!/bin/bash
set -euo pipefail

# Cron дает почти пустое окружение: без этого ночной запуск не видел ни пароля
# MinIO, ни ключей offsite-репликации.
# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

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

    # -f2- and not -f2, the same split check_backup_tree below already uses: the
    # second field is a path, and cutting it at the first space hands every check
    # that follows the name of a file that does not exist.
    LATEST_FILE=$(echo "$LATEST" | cut -d' ' -f2-)
    LATEST_TIME=$(echo "$LATEST" | cut -d' ' -f1 | cut -d. -f1)
    NOW=$(date +%s)
    AGE_HOURS=$(( (NOW - LATEST_TIME) / 3600 ))
    # GNU stat, and no BSD fallback, because a BSD fallback could never run: the
    # find above is called with -printf, which only GNU find has, so on a host
    # without it LATEST is empty and the function has already returned. The error
    # is left on stderr - set -e ends the run either way, and the reason belongs
    # in the log.
    FILE_SIZE=$(stat -c%s "$LATEST_FILE")

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

    # Whole, not merely well formed. A dump cut off halfway is a valid gzip of a
    # valid prefix: it decompresses, it is far over a kilobyte, and its timestamp
    # is the newest in the directory — every check above says yes, and the file a
    # restore would reach for first stops mid-table. The marker below is the last
    # line pg_dump writes.
    if echo "$LATEST_FILE" | grep -q '\.sql\.gz$'; then
        if gunzip -c "$LATEST_FILE" 2>/dev/null | tail -c 4096 | grep -q "PostgreSQL database dump complete"; then
            echo "  Completeness: OK"
        else
            echo "  ERROR: Backup is truncated — pg_dump did not finish!"
            EXIT_CODE=2
        fi
    fi
}

# MinIO пишет не файл, а каталог на запуск: mc mirror раскладывает объекты
# бакета как есть. Проверка по -type f его не видела вовсе, поэтому сторож
# рапортовал "All backups OK", пока бэкапов загрузок не существовало ни одного.
check_backup_tree() {
    local DIR="$1"
    local LABEL="$2"

    if [ ! -d "$DIR" ]; then
        echo "WARNING: $LABEL backup directory not found: $DIR"
        EXIT_CODE=1
        return
    fi

    LATEST=$(find "$DIR" -mindepth 1 -maxdepth 1 -type d -printf '%T@ %p\n' 2>/dev/null |
        sort -rn | head -1)

    if [ -z "$LATEST" ]; then
        echo "ERROR: No $LABEL backups found in $DIR"
        EXIT_CODE=2
        return
    fi

    LATEST_DIR=$(echo "$LATEST" | cut -d' ' -f2-)
    LATEST_TIME=$(echo "$LATEST" | cut -d' ' -f1 | cut -d. -f1)
    AGE_HOURS=$(( ($(date +%s) - LATEST_TIME) / 3600 ))
    SIZE=$(du -sb "$LATEST_DIR" | cut -f1)
    FILES=$(find "$LATEST_DIR" -type f | wc -l)

    echo "$LABEL: $LATEST_DIR (${AGE_HOURS}h old, ${SIZE} bytes, ${FILES} files)"

    if [ "$AGE_HOURS" -gt "$MAX_AGE_HOURS" ]; then
        echo "ERROR: $LABEL backup is older than ${MAX_AGE_HOURS}h"
        EXIT_CODE=2
    fi

    # Пустой каталог — это успешно отработавший mirror по пустому бакету или
    # молча провалившийся по непустому. Отличить их отсюда нельзя, поэтому
    # предупреждение, а не ошибка.
    if [ "$FILES" -eq 0 ]; then
        echo "WARNING: $LABEL backup contains no files"
        [ "$EXIT_CODE" -eq 0 ] && EXIT_CODE=1
    fi
}

echo "=== Backup Verification ==="
echo "Max age: ${MAX_AGE_HOURS}h | Min size: ${MIN_SIZE_BYTES} bytes"
echo ""

check_backup_dir "${PG_BACKUP_DIR:-/var/backups/postgresql}" "PostgreSQL" "*.sql.gz"
echo ""
check_backup_tree "${MINIO_BACKUP_DIR:-/var/backups/minio}" "MinIO"
echo ""

echo "=== Result ==="
if [ "$EXIT_CODE" -eq 0 ]; then
    echo "All backups OK"
elif [ "$EXIT_CODE" -eq 1 ]; then
    echo "Warnings detected (see above)"
else
    echo "ERRORS detected (see above)"
fi

# The verdict goes somewhere a human will meet it. Run from cron, this script's
# exit code went into a log file and into mail for a machine with no MTA, so a
# missing or corrupted backup announced itself for the first time at the restore.
# Written for the node-exporter textfile collector the stack already runs, which
# is what turns it into an alert; the directory is created by install-cron.sh, and
# a machine without it simply skips this.
if [ -n "${TEXTFILE_DIR:-/var/lib/node_exporter/textfile}" ] &&
   [ -d "${TEXTFILE_DIR:-/var/lib/node_exporter/textfile}" ]; then
    METRICS_DIR="${TEXTFILE_DIR:-/var/lib/node_exporter/textfile}"
    TMP_METRICS="$(mktemp "$METRICS_DIR/.dm_backup.XXXXXX")"
    {
        echo "# HELP dm_backup_verification_status Result of the nightly backup check: 0 ok, 1 warning, 2 error."
        echo "# TYPE dm_backup_verification_status gauge"
        echo "dm_backup_verification_status $EXIT_CODE"
        echo "# HELP dm_backup_verification_timestamp_seconds When the check last finished."
        echo "# TYPE dm_backup_verification_timestamp_seconds gauge"
        echo "dm_backup_verification_timestamp_seconds $(date +%s)"
    } > "$TMP_METRICS"
    # Moved into place, so the collector never reads a half-written file.
    mv "$TMP_METRICS" "$METRICS_DIR/dm_backup.prom"
fi

exit $EXIT_CODE
