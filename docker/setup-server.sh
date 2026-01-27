#!/bin/bash
# Скрипт установки DM3 на чистый Ubuntu сервер
# Использование: curl -sSL https://raw.githubusercontent.com/dm-am/dm3/dev/docker/setup-server.sh | bash

set -e

echo "=== Установка Docker ==="
sudo apt update
sudo apt install -y docker.io docker-compose git

echo "=== Настройка Docker для текущего пользователя ==="
sudo usermod -aG docker $USER

echo "=== Клонирование репозитория ==="
cd ~
git clone https://github.com/dm-am/dm3.git
cd dm3

echo "=== Открытие портов (firewall) ==="
sudo iptables -I INPUT -p tcp --dport 80 -j ACCEPT
sudo iptables -I INPUT -p tcp --dport 443 -j ACCEPT

echo "=== Запуск приложения ==="
cd docker
sudo docker-compose -f docker-compose.yml -f docker-compose.preview.yml up -d --build

echo ""
echo "=== Готово! ==="
echo "Приложение доступно по адресу: http://$(curl -s ifconfig.me)"
echo "Логин: preview"
echo "Пароль: dm2026preview"
echo ""
echo "Для смены пароля:"
echo "  docker run --rm httpd htpasswd -nb preview НОВЫЙ_ПАРОЛЬ > ~/dm3/docker/nginx/.htpasswd"
echo "  cd ~/dm3/docker && sudo docker-compose restart nginx"
