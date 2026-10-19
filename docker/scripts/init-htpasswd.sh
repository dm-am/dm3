#!/bin/bash
set -euo pipefail

# Creates the basic-auth file the preview overlay mounts into nginx. It stays out of the
# repository: git keeps every version of what it once tracked, so a hash committed once is
# public. Generated on the host, by setup-server.sh and by the operator changing the
# password. nginx re-reads the file only on restart.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(dirname "$SCRIPT_DIR")"
HTPASSWD_FILE="$DOCKER_DIR/nginx/.htpasswd"
PREVIEW_USER="preview"

if [ -z "${DM_PREVIEW_PASSWORD:-}" ]; then
    if [ -t 0 ]; then
        # Asked for rather than taken as an argument: an inline assignment stays
        # in the shell history and an argument is visible in the process table.
        read -rsp "Пароль для входа на стенд (логин $PREVIEW_USER): " DM_PREVIEW_PASSWORD
        echo
    else
        # curl | bash has no terminal to ask on: stdin is the script itself.
        echo "ОШИБКА: DM_PREVIEW_PASSWORD не задан." >&2
        echo "Для установки: export DM_PREVIEW_PASSWORD=ваш_пароль" >&2
        exit 1
    fi
fi

if [ -z "$DM_PREVIEW_PASSWORD" ]; then
    echo "ОШИБКА: пустой пароль." >&2
    exit 1
fi

# The installer adds the user to the docker group in the very session that has
# to run this, and group membership only takes effect on the next login.
DOCKER=(docker)
if ! docker info >/dev/null 2>&1; then
    DOCKER=(sudo docker)
fi

# bcrypt (-B) instead of the apr1 default of a thousand md5 rounds. The cost stays at the
# tool default because nginx recomputes the hash on every request. The password goes in
# over stdin (-i), away from the process table.
#
# The image names a version because latest is a different tool on the day a password is
# reset. Dependabot does not read shell, so moving this tag is manual;
# PinEveryImageTheDeploymentRuns watches the line.
TMP_FILE="$(mktemp)"
trap 'rm -f "$TMP_FILE"' EXIT
printf '%s\n' "$DM_PREVIEW_PASSWORD" \
    | "${DOCKER[@]}" run --rm -i httpd:2.4.68 htpasswd -niB "$PREVIEW_USER" > "$TMP_FILE"

# Written aside and moved into place: a redirect straight at the target empties
# the file that works before the tool that would replace it has run, and an empty
# auth file refuses every login.
[ -s "$TMP_FILE" ] || { echo "ОШИБКА: htpasswd вернул пустой результат." >&2; exit 1; }
mkdir -p "$(dirname "$HTPASSWD_FILE")"
mv "$TMP_FILE" "$HTPASSWD_FILE"
# mktemp creates the file with 600, and nginx workers read it on every request as
# an unprivileged user.
chmod 644 "$HTPASSWD_FILE"

echo "Учетные данные записаны в $HTPASSWD_FILE (логин $PREVIEW_USER)."
