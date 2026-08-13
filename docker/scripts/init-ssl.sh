#!/bin/bash
set -euo pipefail

# Issues the certificate of the stand and turns the TLS server on.
#
# The edge served plain http and carried a commented-out TLS block that had been
# there since the first day. It had already drifted away from the live server -
# no health probe, and a listen directive in a form nginx has warned about since
# 1.25 - so the thing meant to be switched on in the hour it was needed would not
# have started. The documented command was `certbot --nginx`, which cannot work
# here at all: the edge runs in a container and its configuration is mounted
# read-only, so the plugin has nothing on the host to edit.
#
# Hence webroot. The challenge is fetched anonymously from the public internet
# over http, which is why the edge answers /.well-known/acme-challenge/ from the
# very first boot and answers it in front of the basic-auth door.
#
# Nothing self-signed is ever generated. A warning page teaches the reader to
# click through warnings, and a stand that is honestly http is easier to reason
# about than one that is dishonestly https.
#
# Usage: init-ssl.sh <domain> [<domain>...]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(dirname "$SCRIPT_DIR")"
WEBROOT="$DOCKER_DIR/nginx/acme"
TLS_DIR="$DOCKER_DIR/nginx/tls"
SSL_ROOT="${SSL_ROOT:-/etc/letsencrypt}"
EDGE_CONTAINER="dm-nginx"

if [ "$#" -eq 0 ]; then
    echo "Использование: init-ssl.sh <домен> [<домен>...]" >&2
    echo "Например: bash $0 dm.am www.dm.am" >&2
    exit 1
fi

if ! command -v certbot > /dev/null 2>&1; then
    echo "ОШИБКА: certbot не установлен." >&2
    echo "Установить: sudo apt-get install -y certbot" >&2
    exit 1
fi

mkdir -p "$WEBROOT" "$TLS_DIR"

DOMAIN_ARGS=()
for domain in "$@"; do
    DOMAIN_ARGS+=(-d "$domain")
done

# --cert-name фиксирует имя каталога, поэтому конфигурация nginx ссылается на
# постоянный путь и переживает добавление доменов.
#
# --deploy-hook отрабатывает после каждого продления: nginx читает сертификат
# только при старте, и без перезагрузки край продолжал бы отдавать истекший
# сертификат ровно до следующей выкладки.
certbot certonly --webroot -w "$WEBROOT" \
    --cert-name dm "${DOMAIN_ARGS[@]}" \
    --deploy-hook "docker exec $EDGE_CONTAINER nginx -s reload"

PRIMARY="$1"

# Оба сервера пишутся сюда: редирект появляется тогда же, когда и TLS, и ни
# минутой раньше — иначе стенд отправлял бы на адрес, которого еще нет.
#
# Редирект висит на имени домена, а не на _: край продолжает отвечать по адресу,
# как отвечал, и https-only получает ровно тот домен, для которого выпущен
# сертификат. Челлендж остается и здесь — продление идет тем же путем.
cat > "$TLS_DIR/tls.conf" <<EOF
# Создан scripts/init-ssl.sh. Правки руками переживут ровно до следующего
# выпуска сертификата.
server {
    listen 80;
    server_name $PRIMARY;

    location ^~ /.well-known/acme-challenge/ {
        access_log off;
        root /var/www/certbot;
    }

    location / {
        return 301 https://\$host\$request_uri;
    }
}

server {
    listen 443 ssl;
    http2 on;
    server_name $PRIMARY;

    ssl_certificate     /etc/nginx/ssl/live/dm/fullchain.pem;
    ssl_certificate_key /etc/nginx/ssl/live/dm/privkey.pem;
    ssl_protocols TLSv1.2 TLSv1.3;
    ssl_prefer_server_ciphers off;
    ssl_session_cache shared:edge_ssl:10m;
    ssl_session_timeout 1d;

    include /etc/nginx/edge-locations.conf;
}
EOF

echo "Сертификат для $PRIMARY выпущен, TLS-сервер включен."
echo "Применить: sudo systemctl restart dm3"
echo "Проверить продление: sudo certbot renew --dry-run"
echo
echo "Каталог сертификатов: $SSL_ROOT (монтируется краю только на чтение)."
