#!/bin/bash
set -euo pipefail

# The one place that creates docker/.env.
#
# Copying the template is half the job. It ships the encryption key empty on
# purpose, and the compose files declare that key - along with both Mongo
# passwords and both MinIO accounts - through ${...:?}, which stops
# interpolation before a single container starts. The server installer created
# no .env at all and died on exactly that, with the systemd unit and four
# nightly backup jobs already enabled.
#
# Usage: init-env.sh [local|server] [image-tag]
#   local  (default) template plus a generated encryption key, the same
#          preparation scripts/dm.ps1 does on a developer machine
#   server the above plus fresh credentials instead of the shared example
#          values, the Production environment and a pinned image tag

MODE="${1:-local}"
IMAGE_TAG="${2:-}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DOCKER_DIR="$(dirname "$SCRIPT_DIR")"
ENV_FILE="$DOCKER_DIR/.env"
EXAMPLE_FILE="$DOCKER_DIR/.env.example"

case "$MODE" in
    local|server) ;;
    *)
        echo "Error: unknown mode '$MODE', expected local or server" >&2
        exit 1
        ;;
esac

if [ -f "$ENV_FILE" ]; then
    echo ".env already exists at $ENV_FILE"
    exit 0
fi

if [ ! -f "$EXAMPLE_FILE" ]; then
    echo "Error: .env.example not found at $EXAMPLE_FILE" >&2
    exit 1
fi

cp "$EXAMPLE_FILE" "$ENV_FILE"
# The file holds every credential of the installation.
chmod 600 "$ENV_FILE"

# Rewrites one KEY=VALUE line, appending it when the template does not carry the
# key at all. Generated values are hex or base64, so "|" is free as a delimiter
# and nothing can hold a character that breaks a connection string. sed -i is
# avoided on purpose: BSD sed wants an argument for it, and this runs on
# developer machines as well as on the server.
set_value() {
    local key="$1" value="$2" tmp
    if grep -q "^${key}=" "$ENV_FILE"; then
        tmp="$(mktemp)"
        sed "s|^${key}=.*|${key}=${value}|" "$ENV_FILE" > "$tmp"
        cat "$tmp" > "$ENV_FILE"
        rm -f "$tmp"
    else
        printf '%s=%s\n' "$key" "$value" >> "$ENV_FILE"
    fi
}

set_value DM_CryptoConfiguration__KeyBase64 "$(openssl rand -base64 32)"

if [ "$MODE" = "server" ]; then
    # The only moment these can be chosen: the Mongo application user is created
    # by the initdb hook and the MinIO accounts by minio-init, and both run once
    # per empty volume. Keeping the template values would put every password of
    # a public stand in a public repository.
    for secret in POSTGRES_PASSWORD RABBITMQ_DEFAULT_PASS MINIO_ROOT_PASSWORD \
                  GF_SECURITY_ADMIN_PASSWORD MONGO_ROOT_PASSWORD MONGO_PASSWORD \
                  MINIO_APP_PASSWORD MINIO_IMGPROXY_PASSWORD; do
        set_value "$secret" "$(openssl rand -hex 24)"
    done
    # imgproxy wants hex of exactly 32 bytes for both.
    set_value IMGPROXY_KEY "$(openssl rand -hex 32)"
    set_value IMGPROXY_SALT "$(openssl rand -hex 32)"

    # The template sets Development for local work, and a copied file must not
    # be the thing that opens the dev-only seed and role endpoints on a server.
    set_value ASPNETCORE_ENVIRONMENT Production

    if [ -n "$IMAGE_TAG" ]; then
        set_value IMAGE_TAG "$IMAGE_TAG"
    fi
fi

echo "Created $ENV_FILE from .env.example (mode: $MODE)"
