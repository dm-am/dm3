#!/bin/bash
set -euo pipefail

# DM3 server setup script for clean Ubuntu
# Usage: curl -sSL https://raw.githubusercontent.com/dm-am/dm3/dev/docker/setup-server.sh | bash

INSTALL_DIR="/opt/dm3"
# The branch to install from, and with it the tag of the images this stand runs:
# latest is published only from main, so a stand cloned from dev has to ask for
# the dev images or it runs main's containers against dev's compose files.
DM_BRANCH="${DM_BRANCH:-dev}"

echo "=== Установка Docker ==="
sudo apt update
sudo apt install -y docker.io docker-compose-plugin git iptables-persistent openssl

echo "=== Настройка Docker для текущего пользователя ==="
sudo usermod -aG docker "$USER"

echo "=== Ротация журналов Docker ==="
# The default json-file driver keeps every line forever, and Serilog writes to
# the console beside Loki - so each line is stored twice, once under Loki's own
# retention and once in a file that grows until the disk is full. On a
# preview-class VPS the first service to die on a full disk is Postgres.
#
# Given to the daemon rather than to each compose service: the daemon also
# covers the one-off containers the backup script and the credential generator
# start, and this is already the layer the installer owns - it writes the
# firewall rules, the unit, the crontab and a logrotate policy for the backup
# log. Log options apply to containers created after they are set, and nothing
# has been started yet.
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
git clone --branch "$DM_BRANCH" https://github.com/dm-am/dm3.git "$INSTALL_DIR"

echo "=== Подготовка docker/.env ==="
# Everything below needs this file: the backup scripts source it, and the
# compose files declare the encryption key, both Mongo passwords and both MinIO
# accounts through ${...:?}, which stops interpolation before the first
# container. Nothing created it, so the installer died on its last line with the
# unit and the nightly jobs already enabled.
bash "$INSTALL_DIR/docker/scripts/init-env.sh" server "$DM_BRANCH"

echo "=== Открытие портов (firewall) ==="
sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save

echo "=== Настройка preview-пароля ==="
# A clone carries no credentials: the file is generated here, on the server, and
# the check for an unset password lives in the generator so that the operator
# changing the password later gets the same one.
bash "$INSTALL_DIR/docker/scripts/init-htpasswd.sh"

echo "=== Установка systemd-сервиса ==="
sudo cp "$INSTALL_DIR/docker/dm3.service" /etc/systemd/system/dm3.service
sudo systemctl daemon-reload
sudo systemctl enable dm3.service

echo "=== Установка cron-задач для бэкапов ==="
sudo bash "$INSTALL_DIR/docker/scripts/install-cron.sh" "$INSTALL_DIR"

echo "=== Запуск приложения ==="
cd "$INSTALL_DIR/docker"
# No --build: CI builds the four images and pushes them, the server pulls them.
# Building the solution here costs gigabytes of disk and peak memory on a
# preview-class VPS, and it leaves a locally built image under the tag the
# registry publishes - which no later "up" refreshes, so the published images
# were consumed by nobody. The production profile adds watchtower, the updater
# the guides name; without a profile it was declared and never started.
sudo docker compose --profile production -f docker-compose.yml -f docker-compose.preview.yml up -d

echo ""
echo "=== Готово! ==="
echo "Приложение доступно по адресу: http://$(curl -s ifconfig.me)"
echo "Логин: preview"
# Пароль здесь не печатается: его задал оператор минуту назад, а напечатанный
# остается в терминале, в истории сессии и в логе установки, если она шла через
# tee. Вдобавок под set -u эта строка роняла установщик на последнем шаге, если
# пароль вводили в промпт init-htpasswd.sh, а не экспортировали.
echo ""
echo "Для смены пароля:"
echo "  bash $INSTALL_DIR/docker/scripts/init-htpasswd.sh"
echo "  cd $INSTALL_DIR/docker && sudo docker compose restart nginx"
echo ""
echo "Сервис автозапуска: systemctl status dm3"
echo "Бэкапы: ежедневно в 02:00-04:00, лог /var/log/dm3-backup.log"
