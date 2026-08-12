#!/bin/bash
set -euo pipefail

# Creates the basic-auth file the preview overlay mounts into nginx.
#
# The file is not in the repository and must not get there: git keeps every
# version of what it once tracked, and .gitignore does nothing for a path
# already in the index, so a hash committed once is public from then on. It is
# generated here, on the host, by whoever knows the password.
#
# Two callers: setup-server.sh during install, and the operator changing the
# password by hand. nginx re-reads the file only on restart.

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

# bcrypt (-B) instead of the apr1 default: apr1 is a thousand rounds of md5, so
# a leaked file is minutes of offline guessing. The cost stays at the tool
# default because nginx recomputes the hash on every request with no cache, which
# makes the work factor the latency of every asset the stand serves as well. The
# password goes in over stdin (-i) so that it never reaches the process table.
#
# The image names a version on purpose. Untagged means latest, and latest is
# whatever the registry holds on the day an operator resets the password: a
# different tool, on the one line that mints the credentials of the stand.
# Moving this tag is a manual job - dependabot parses compose files and
# Dockerfiles, never shell, so the docker ecosystem entries in
# .github/dependabot.yml do not reach this script. What does watch the line is
# PinEveryImageTheDeploymentRuns, which reads the scripts along with the compose
# files.
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
