#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
DOCKER_DIR="$(dirname "$SCRIPT_DIR")"
ENV_FILE="$DOCKER_DIR/.env"
EXAMPLE_FILE="$DOCKER_DIR/.env.example"

if [ -f "$ENV_FILE" ]; then
    echo ".env already exists at $ENV_FILE"
    exit 0
fi

if [ ! -f "$EXAMPLE_FILE" ]; then
    echo "Error: .env.example not found at $EXAMPLE_FILE"
    exit 1
fi

cp "$EXAMPLE_FILE" "$ENV_FILE"
echo "Created $ENV_FILE from .env.example"
echo "Please update the passwords in $ENV_FILE before running docker compose."
