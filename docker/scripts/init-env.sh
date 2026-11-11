#!/bin/bash
set -euo pipefail

# The one place that creates the environment file: docker/.env from a checkout,
# shared/env beside the releases from a release directory (_paths.sh). A bare copy of the template is not enough: the
# key ships empty and compose stops at ${...:?} before any container starts.
#
# Usage: init-env.sh [local|server] [image-tag]
#   local  (default) template plus a generated encryption key, the same
#          preparation scripts/dm.ps1 does on a developer machine
#   server the above plus fresh credentials instead of the shared example
#          values, the Production environment and a pinned image tag

MODE="${1:-local}"
IMAGE_TAG="${2:-}"

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=docker/scripts/_paths.sh
. "$SCRIPT_DIR/_paths.sh"
ENV_FILE="$DM_ENV_FILE"
EXAMPLE_FILE="$DM_DOCKER_DIR/.env.example"

case "$MODE" in
    local|server) ;;
    *)
        echo "Error: unknown mode '$MODE', expected local or server" >&2
        exit 1
        ;;
esac

# A release directory exists only on a server, and local there means Development: Swagger
# mounted, the CSP relaxed, HSTS dropped, the template credentials kept.
if [ "$DM_LAYOUT" = release ] && [ "$MODE" = local ]; then
    echo "Error: local mode from a release directory; a server takes init-env.sh server" >&2
    exit 1
fi

if [ ! -f "$EXAMPLE_FILE" ]; then
    echo "Error: .env.example not found at $EXAMPLE_FILE" >&2
    exit 1
fi

EXISTING=0
if [ -f "$ENV_FILE" ]; then
    # Topped up rather than left alone: a hand-copied template carries the key empty. Only
    # empty values are filled, so a key already chosen is never replaced.
    EXISTING=1
else
    # shared/ does not exist before the first run on a server.
    mkdir -p "$(dirname "$ENV_FILE")"
    cp "$EXAMPLE_FILE" "$ENV_FILE"
fi

# The file holds every credential of the installation.
chmod 600 "$ENV_FILE"

# Rewrites one KEY=VALUE line, appending it when the template lacks the key. Generated
# values are hex or base64, so "|" is a safe delimiter. No sed -i: BSD sed wants an argument.
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

# Generated values are per installation: rerunning must not invalidate sessions and secrets.
set_if_empty() {
    if is_empty "$1"; then
        set_value "$1" "$2"
    fi
}

# Keys the template has and this file lacks, so a variable added after somebody's .env was
# written reaches them. Only absent keys with a template value: an empty one is generated
# above or defaulted by compose. Not on a server, where the template values are the
# credentials published in the repository and the guard at the end refuses them.
if [ "$MODE" = "local" ]; then
    ADDED=""
    while IFS= read -r line; do
        case "$line" in
            ''|'#'*) continue ;;
            *=*) ;;
            *) continue ;;
        esac

        key="${line%%=*}"
        value="${line#*=}"

        [ -n "$value" ] || continue
        if grep -q "^${key}=" "$ENV_FILE"; then
            continue
        fi

        printf '%s=%s\n' "$key" "$value" >> "$ENV_FILE"
        ADDED="$ADDED $key"
    done < "$EXAMPLE_FILE"

    if [ -n "$ADDED" ]; then
        echo "Added from .env.example:$ADDED"
    fi
fi

set_if_empty DM_CryptoConfiguration__KeyBase64 "$(openssl rand -base64 32)"

SECRETS=(POSTGRES_PASSWORD DM_APP_PASSWORD DM_EXPORTER_PASSWORD
         GF_SECURITY_ADMIN_PASSWORD
         STORAGE_APP_PASSWORD STORAGE_BACKUP_PASSWORD)

if [ "$MODE" = "server" ] && [ "$EXISTING" = 0 ]; then
    # The only moment these can be chosen: the Postgres users are created with
    # them once per empty volume. Only on a file this run created, since rotating them on a
    # running stand locks the API out. The storage accounts are rewritten from these values
    # on every start of seaweedfs.
    for secret in "${SECRETS[@]}"; do
        set_value "$secret" "$(openssl rand -hex 24)"
    done
fi

# Outside the block above: not a credential, and Development on a server mounts Swagger,
# relaxes the CSP and drops HSTS.
if [ "$MODE" = "server" ]; then
    set_value ASPNETCORE_ENVIRONMENT Production
    # Only when empty: migrate is the operator's choice, and a rerun must not undo it.
    set_if_empty DM_SCHEMA_REGIME reset
    set_if_empty DM_RELEASE_HEADROOM_MIB 512
fi

# The mail relay is a required answer on a server: activation, password reset and every
# alert travel through it, and the compose default (Mailpit) delivers into a dead end.
# Refused rather than warned about.
if [ "$MODE" = "server" ] && is_empty MAIL_HOST; then
    echo "Error: MAIL_HOST is not set." >&2
    echo "On a server it is the address of the mail relay: the letters of the site" >&2
    echo "and every alert of alertmanager travel through it. Set MAIL_HOST and" >&2
    echo "MAIL_PORT in $ENV_FILE (see docker/.env.example), then run this again." >&2
    exit 1
fi

if [ "$MODE" = "server" ] && [ -n "$IMAGE_TAG" ]; then
    set_value IMAGE_TAG "$IMAGE_TAG"
fi

if [ "$EXISTING" = 1 ]; then
    echo "Completed $ENV_FILE (mode: $MODE); values already present were kept"
else
    echo "Created $ENV_FILE from .env.example (mode: $MODE)"
fi

# A server still holding the template's credentials holds those of every reader of the
# repository. They cannot be rotated here, so refuse instead of exiting 0.
if [ "$MODE" = "server" ]; then
    # Every value the template ships with a real one in it.
    SHARED=""
    for secret in "${SECRETS[@]}"; do
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
