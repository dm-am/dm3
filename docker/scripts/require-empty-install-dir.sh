#!/bin/bash
set -euo pipefail

# The installer installs; it does not update.
#
# Run a second time it reached `git clone` into a directory that already had a
# tree in it, and died there - under `set -e`, after having installed packages
# and restarted the docker daemon on a running stand. The failure was correct
# and the order was not: whoever ran it looking for an update got a bounced
# daemon and a message about a non-empty directory.
#
# Checked first, then, and with the answer to the question actually being asked.
#
# Deliberately not a fetch-and-merge: the tree on a server carries the operator's
# own edits - the environment file, the edge credentials, local configuration -
# and a checkout would take them away without a word.

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
