#!/bin/bash
set -euo pipefail

# The installer installs and does not update. Run again, it used to reach `git clone` into
# a non-empty directory after apt and a docker restart on a live stand. Checked first.
# Deliberately not a fetch-and-merge: the server tree carries the operator's own edits (the
# environment file, the edge credentials), and a checkout would take them away.

INSTALL_DIR="${1:-/opt/dm3}"

if [ ! -e "$INSTALL_DIR" ]; then
    exit 0
fi

if [ -z "$(ls -A "$INSTALL_DIR" 2>/dev/null)" ]; then
    exit 0
fi

cat >&2 <<EOF
$INSTALL_DIR уже существует и не пуст.

Установщик ставит стенд с нуля и обновлением не служит. Что делать вместо
повторного запуска — в разделе "Обновление стенда" файла docs/guides/DEPLOYMENT.md:
образы приложений носит watchtower, дерево обновляет оператор, а контейнеры,
читающие конфигурацию с диска, после этого перезапускаются.

Если стенд ставится действительно заново, каталог надо освободить руками — и
сначала забрать из него то, что там ведется на месте: docker/.env, учетные
данные края и локальные правки конфигурации.
EOF
exit 1
