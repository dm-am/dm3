#!/bin/bash
set -euo pipefail

# The layout part of the server installer (release design 3.2): the account that owns the
# mirror, the directories, the signer's key and the origin pinned in /etc/dm3, the shared
# environment and edge files, the release script, and then the first release through the same
# verified path as every later one, dm3-release maintain. setup-server.sh runs it after the host
# part; it also runs alone on a host that has the packages already, as on the deployment-smoke
# runner.
#
# Usage, as root, from a checkout of the verified tag:
#   install-release.sh --signers <allowed_signers> --tag <vX.Y.Z> [--origin <url>]
#                      --mail-host <host> --mail-port <port> [--image-prefix <prefix>] [--no-timer]
#
# --mail-host and --mail-port go into shared/env on its first run, so the installer finishes in
# one go. --no-timer enables the release timer without starting it, for a machine where a tick
# would race a test.

SIGNERS_FILE=""
TAG=""
ORIGIN="https://github.com/dm-am/dm3.git"
IMAGE_PREFIX="ghcr.io/dm-am/"
MAIL_HOST_GIVEN=""
MAIL_PORT_GIVEN=""
START_TIMER=1

usage() {
    echo "Usage: install-release.sh --signers <allowed_signers> --tag <vX.Y.Z> --mail-host <host> --mail-port <port> [--origin <url>] [--image-prefix <prefix>] [--no-timer]" >&2
    exit 2
}

while [ "$#" -gt 0 ]; do
    case "$1" in
        --signers | --tag | --origin | --image-prefix | --mail-host | --mail-port)
            [ "$#" -ge 2 ] || { echo "Error: $1 needs a value" >&2; usage; }
            case "$1" in
                --signers) SIGNERS_FILE=$2 ;;
                --tag) TAG=$2 ;;
                --origin) ORIGIN=$2 ;;
                --image-prefix) IMAGE_PREFIX=$2 ;;
                --mail-host) MAIL_HOST_GIVEN=$2 ;;
                --mail-port) MAIL_PORT_GIVEN=$2 ;;
            esac
            shift 2
            ;;
        --no-timer) START_TIMER=0; shift ;;
        *) echo "Error: unknown argument $1" >&2; usage ;;
    esac
done

# Everything is asked before anything is touched: a refusal halfway would leave a layout the
# guard then refuses to install over.
[ -n "$TAG" ] || { echo "Error: --tag names the release to install" >&2; usage; }
[ -n "$SIGNERS_FILE" ] || { echo "Error: --signers names the allowed_signers file of the release key" >&2; usage; }
[ -s "$SIGNERS_FILE" ] || { echo "Error: --signers $SIGNERS_FILE is not a file with a key in it" >&2; exit 1; }
# Without the relay init-env.sh server stops after the account, the directories and the pins.
if [ -z "$MAIL_HOST_GIVEN" ] || [ -z "$MAIL_PORT_GIVEN" ]; then
    echo "Error: --mail-host and --mail-port name the mail relay of the server" >&2
    usage
fi
[ "$(id -u)" -eq 0 ] || { echo "Error: install-release.sh runs as root" >&2; exit 1; }

DM3_ROOT="${DM3_ROOT:-/opt/dm3}"
DM3_ETC="${DM3_ETC:-/etc/dm3}"
DM3_STATE="${DM3_STATE:-/var/lib/dm3}"
export DM3_ROOT DM3_ETC DM3_STATE
HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SBIN=/usr/local/sbin/dm3-release
RELEASE_USER=dm-release

bash "$HERE/require-empty-install-dir.sh" "$DM3_ROOT" "$DM3_ETC" "$DM3_STATE"

echo "=== Учетная запись $RELEASE_USER ==="
# git runs only as this account: a repository a writer filled is read with its rights, not root's.
if ! id -u "$RELEASE_USER" > /dev/null 2>&1; then
    useradd --system --home-dir "$DM3_STATE" --no-create-home --shell /usr/sbin/nologin "$RELEASE_USER"
fi

echo "=== Каталоги $DM3_ROOT, $DM3_ETC, $DM3_STATE ==="
install -d -m 755 "$DM3_ROOT" "$DM3_ROOT/rel" "$DM3_ROOT/shared" "$DM3_ETC" "$DM3_STATE"
install -d -m 755 "$DM3_ROOT/shared/edge" "$DM3_ROOT/shared/edge/tls" "$DM3_ROOT/shared/edge/acme"
install -d -m 755 -o "$RELEASE_USER" -g "$RELEASE_USER" "$DM3_ROOT/repo.git" "$DM3_STATE/scratch"
# As dm3-release runs git: its own home, since under sudo HOME is still /root.
(cd / && runuser -u "$RELEASE_USER" -- env -u XDG_CONFIG_HOME HOME="$DM3_STATE" git init -q --bare "$DM3_ROOT/repo.git")

echo "=== Ключ подписи и источник релизов ==="
install -m 644 "$SIGNERS_FILE" "$DM3_ETC/release-signers"
printf 'origin=%s\nimage_prefix=%s\n' "$ORIGIN" "$IMAGE_PREFIX" > "$DM3_ETC/release.conf"
chmod 644 "$DM3_ETC/release.conf"

echo "=== Файл окружения $DM3_ROOT/shared/env ==="
# The relay goes in through the environment, which init-env.sh writes into the file it creates:
# a file written here first would count as existing, and no credentials would be generated.
DM_LAYOUT_ROOT="$DM3_ROOT" MAIL_HOST="$MAIL_HOST_GIVEN" MAIL_PORT="$MAIL_PORT_GIVEN" \
    bash "$HERE/init-env.sh" server

echo "=== Пароль входа на стенд ==="
DM_LAYOUT_ROOT="$DM3_ROOT" bash "$HERE/init-htpasswd.sh"

echo "=== Скрипт релиза и первый релиз $TAG ==="
# Renamed into place: a later maintain replaces the script it is running from the same way.
install -m 755 "$HERE/release-entry.sh" "$SBIN.new"
mv -f "$SBIN.new" "$SBIN"
# maintain installs the units and the cron lines from the release itself.
"$SBIN" maintain "$TAG"

echo "=== Юниты systemd ==="
# dm3 is started as well, so the unit is active and its stop runs at shutdown.
systemctl enable --now dm3.service
if [ "$START_TIMER" = 1 ]; then
    systemctl enable --now dm3-release.timer
else
    systemctl enable dm3-release.timer
fi

echo ""
echo "=== Готово ==="
echo "Живой релиз и состояние контейнеров: sudo dm3-release status"
echo "Логин стенда: preview. Пароль не печатается, он остается в истории и журнале установки."
echo "Смена пароля: sudo DM_LAYOUT_ROOT=$DM3_ROOT bash docker/scripts/init-htpasswd.sh,"
echo "  затем sudo docker restart dm-nginx: край читает файл только при старте"
echo "Бэкапы: ежедневно в 02:00-04:00, лог /var/log/dm3-backup.log"
