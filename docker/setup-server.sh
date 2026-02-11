#!/bin/bash
set -euo pipefail

# DM3 server setup script for clean Ubuntu
# Usage: curl -sSL https://raw.githubusercontent.com/dm-am/dm3/dev/docker/setup-server.sh | bash

INSTALL_DIR="/opt/dm3"

echo "=== Установка Docker ==="
sudo apt update
sudo apt install -y docker.io docker-compose-plugin git iptables-persistent

echo "=== Настройка Docker для текущего пользователя ==="
sudo usermod -aG docker "$USER"

echo "=== Клонирование репозитория ==="
sudo mkdir -p "$INSTALL_DIR"
sudo chown "$USER:$USER" "$INSTALL_DIR"
git clone https://github.com/dm-am/dm3.git "$INSTALL_DIR"

echo "=== Открытие портов (firewall) ==="
sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT
sudo netfilter-persistent save

echo "=== Настройка preview-пароля ==="
if [ -z "${DM_PREVIEW_PASSWORD:-}" ]; then
    echo "ВНИМАНИЕ: DM_PREVIEW_PASSWORD не задан. Используется пароль по умолчанию." >&2
    echo "Для установки: export DM_PREVIEW_PASSWORD=ваш_пароль" >&2
    DM_PREVIEW_PASSWORD="dm2026preview"
fi

docker run --rm httpd htpasswd -nb preview "$DM_PREVIEW_PASSWORD" > "$INSTALL_DIR/docker/nginx/.htpasswd"

echo "=== Установка systemd-сервиса ==="
sudo cp "$INSTALL_DIR/docker/dm3.service" /etc/systemd/system/dm3.service
sudo systemctl daemon-reload
sudo systemctl enable dm3.service

echo "=== Установка cron-задач для бэкапов ==="
sudo bash "$INSTALL_DIR/docker/scripts/install-cron.sh" "$INSTALL_DIR"

echo "=== Запуск приложения ==="
cd "$INSTALL_DIR/docker"
sudo docker compose -f docker-compose.yml -f docker-compose.preview.yml up -d --build

echo ""
echo "=== Готово! ==="
echo "Приложение доступно по адресу: http://$(curl -s ifconfig.me)"
echo "Логин: preview"
echo "Пароль: ${DM_PREVIEW_PASSWORD}"
echo ""
echo "Для смены пароля:"
echo "  docker run --rm httpd htpasswd -nb preview НОВЫЙ_ПАРОЛЬ > $INSTALL_DIR/docker/nginx/.htpasswd"
echo "  cd $INSTALL_DIR/docker && sudo docker compose restart nginx"
echo ""
echo "Сервис автозапуска: systemctl status dm3"
echo "Бэкапы: ежедневно в 02:00-04:00, лог /var/log/dm3-backup.log"
