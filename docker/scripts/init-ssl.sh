#!/bin/bash
set -euo pipefail

# Issues the certificate of the stand and turns the TLS server on.
#
# Webroot, because `certbot --nginx` cannot work here: the edge runs in a container with
# its configuration mounted read-only. The challenge is fetched anonymously over http, so
# the edge answers /.well-known/acme-challenge/ from the first boot, in front of basic
# auth. Nothing self-signed is generated: a warning page teaches readers to click through
# warnings.
#
# Usage: init-ssl.sh <domain> [<domain>...]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=docker/scripts/_paths.sh
. "$SCRIPT_DIR/_paths.sh"
WEBROOT="$DM_EDGE_DIR/acme"
TLS_DIR="$DM_EDGE_DIR/tls"
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

# --cert-name fixes the directory name, so the nginx configuration keeps a stable path.
# --deploy-hook reloads nginx after every renewal, since it reads the certificate only at
# start and would serve the expired one until the next deploy.
certbot certonly --webroot -w "$WEBROOT" \
    --cert-name dm "${DOMAIN_ARGS[@]}" \
    --deploy-hook "docker exec $EDGE_CONTAINER nginx -s reload"

PRIMARY="$1"

# Both servers are written here: the redirect appears together with TLS and not before. It
# hangs on the domain name, not on _, so the edge keeps answering by address. The
# challenge stays, because renewal uses the same path.
cat > "$TLS_DIR/tls.conf" <<EOF
# Создан docker/scripts/init-ssl.sh. Правки руками переживут ровно до следующего
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
