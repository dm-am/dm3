#!/bin/bash
# Loads the environment file for a script started by cron: docker/.env from a checkout,
# shared/env from a release (_paths.sh).
#
# Cron starts a job with almost no environment, and every backup script needs credentials
# (STORAGE_BACKUP_PASSWORD, the AWS variables of the offsite replication). Sourced rather
# than written into the crontab line, so it holds however the script is started. Values
# already in the environment win.

# shellcheck source=docker/scripts/_paths.sh
. "$(dirname "${BASH_SOURCE[0]}")/_paths.sh"
_ENV_FILE="$DM_ENV_FILE"

if [ -f "$_ENV_FILE" ]; then
    while IFS= read -r _line || [ -n "$_line" ]; do
        case "$_line" in
            ''|'#'*) continue ;;
        esac
        _key="${_line%%=*}"
        # Not a KEY=VALUE line.
        [ "$_key" = "$_line" ] && continue
        _key="${_key#export }"
        _key="$(echo "$_key" | tr -d '[:space:]')"
        [ -z "$_key" ] && continue
        # Already set outside.
        [ -n "${!_key+x}" ] && continue
        _value="${_line#*=}"
        _value="${_value%\"}"; _value="${_value#\"}"
        _value="${_value%\'}"; _value="${_value#\'}"
        export "$_key=$_value"
    done < "$_ENV_FILE"
fi

unset _ENV_FILE _line _key _value
