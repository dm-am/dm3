#!/bin/bash
set -euo pipefail

# The installer installs and does not update. Run again, it would lay a second layout over the
# first after apt and a docker restart on a live server. Checked first, for every directory of
# the layout: the releases and the shared files, the pins, the state of the release script.
#
# Usage: require-empty-install-dir.sh [dir...]   (default /opt/dm3)

[ "$#" -gt 0 ] || set -- /opt/dm3

occupied=()
for dir in "$@"; do
    if [ -e "$dir" ] && [ -n "$(ls -A "$dir" 2> /dev/null)" ]; then
        occupied+=("$dir")
    fi
done

if [ "${#occupied[@]}" -eq 0 ]; then
    exit 0
fi

{
    for dir in "${occupied[@]}"; do
        echo "$dir уже существует и не пуст."
    done
    cat << 'EOF'

Установщик ставит сервер с нуля и обновлением не служит. Обновления приходят
подписанными релизами: что стоит сейчас, показывает sudo dm3-release status,
а релиз, который таймер не берет сам, ставит sudo dm3-release maintain <тег>.

Если сервер ставится действительно заново, каталоги надо освободить руками и
сначала забрать из них то, что ведется на месте: /opt/dm3/shared (файл
окружения и файлы края) и /etc/dm3 (ключи подписи и release.conf).
EOF
} >&2
exit 1
