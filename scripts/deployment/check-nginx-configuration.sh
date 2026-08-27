#!/usr/bin/env bash
# Fails when a server configuration of the deployment does not parse.
#
# Nothing compiled the edge: a typo in either nginx.conf was a crash-looping
# container on the server, which is the whole site. Each configuration is read by
# the image the deployment runs, so the answer is the one nginx will give there -
# a directive valid in a newer build is not valid here.
#
# Why a script and not the commands in the workflow: they ran in the
# compose-topology job and nowhere else, so a typo in the edge was first read by
# a red push. The four container runs take seconds against the forty minutes of
# the local set. The workflow, scripts/gates.sh and scripts/hooks/pre-push all
# call this now, so there is one command and one version of the image.
set -euo pipefail

IMAGE="nginx:1.27-alpine"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

# Git Bash rewrites anything that looks like a Unix path in an argument before a
# native program sees it, which turns the container side of -v into a directory
# under the Git installation and -subj into a path. The variable switches that
# off for everything below, and host() hands docker and openssl - both native
# programs here - the path they want instead. On Linux and macOS cygpath does not
# exist and the value passes through unchanged.
export MSYS_NO_PATHCONV=1
host() {
  if command -v cygpath > /dev/null 2>&1; then
    cygpath -m "$1"
  else
    printf '%s' "$1"
  fi
}

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

# Issued here rather than carried in the repository: a certificate committed to
# a tree expires in it, and this one has to be valid only for the seconds nginx
# spends reading it.
certificate() {
  mkdir -p "$1/live/dm"
  openssl req -x509 -newkey rsa:2048 -nodes -days 1 \
    -keyout "$(host "$1/live/dm/privkey.pem")" \
    -out "$(host "$1/live/dm/fullchain.pem")" \
    -subj "/CN=$2" 2> /dev/null
}

# The upstreams are resolved at start-up and none of them exists here. The
# password file is mounted because the door lives in the shared locations file
# now, and nginx refuses one it cannot open. The dollars are escaped rather than
# quoted away because a single-quoted string carrying them reads to shellcheck as
# an expansion somebody forgot to write in double quotes.
printf "preview:\$apr1\$syntax\$0123456789abcdefghijk\n" > "$WORK/htpasswd"

edge() {
  docker run --rm \
    --add-host dmapi:127.0.0.1 --add-host dmfront:127.0.0.1 \
    --add-host minio:127.0.0.1 --add-host imgproxy:127.0.0.1 \
    -v "$(host "$ROOT/docker/nginx/nginx.conf"):/etc/nginx/nginx.conf:ro" \
    -v "$(host "$ROOT/docker/nginx/edge-locations.conf"):/etc/nginx/edge-locations.conf:ro" \
    -v "$(host "$WORK/htpasswd"):/etc/nginx/.htpasswd:ro" \
    "$@" \
    "$IMAGE" nginx -t
}

echo "the edge parses as it is served today"
edge

echo "the edge parses with the TLS server the issuing script writes"
# This is the half that used to be a commented-out copy: nothing parsed it, and
# by the time anybody would have switched it on it had lost the health probe and
# carried a listen directive nginx has warned about since 1.25.
certificate "$WORK/edge-ssl" edge-syntax-check
mkdir -p "$WORK/edge-tls"
sed -n '/^cat > /,/^EOF$/p' docker/scripts/init-ssl.sh \
  | sed '1d;$d;s/\$PRIMARY/edge-syntax-check/g' \
  > "$WORK/edge-tls/tls.conf"
test -s "$WORK/edge-tls/tls.conf" || {
  echo "ERROR: no TLS server block could be read out of docker/scripts/init-ssl.sh" >&2
  exit 1
}
edge \
  -v "$(host "$WORK/edge-tls"):/etc/nginx/tls:ro" \
  -v "$(host "$WORK/edge-ssl"):/etc/nginx/ssl:ro"

echo "the SPA server block parses"
# A server block, not a whole configuration: it is mounted where the image
# includes it from.
docker run --rm \
  -v "$(host "$ROOT/src/DM.Web.Client/nginx.conf"):/etc/nginx/conf.d/default.conf:ro" \
  "$IMAGE" nginx -t

echo "the second door parses once its template is rendered"
# A third edge, serving a whole public address of its own. It arrives as a
# template, so the image's own entrypoint renders it first; the certificate is
# generated here because the configuration refuses to start without one, which
# is the point of it.
certificate "$WORK/pop-ssl" pop-syntax-check
docker run --rm \
  --add-host dmfront:127.0.0.1 \
  -e POP_UPSTREAM=https://127.0.0.1 \
  -e POP_UPSTREAM_HOST=pop-syntax-check \
  -e NGINX_ENVSUBST_FILTER='^POP_' \
  -v "$(host "$ROOT/docker/nginx/pop.conf.template"):/etc/nginx/templates/default.conf.template:ro" \
  -v "$(host "$WORK/pop-ssl"):/etc/nginx/ssl:ro" \
  "$IMAGE" \
  sh -c '/docker-entrypoint.sh nginx -t'

echo "OK: every server configuration of the deployment parses."
