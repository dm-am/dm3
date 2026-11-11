#!/bin/bash
# Decides where the machine files of the operator's tools live: the environment file, the
# edge files and the scripts cron runs. Sourced, and it only sets variables, because
# init-env.sh needs the location of the environment file it is about to write and must not
# load it.
#
# From a checkout everything stays inside docker/, as it always has. From a release
# directory (rel/<id>/docker beside rel/<id>/release.env) it goes to shared/ of the root
# three levels up, since a release directory is read-only and replaced by the next one.
#
# DM_LAYOUT_ROOT names that root explicitly and wins over the location. Only the server
# installer sets it, because it writes shared/ from a checkout before any release
# directory exists. Example: DM_LAYOUT_ROOT=/opt/dm3.
#
# The one argument, used only from a checkout, is the install directory whose scripts cron
# runs (install-cron.sh passes its own).

# The logical path, not pwd -P: rel/current has to stay rel/current in the cron lines, or
# they would name one release forever.
_self="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DM_DOCKER_DIR="$(dirname "$_self")"

# shellcheck disable=SC2034 # read by the scripts that source this file
if [ -n "${DM_LAYOUT_ROOT:-}" ] || [ -f "$DM_DOCKER_DIR/../release.env" ]; then
    _root="${DM_LAYOUT_ROOT:-$(cd "$DM_DOCKER_DIR/../../.." && pwd)}"
    DM_LAYOUT=release
    DM_ENV_FILE="$_root/shared/env"
    DM_EDGE_DIR="$_root/shared/edge"
    DM_CRON_SCRIPTS_DIR="$_root/rel/current/docker/scripts"
else
    DM_LAYOUT=checkout
    DM_ENV_FILE="$DM_DOCKER_DIR/.env"
    DM_EDGE_DIR="$DM_DOCKER_DIR/nginx"
    DM_CRON_SCRIPTS_DIR="${1:-/opt/dm3}/docker/scripts"
fi

unset _self _root
