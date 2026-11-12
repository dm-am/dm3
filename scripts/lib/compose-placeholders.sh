#!/usr/bin/env bash
# Sourced by the check scripts that resolve the compose files; never run.
#
# Placeholders whose only job is to be non-empty, and not secrets. Compose rejects an empty
# ${VAR:?} as firmly as a missing one, so every `config` needs the whole set. One copy for
# every check: --env-file /dev/null is what keeps the answer about the compose files instead
# of about the .env of whoever runs them, and that decision is the same on a runner and on a
# developer machine.
export DM_CryptoConfiguration__KeyBase64=validation-placeholder
export POSTGRES_PASSWORD=validation-placeholder
export STORAGE_APP_PASSWORD=validation-placeholder
export STORAGE_BACKUP_PASSWORD=validation-placeholder
export GF_SECURITY_ADMIN_PASSWORD=validation-placeholder
export POP_UPSTREAM=https://validation-placeholder
export POP_UPSTREAM_HOST=validation-placeholder
export POP_ADDRESS=198.51.100.4/32

# The two the compose files declare without ${...:?}. They cost nothing to fill and unfilled
# they print a warning per service that references them, lines that bury the output of the
# checks on a screen.
export DM_APP_PASSWORD=validation-placeholder
export DM_EXPORTER_PASSWORD=validation-placeholder

# What a release or an operator's shell may carry and the checks state for themselves: a
# leftover value would turn an assertion about the defaults into one about that shell.
unset IMAGE_TAG SOURCE_REVISION DM_EDGE_DIR \
  DM_IMAGE_API DM_IMAGE_FRONT DM_IMAGE_WORKER_MAIL DM_IMAGE_WORKER_NOTIFICATION
