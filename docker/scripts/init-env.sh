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

if [ ! -f "$EXAMPLE_FILE" ]; then
    echo "Error: .env.example not found at $EXAMPLE_FILE" >&2
    exit 1
fi

EXISTING=0
if [ -f "$ENV_FILE" ]; then
    # An existing file is topped up rather than left alone. Returning success on
    # sight was the same hole under a different door: two guides still tell the
    # reader to copy .env.example by hand, and after that this script had nothing
    # to say - the copy carries the encryption key empty, compose declares it
    # through ${...:?}, and the documented first run died on interpolation before
    # a single container started, on a file the tooling had just approved of.
    # Only empty values are filled, so a key already chosen is never replaced.
    EXISTING=1
else
    cp "$EXAMPLE_FILE" "$ENV_FILE"
fi

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

# True when the key is absent from the file or present with an empty value.
is_empty() {
    ! grep -qE "^${1}=.+$" "$ENV_FILE"
}

# Generated values are per installation, so an existing one is kept: rerunning
# this must not invalidate every session and every stored secret of a stand.
set_if_empty() {
    if is_empty "$1"; then
        set_value "$1" "$2"
    fi
}

set_if_empty DM_CryptoConfiguration__KeyBase64 "$(openssl rand -base64 32)"

if [ "$MODE" = "server" ] && [ "$EXISTING" = 0 ]; then
    # The only moment these can be chosen: the Mongo application user is created
    # by the initdb hook and the MinIO accounts by minio-init, and both run once
    # per empty volume. Keeping the template values would put every password of
    # a public stand in a public repository.
    #
    # Only on a file this run created. Rotating the passwords of a stand that is
    # already up locks the API out of stores whose users were created with the
    # old ones, so an existing file keeps whatever it holds and says so below.
    for secret in POSTGRES_PASSWORD RABBITMQ_DEFAULT_PASS MINIO_ROOT_PASSWORD \
                  GF_SECURITY_ADMIN_PASSWORD MONGO_ROOT_PASSWORD MONGO_PASSWORD \
                  MINIO_APP_PASSWORD MINIO_IMGPROXY_PASSWORD; do
        set_value "$secret" "$(openssl rand -hex 24)"
    done
    # imgproxy wants hex of exactly 32 bytes for both.
    set_value IMGPROXY_KEY "$(openssl rand -hex 32)"
    set_value IMGPROXY_SALT "$(openssl rand -hex 32)"

fi

# Outside the block above on purpose: this is not a credential. Rotating a
# password on a running stand locks the API out of stores whose users hold the
# old one, which is why that block only fires on a file this run created — but
# the environment is read at startup and bound to nothing, so setting it is
# always safe and Development on a server is never right. Left as the template
# had it, a hand-copied .env mounts Swagger, relaxes the CSP to
# script-src 'self' 'unsafe-inline' and drops Strict-Transport-Security, and the
# script said so in a note on stderr and exited 0.
if [ "$MODE" = "server" ]; then
    set_value ASPNETCORE_ENVIRONMENT Production
fi

if [ "$MODE" = "server" ] && [ -n "$IMAGE_TAG" ]; then
    set_value IMAGE_TAG "$IMAGE_TAG"
fi

if [ "$EXISTING" = 1 ]; then
    echo "Completed $ENV_FILE (mode: $MODE); values already present were kept"
else
    echo "Created $ENV_FILE from .env.example (mode: $MODE)"
fi

# A server still holding the template's credentials holds the credentials of
# every reader of the repository. The script cannot rotate them here — see the
# block above — so it refuses instead of reporting success: a note on stderr and
# exit 0 is indistinguishable from a clean run to the installer that calls this,
# and the stand came up on published passwords.
if [ "$MODE" = "server" ]; then
    SHARED=""
    for secret in POSTGRES_PASSWORD RABBITMQ_DEFAULT_PASS MINIO_ROOT_PASSWORD \
                  GF_SECURITY_ADMIN_PASSWORD MONGO_ROOT_PASSWORD MONGO_PASSWORD \
                  MINIO_APP_PASSWORD MINIO_IMGPROXY_PASSWORD; do
        example_value="$(sed -n "s|^${secret}=||p" "$EXAMPLE_FILE" | head -1)"
        actual_value="$(sed -n "s|^${secret}=||p" "$ENV_FILE" | head -1)"
        if [ -n "$example_value" ] && [ "$example_value" = "$actual_value" ]; then
            SHARED="$SHARED $secret"
        fi
    done

    if [ -n "$SHARED" ]; then
        echo "Error: $ENV_FILE still holds the example values for:$SHARED" >&2
        echo "These are published in the repository. Choose them, or delete $ENV_FILE and rerun." >&2
        exit 1
    fi
fi
