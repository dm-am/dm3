#!/bin/bash
# Loads docker/.env for a script started by cron.
#
# Cron gives a job almost no environment: no PATH beyond the bare minimum, and
# nothing from the shell that installed the crontab. Every backup script here
# requires credentials — MINIO_ROOT_PASSWORD, and the AWS variables that switch
# offsite replication on — and the MinIO one exits 1 when its password is unset.
# So the nightly run failed every night, and the only watchman did not look at
# MinIO at all and kept reporting that everything was fine.
#
# Sourced by the scripts rather than written into the crontab line so that it
# holds however the script is started: by cron, by hand, or from the installer.
#
# Values already in the environment win: a run that sets BACKUP_DIR or points at
# another host must not be overwritten by the file.

_ENV_FILE="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/.env"

if [ -f "$_ENV_FILE" ]; then
    while IFS= read -r _line || [ -n "$_line" ]; do
        case "$_line" in
            ''|'#'*) continue ;;
        esac
        _key="${_line%%=*}"
        # Не строка вида KEY=VALUE — пропускаем, а не пытаемся разобрать.
        [ "$_key" = "$_line" ] && continue
        _key="${_key#export }"
        _key="$(echo "$_key" | tr -d '[:space:]')"
        [ -z "$_key" ] && continue
        # Уже задано снаружи — не трогаем.
        [ -n "${!_key+x}" ] && continue
        _value="${_line#*=}"
        _value="${_value%\"}"; _value="${_value#\"}"
        _value="${_value%\'}"; _value="${_value#\'}"
        export "$_key=$_value"
    done < "$_ENV_FILE"
fi

unset _ENV_FILE _line _key _value
