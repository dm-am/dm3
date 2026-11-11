#!/usr/bin/env bash
# Fails when a server configuration of the deployment does not parse, when the edge cannot
# switch between the primaries and the twins of a rollout or does not return to the
# primaries on a restart, when the asset hook of the frontend image rewrites a file it should
# only touch, or when the SPA server drops a header it promises.
#
# Nothing compiled the edge: a typo in either nginx.conf was a crash-looping container,
# which is the whole site. Each configuration is read by the image the edge runs, so the
# answer is the one nginx gives there; ImageDeliveryShould holds the two references equal.
set -euo pipefail

IMAGE="nginx:1.30.6-alpine@sha256:583b7f2fce1cdb8da5ad5c39ebd3c33b98b87d493190e548bdee1fa8c619c311"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/common.sh"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/compose-placeholders.sh"
cd "$ROOT"

# MSYS_NO_PATHCONV: Git Bash would otherwise rewrite the container side of -v and -subj into
# paths under the Git installation (host() hands docker and openssl the native path).
export MSYS_NO_PATHCONV=1

WORK="$(mktemp -d)"
EDGE_CONTAINER="dm-edge-check-$$"
trap 'docker rm -f "$EDGE_CONTAINER" > /dev/null 2>&1 || true; rm -rf "$WORK"' EXIT

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

# The configuration files of the edge, mounted where the compose file mounts them.
EDGE_FILES=(
  -v "$(host "$ROOT/docker/nginx/nginx.conf"):/etc/nginx/nginx.conf:ro"
  -v "$(host "$ROOT/docker/nginx/edge-locations.conf"):/etc/nginx/edge-locations.conf:ro"
  -v "$(host "$ROOT/docker/nginx/upstreams"):/etc/nginx/upstreams:ro"
  -v "$(host "$WORK/htpasswd"):/etc/nginx/.htpasswd:ro"
)

# nginx -t with the live links set to the two upstream files named, "none" leaving one unset.
edge_linked() {
  local api=$1 frontend=$2; shift 2
  # shellcheck disable=SC2016 # expanded by the container's shell
  docker run --rm \
    --add-host dmapi:127.0.0.1 --add-host dmapi-next:127.0.0.1 \
    --add-host dmfront:127.0.0.1 --add-host dmfront-next:127.0.0.1 \
    --add-host storage:127.0.0.1 \
    "${EDGE_FILES[@]}" \
    "$@" \
    "$IMAGE" sh -c '
      mkdir -p /etc/nginx/upstreams-live
      [ "$0" = none ] || ln -s "/etc/nginx/upstreams/$0.conf" /etc/nginx/upstreams-live/api.conf
      [ "$1" = none ] || ln -s "/etc/nginx/upstreams/$1.conf" /etc/nginx/upstreams-live/frontend.conf
      nginx -t' "$api" "$frontend"
}

edge() { edge_linked api-primary front-primary "$@"; }

echo "the edge parses as it is served today"
edge

echo "the edge parses with the links a rollout sets to the twins"
edge_linked api-next front-next

echo "the edge refuses to start with an upstream unlinked"
# Without a link the glob matches nothing and nginx resolves the name api as a host; were
# that to start, an edge whose command failed would serve 502 for the whole site.
for links in "none front-primary" "api-primary none"; do
  # shellcheck disable=SC2086 # two words on purpose
  if edge_linked $links > /dev/null 2>&1; then
    fail "the edge parses with the links \"$links\""
  fi
done

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

echo "the edge switches to the twins and back, a restart returns it to the primaries"
# The edge starts with the command the compose file gives it, read back from the resolved
# model, so this is the start a server runs. Four stub servers stand in for the instances,
# each on a loopback address of its own that --add-host gives the instance's name, and answer
# with their own name; they come in through the TLS include, which is the one directory of
# extra servers the configuration reads. The questions go through the rollout's listener, so
# its answers are the upstreams the edge has loaded.
mapfile -d '' -t EDGE_COMMAND < <(
  cd "$ROOT/docker" && env -u MSYS_NO_PATHCONV docker compose --env-file /dev/null \
    -f docker-compose.yml -f docker-compose.preview.yml config --format json \
  | node -e 'let s = ""; process.stdin.on("data", d => s += d).on("end", () =>
      process.stdout.write((JSON.parse(s).services.nginx.command ?? []).map(a => a + "\0").join("")))')
[ "${#EDGE_COMMAND[@]}" -gt 0 ] || fail "the edge has no command in the resolved compose model"

mkdir -p "$WORK/edge-stubs"
cat > "$WORK/edge-stubs/stubs.conf" << 'EOF'
server { listen 127.0.0.2:5000; location / { return 200 "api-primary"; } }
server { listen 127.0.0.3:5000; location / { return 200 "api-next"; } }
server { listen 127.0.0.4:80; location / { return 200 "front-primary"; } }
server { listen 127.0.0.5:80; location / { return 200 "front-next"; } }
EOF
docker run -d --name "$EDGE_CONTAINER" \
  --add-host dmapi:127.0.0.2 --add-host dmapi-next:127.0.0.3 \
  --add-host dmfront:127.0.0.4 --add-host dmfront-next:127.0.0.5 \
  --add-host storage:127.0.0.1 \
  "${EDGE_FILES[@]}" \
  -v "$(host "$WORK/edge-stubs"):/etc/nginx/tls:ro" \
  "$IMAGE" "${EDGE_COMMAND[@]}" > /dev/null

# What the listener answers for the API and the frontend, as one line.
served() {
  # shellcheck disable=SC2016 # expanded by the container's shell
  docker exec "$EDGE_CONTAINER" sh -c \
    'echo "$(wget -qO- http://127.0.0.1:8081/_ready) $(wget -qO- http://127.0.0.1:8081/_instance)"' \
    2> /dev/null || true
}
# A start or a reload takes effect within a few seconds, or it has not taken effect.
serves() {
  local tries=0
  until [ "$(served)" = "$1" ]; do
    tries=$((tries + 1))
    [ "$tries" -lt 20 ] || fail "the edge serves \"$(served)\" after $2, not \"$1\""
    sleep 0.5
  done
}

serves "api-primary front-primary" "its own start"
docker exec "$EDGE_CONTAINER" sh -c '
  ln -sfn /etc/nginx/upstreams/api-next.conf /etc/nginx/upstreams-live/api.conf &&
  ln -sfn /etc/nginx/upstreams/front-next.conf /etc/nginx/upstreams-live/frontend.conf &&
  nginx -t -q && nginx -s reload' || fail "the switch to the twins does not parse"
serves "api-next front-next" "a switch to the twins"
docker restart "$EDGE_CONTAINER" > /dev/null
serves "api-primary front-primary" "a restart"

# The positive control first: an address that does not answer at all proves nothing.
address=$(docker exec "$EDGE_CONTAINER" hostname -i | awk '{print $1}')
case "$address" in ''|127.*) fail "the container has no address of its own to ask: \"$address\"" ;; esac
[ "$(docker exec "$EDGE_CONTAINER" wget -qO- "http://$address/nginx-health" 2> /dev/null)" = ok ] ||
  fail "the edge does not answer on its own address $address"
if docker exec "$EDGE_CONTAINER" wget -qO- -T 2 "http://$address:8081/_ready" > /dev/null 2>&1; then
  fail "the rollout's listener answers on $address, past the door"
fi
docker rm -f "$EDGE_CONTAINER" > /dev/null

echo "the SPA server block parses"
# A server block, not a whole configuration: it is mounted where the image
# includes it from.
docker run --rm \
  -v "$(host "$ROOT/src/DM.Web.Client/nginx.conf"):/etc/nginx/conf.d/default.conf:ro" \
  "$IMAGE" nginx -t

echo "the asset hook copies once and then only touches"
# Run by the image's own busybox, which is the shell the entrypoint starts it with. The
# directories live inside the container, as the volume does: a host mount from Windows would
# answer for inodes and times on its own terms.
cat > "$WORK/hook-check.sh" << 'EOF'
set -eu
hook=$1
fail() { echo "ERROR: $*" >&2; exit 1; }
[ -f "$hook" ] || fail "there is no asset hook at src/DM.Web.Client/publish-assets.sh"
from=$(mktemp -d)
to=$(mktemp -d)
publish() { DM_ASSETS_FROM="$from" DM_ASSETS_TO="$to" sh "$hook"; }
leftovers() {
  if ls -A "$to" | grep -q '^\.'; then fail "a temporary file is left in the volume: $(ls -A "$to")"; fi
}
printf 'alpha' > "$from/a.js"
printf 'beta' > "$from/b.css"
printf 'theirs' > "$to/c.js"
touch -d '2001-01-01 00:00:00' "$to/c.js"
alone=$(stat -c %Y "$to/c.js")

publish
cmp -s "$from/a.js" "$to/a.js" || fail "the first run did not copy a.js"
cmp -s "$from/b.css" "$to/b.css" || fail "the first run did not copy b.css"
leftovers

# Different bytes under the same name stand for the copy the other frontend serves.
printf 'served' > "$to/a.js"
touch -d '2001-01-01 00:00:00' "$to/a.js"
inode=$(stat -c %i "$to/a.js")
before=$(stat -c %Y "$to/a.js")

publish
[ "$(cat "$to/a.js")" = served ] || fail "the second run rewrote a.js"
[ "$(stat -c %i "$to/a.js")" = "$inode" ] || fail "the second run replaced a.js with another file"
[ "$(stat -c %Y "$to/a.js")" -gt "$before" ] || fail "the second run did not touch a.js"
[ "$(cat "$to/c.js")" = theirs ] || fail "a file the image does not carry was changed"
[ "$(stat -c %Y "$to/c.js")" = "$alone" ] || fail "a file the image does not carry was touched"
leftovers
EOF
docker run --rm \
  -v "$(host "$ROOT/src/DM.Web.Client/publish-assets.sh"):/hook/publish-assets.sh:ro" \
  -v "$(host "$WORK/hook-check.sh"):/check/hook-check.sh:ro" \
  "$IMAGE" sh /check/hook-check.sh /hook/publish-assets.sh

echo "the SPA server serves the headers it promises"
# The document has to be revalidated, or a browser keeps an index.html whose chunks a later
# release deleted. The assets come from the volume, not from the image's own directory, which
# the stub here leaves without them.
cat > "$WORK/header-check.sh" << 'EOF'
set -eu
fail() { echo "ERROR: $*" >&2; exit 1; }
document=dm-check-document
printf '%s\n' "$document" > /usr/share/nginx/html/index.html
mkdir -p /var/lib/dm-front/assets
printf 'volume' > /var/lib/dm-front/assets/only-in-volume.js
nginx
fetch() {
  rm -f /tmp/body /tmp/headers
  wget -S -O /tmp/body "http://127.0.0.1$1" 2> /tmp/headers || true
  touch /tmp/body
  status=$(sed -n 's/^ *HTTP\/[0-9.]* \([0-9]*\).*/\1/p' /tmp/headers | tail -n 1)
}
header() { grep -i "^ *$1:" /tmp/headers || true; }
for path in / /some/route; do
  fetch "$path"
  [ "$status" = 200 ] || fail "$path answered $status"
  header Cache-Control | grep -q 'no-cache' || fail "$path carries no Cache-Control: no-cache"
  [ -n "$(header Content-Security-Policy)" ] || fail "$path carries no Content-Security-Policy"
  grep -q "$document" /tmp/body || fail "$path is not the document"
done
fetch /assets/only-in-volume.js
[ "$status" = 200 ] || fail "an asset of the volume answered $status"
header Cache-Control | grep -q 'immutable' || fail "an asset carries no immutable Cache-Control"
[ "$(cat /tmp/body)" = volume ] || fail "an asset was not served from the volume"
fetch /assets/missing.js
[ "$status" = 404 ] || fail "a missing asset answered $status, not 404"
if grep -q "$document" /tmp/body; then fail "a missing asset was answered with the document"; fi
fetch /_instance
[ "$status" = 200 ] || fail "/_instance answered $status"
[ "$(cat /tmp/body)" = "$(hostname)" ] || fail "/_instance does not name this container"
header Cache-Control | grep -q 'no-store' || fail "/_instance carries no Cache-Control: no-store"
EOF
docker run --rm \
  -v "$(host "$ROOT/src/DM.Web.Client/nginx.conf"):/etc/nginx/conf.d/default.conf:ro" \
  -v "$(host "$WORK/header-check.sh"):/check/header-check.sh:ro" \
  "$IMAGE" sh /check/header-check.sh

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

echo "OK: every server configuration of the deployment parses, the asset hook and the SPA headers hold."
