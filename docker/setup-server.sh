#!/bin/bash
set -euo pipefail

# DM3 server setup script for clean Ubuntu
# Usage: DM_REF=<tag>
#   curl -sSL https://raw.githubusercontent.com/dm-am/dm3/$DM_REF/docker/setup-server.sh | DM_REF=$DM_REF bash
# The ref is written once and used twice: the script that is downloaded and the
# tree that is cloned are then the same thing by construction.

INSTALL_DIR="/opt/dm3"
# What to install from, and with it the tag of the images this stand runs: latest is
# published only from main, so a stand cloned from dev has to ask for the dev images. A
# tag is the intended value, since a branch moves and two runs a day apart give two stands.
DM_REF="${DM_REF:-dev}"

# Asked before anything is installed: a repeat run used to die at the clone, after apt and
# a docker restart on a live stand.
bash "$(dirname "$0")/scripts/require-empty-install-dir.sh" "$INSTALL_DIR"

echo "=== Установка Docker ==="
sudo apt update
sudo apt install -y docker.io docker-compose-plugin git iptables-persistent openssl certbot

echo "=== Настройка Docker для текущего пользователя ==="
sudo usermod -aG docker "$USER"

echo "=== Ротация журналов Docker ==="
# The default json-file driver keeps every line forever and Serilog also writes to the
# console, so a full disk kills Postgres first. Set on the daemon, which also covers the
# one-off containers, and it applies to containers created after it.
DOCKER_DAEMON_CONFIG=/etc/docker/daemon.json
if [ -f "$DOCKER_DAEMON_CONFIG" ]; then
    echo "$DOCKER_DAEMON_CONFIG уже существует, ротацию журналов проверить вручную:"
    echo "  log-driver json-file, log-opts max-size 10m, max-file 3"
else
    sudo mkdir -p "$(dirname "$DOCKER_DAEMON_CONFIG")"
    sudo tee "$DOCKER_DAEMON_CONFIG" > /dev/null <<'JSON'
{
  "log-driver": "json-file",
  "log-opts": {
    "max-size": "10m",
    "max-file": "3"
  }
}
JSON
    sudo systemctl restart docker
fi

echo "=== Клонирование репозитория ==="
sudo mkdir -p "$INSTALL_DIR"
sudo chown "$USER:$USER" "$INSTALL_DIR"
git clone --branch "$DM_REF" https://github.com/dm-am/dm3.git "$INSTALL_DIR"

# Which commit this stand is; a ref that turns out to be a branch is said out loud.
INSTALLED_COMMIT="$(git -C "$INSTALL_DIR" rev-parse --short HEAD)"
if git -C "$INSTALL_DIR" show-ref --verify --quiet "refs/tags/$DM_REF"; then
    echo "Установлено из тега $DM_REF ($INSTALLED_COMMIT)."
else
    echo "ВНИМАНИЕ: $DM_REF это ветка, а не тег. Установлен коммит $INSTALLED_COMMIT," >&2
    echo "и завтра та же команда даст другой. Для воспроизводимой установки:" >&2
    echo "  DM_REF=<тег> curl -sSL .../setup-server.sh | bash" >&2
fi

echo "=== Подготовка docker/.env ==="
# Everything below needs this file: the backup scripts source it and compose stops at
# ${...:?} without it.
bash "$INSTALL_DIR/docker/scripts/init-env.sh" server "$DM_REF"

echo "=== Открытие портов (firewall) ==="
sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save

echo "=== Настройка preview-пароля ==="
# A clone carries no credentials: the file is generated here, and the generator holds the
# unset-password check so a later change of the password gets the same one.
bash "$INSTALL_DIR/docker/scripts/init-htpasswd.sh"

echo "=== Установка systemd-сервиса ==="
sudo cp "$INSTALL_DIR/docker/dm3.service" /etc/systemd/system/dm3.service
sudo systemctl daemon-reload
sudo systemctl enable dm3.service

echo "=== Установка cron-задач для бэкапов ==="
sudo bash "$INSTALL_DIR/docker/scripts/install-cron.sh" "$INSTALL_DIR"

echo "=== Запуск приложения ==="
cd "$INSTALL_DIR/docker"
# The object store first and with --wait: the site starts without it, so this is the one
# step a failed bucket creation stops, instead of every upload answering 500.
sudo docker compose --profile production -f docker-compose.yml -f docker-compose.preview.yml up -d --wait storage seaweedfs
# No --build: CI builds the four images and the server pulls them; building here costs
# gigabytes of disk and memory on a small VPS. The production profile adds watchtower.
sudo docker compose --profile production -f docker-compose.yml -f docker-compose.preview.yml up -d

echo ""
echo "=== Готово! ==="
echo "Приложение доступно по адресу: http://$(curl -s ifconfig.me)"
echo "Логин: preview"
# The password is not printed: it would stay in the terminal, the session history and the
# install log, and under set -u it crashed this step when typed at the init-htpasswd.sh prompt.
echo ""
echo "Стенд отвечает по http: сертификата пока нет, самоподписанного не будет —"
echo "предупреждение браузера учит кликать сквозь предупреждения."
echo "Когда появится домен:"
echo "  bash $INSTALL_DIR/docker/scripts/init-ssl.sh <домен>"
echo "  sudo systemctl restart dm3"
echo "  sudo certbot renew --dry-run"
echo ""
echo "Для смены пароля:"
echo "  bash $INSTALL_DIR/docker/scripts/init-htpasswd.sh"
echo "  cd $INSTALL_DIR/docker && sudo docker compose restart nginx"
echo ""
echo "Сервис автозапуска: systemctl status dm3"
echo "Бэкапы: ежедневно в 02:00-04:00, лог /var/log/dm3-backup.log"
