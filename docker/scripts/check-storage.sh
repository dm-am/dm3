#!/bin/bash
set -euo pipefail

# The boundaries of the object store, checked against the stack that is up.
#
# One script for the pipeline and for an operator, the way smoke-edge.sh is:
# deployment-smoke runs it after the stack comes up, and the same lines answer
# on a stand or a server. A test in the solution would need its own copy of the
# topology - flags, networks, storage-start.sh - and that copy drifts from the
# compose file without a word. This reads what is deployed.
#
# What it holds:
#   - anonymous reads reach the prefixes UploadFolder declares public and
#     nothing else, and no anonymous request lists a key;
#   - nothing on the stand network reaches SeaweedFS past the proxy, by name or
#     by address, and the proxy forwards S3 and metrics only;
#   - the memory limit of the collector is in force, and the price it costs in
#     CPU at rest stays under the threshold;
#   - each account can do what it is for and nothing more, and an object comes
#     back with the type and caching it was written with.
# The last part needs the passwords from docker/.env and is skipped without
# them, as smoke-edge.sh skips its part without the stand credentials.
#
# Usage:
#   check-storage.sh [ENDPOINT]
#
# ENDPOINT is where this host reaches S3, the published loopback port by
# default. The signed requests run in a throwaway container on the host network;
# STORAGE_NETWORK and STORAGE_ENDPOINT point them elsewhere where the host
# network does not reach the port, as on Docker Desktop. The container and
# network names are overridable for a stand under another project name.

# shellcheck source=/dev/null
. "$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)/_env.sh"

ENDPOINT="${1:-http://127.0.0.1:9000}"
ENDPOINT="${ENDPOINT%/}"
AWS_NETWORK="${STORAGE_NETWORK:-host}"
AWS_ENDPOINT="${STORAGE_ENDPOINT:-$ENDPOINT}"
BUCKET="${STORAGE_BUCKET:-dm-uploads}"
PROXY="${STORAGE_PROXY_CONTAINER:-dm-storage}"
SERVER="${STORAGE_SERVER_CONTAINER:-dm-seaweedfs}"
APP_NETWORK="${STORAGE_APP_NETWORK:-dm-full-app}"
# 0.25 of a core at rest: the collector costs about 0.08 under the memory limit,
# and twice the measured cost after load is still below this.
CPU_LIMIT="${STORAGE_CPU_LIMIT:-0.25}"
CPU_WINDOW=30
# GOMEMLIMIT=180MiB in docker-compose.yml. Without it the runtime reports
# math.MaxInt64, and the peak under load comes within 23 MiB of the container
# limit. A constant rather than an override: a ceiling raised from outside would
# let the check pass on the very stand it exists to fail.
MEMLIMIT_MAX=188743680
# UploadFolder.AnonymouslyReadable, and posts as the prefix that is not in it.
# StorageConfigurationShould fails the build when the first list drifts.
PUBLIC_PREFIXES="avatars characters"
PRIVATE_PREFIX="posts"
CACHE_CONTROL="public, max-age=31536000, immutable"
PROBE="check-storage-$(date +%s)-$$"

WORK="$(mktemp -d)"
printf 'check-storage probe object\n' > "$WORK/payload"
FAILED=0
WROTE=0

pass() { echo "ok: $1"; }
fail() {
    echo "FAIL: $1" >&2
    FAILED=1
}

# The one way this script starts a container: the pinned client image, on a
# given network, with the entrypoint the caller needs. Standard input passes
# through, which is how the probe script and the uploaded object get in.
cli() {
    local network="$1" entrypoint="$2"
    shift 2
    docker run --rm -i --network "$network" \
      -e AWS_ACCESS_KEY_ID -e AWS_SECRET_ACCESS_KEY \
      -e AWS_DEFAULT_REGION=us-east-1 \
      -e AWS_REQUEST_CHECKSUM_CALCULATION=when_required \
      --entrypoint "$entrypoint" \
      amazon/aws-cli:2.37.9@sha256:92de75724b6a746951f0e8b915d86bbccd7cb55aff96cd0cb4f7017272160780 \
      "$@"
}

# aws as one account. Path-style addressing as in backup-storage.sh. The keys go
# into the container by name from a subshell, so they reach no process list and
# do not replace the offsite keys _env.sh exported.
aws_as() {
    local user="$1" password="$2"
    shift 2
    (
        export AWS_ACCESS_KEY_ID="$user" AWS_SECRET_ACCESS_KEY="$password"
        # shellcheck disable=SC2016 # expanded by the shell inside the container
        cli "$AWS_NETWORK" sh -c 'aws configure set default.s3.addressing_style path && exec aws "$@"' aws \
          "$@" --endpoint-url "$AWS_ENDPOINT" < "$WORK/payload"
    )
}

# --path-as-is, or curl folds the dot segments away before the store sees them.
# A refused connection answers 000 rather than ending the run.
status() {
    local method="$1" path="$2"
    shift 2
    : > "$WORK/body"
    : > "$WORK/headers"
    if [ "$method" = HEAD ]; then
        curl -s --path-as-is -I -o "$WORK/headers" -w '%{http_code}' "$@" "$ENDPOINT$path" || true
    else
        curl -s --path-as-is -X "$method" -D "$WORK/headers" -o "$WORK/body" -w '%{http_code}' \
          "$@" "$ENDPOINT$path" || true
    fi
}

expect() {
    local what="$1" want="$2" got
    shift 2
    got="$(status "$@")"
    if [ "$got" = "$want" ]; then
        pass "$what -> $got"
    else
        fail "$what answered $got, expected $want"
    fi
}

header() {
    tr -d '\r' < "$WORK/headers" | sed -n "s/^$1: //Ip" | head -1
}

# Every key this run may have written, including the ones a broken account
# would have been allowed to write, removed in one request.
cleanup() {
    if [ "$WROTE" = 1 ]; then
        local keys="" key
        for key in $PUBLIC_PREFIXES $PRIVATE_PREFIX; do
            keys="$keys{Key=$key/$PROBE.webp},"
        done
        for key in imgproxy backup anonymous; do
            keys="$keys{Key=avatars/$PROBE-$key.webp},"
        done
        aws_as "$APP_USER" "$APP_PASSWORD" s3api delete-objects --bucket "$BUCKET" \
          --delete "Objects=[${keys%,}],Quiet=true" > /dev/null 2>&1 || true
    fi
    rm -rf "$WORK"
}
trap cleanup EXIT

# The verdict. Returns rather than exits when everything held, so the last
# line of the script is not an exit.
finish() {
    if [ "$FAILED" != 0 ]; then
        echo "Object storage boundaries are broken, see FAIL lines above." >&2
        exit 1
    fi
    echo "OK: object storage boundaries hold."
}

echo "=== The store's own settings ==="

# Through the proxy, from inside it: the metrics port is published to nobody,
# and this is the path Prometheus scrapes.
metric() {
    docker exec "$PROXY" curl -fsS http://127.0.0.1:9324/metrics 2> /dev/null |
        awk -v name="$1" '$1 == name { print $2; exit }'
}

memlimit="$(metric go_gc_gomemlimit_bytes || true)"
if [ -z "$memlimit" ]; then
    fail "go_gc_gomemlimit_bytes is not on $PROXY:9324"
elif awk -v v="$memlimit" -v max="$MEMLIMIT_MAX" 'BEGIN { exit !(v + 0 <= max + 0) }'; then
    pass "GOMEMLIMIT in force ($memlimit bytes)"
else
    fail "GOMEMLIMIT is $memlimit bytes, above $MEMLIMIT_MAX"
fi

cpu_before="$(metric process_cpu_seconds_total || true)"
sleep "$CPU_WINDOW"
cpu_after="$(metric process_cpu_seconds_total || true)"
if [ -z "$cpu_before" ] || [ -z "$cpu_after" ]; then
    fail "process_cpu_seconds_total is not on $PROXY:9324"
else
    cores="$(awk -v a="$cpu_before" -v b="$cpu_after" -v w="$CPU_WINDOW" 'BEGIN { printf "%.3f", (b - a) / w }')"
    if awk -v c="$cores" -v limit="$CPU_LIMIT" 'BEGIN { exit !(c + 0 <= limit + 0) }'; then
        pass "CPU at rest $cores of a core over ${CPU_WINDOW}s (limit $CPU_LIMIT)"
    else
        fail "CPU at rest $cores of a core over ${CPU_WINDOW}s, above $CPU_LIMIT"
    fi
fi

echo "=== Network ==="

server_networks="$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.NetworkID}} {{end}}' "$SERVER" 2> /dev/null || true)"
if [ -z "$server_networks" ]; then
    fail "container $SERVER not found"
else
    # shellcheck disable=SC2086 # a list of identifiers without spaces, split on purpose
    set -- $server_networks
    network="$(docker network inspect -f '{{.Name}} {{.Internal}}' "$1")"
    if [ "$#" -ne 1 ]; then
        fail "$SERVER is attached to $# networks, expected its internal network only"
    elif [ "${network#* }" != true ]; then
        fail "$SERVER is attached to ${network% *}, which is not internal"
    else
        pass "$SERVER is attached to ${network% *} only, and it is internal"
    fi
    if [ -n "$(docker port "$SERVER")" ]; then
        fail "$SERVER publishes ports: $(docker port "$SERVER" | tr '\n' ' ')"
    else
        pass "$SERVER publishes no port"
    fi
fi

# From a container on the stand network: the server's names must not resolve,
# its addresses must not answer, and the proxy must forward 9000 and 9324 and
# nothing else. 8333 S3, 8888 filer, 9333 master, 23646 and 33646 the admin
# server and its workers, 18888 the filer's gRPC.
server_ips="$(docker inspect -f '{{range .NetworkSettings.Networks}}{{.IPAddress}} {{end}}' "$SERVER" 2> /dev/null || true)"
targets=("$PROXY:9000" "$PROXY:9324")
closed=()
for port in 8333 8888 9333 18888 23646 33646; do
    closed+=("$PROXY:$port")
    for ip in $server_ips; do
        closed+=("$ip:$port")
    done
done
for ip in $server_ips; do
    closed+=("$ip:9324")
done

probe="$(unset AWS_ACCESS_KEY_ID AWS_SECRET_ACCESS_KEY
    cli "$APP_NETWORK" bash -s -- "seaweedfs $SERVER" "${targets[@]}" "${closed[@]}" <<'PROBE' || true
for name in $1; do
    if getent hosts "$name" > /dev/null 2>&1; then echo "resolves $name"; else echo "unresolved $name"; fi
done
shift
for target in "$@"; do
    if timeout 3 bash -c "exec 3<>/dev/tcp/${target%:*}/${target##*:}" 2> /dev/null; then
        echo "open $target"
    else
        echo "closed $target"
    fi
done
PROBE
)"

for name in seaweedfs "$SERVER"; do
    if grep -qx "unresolved $name" <<< "$probe"; then
        pass "$name does not resolve from $APP_NETWORK"
    else
        fail "$name resolves from $APP_NETWORK"
    fi
done
for target in "${targets[@]}"; do
    if grep -qx "open $target" <<< "$probe"; then pass "$target answers"; else fail "$target does not answer"; fi
done
for target in "${closed[@]}"; do
    if grep -qx "closed $target" <<< "$probe"; then pass "$target is closed"; else fail "$target is open from $APP_NETWORK"; fi
done

echo "=== Anonymous requests to $ENDPOINT ==="

for prefix in $PUBLIC_PREFIXES; do
    expect "missing $prefix object" 404 GET "/$BUCKET/$prefix/$PROBE-missing.webp"
done
expect "$PRIVATE_PREFIX object" 403 GET "/$BUCKET/$PRIVATE_PREFIX/$PROBE-missing.webp"
expect "ListObjectsV2" 403 GET "/$BUCKET?list-type=2"
expect "ListObjects by prefix" 403 GET "/$BUCKET?prefix=avatars/"
expect "ListObjects" 403 GET "/$BUCKET"
expect "HeadBucket" 403 HEAD "/$BUCKET"
expect "dot segments" 400 GET "/$BUCKET/avatars/..%2F$PRIVATE_PREFIX/x.webp"
expect "anonymous PutObject" 403 PUT "/$BUCKET/avatars/$PROBE-anonymous.webp" --data-binary probe
expect "anonymous DeleteObject" 403 DELETE "/$BUCKET/avatars/$PROBE-missing.webp"

# A public prefix read as a directory answers with an empty body or 404, never
# with a listing: the random part of a key is what keeps it unguessable.
for prefix in $PUBLIC_PREFIXES; do
    for query in "" "?delimiter=/&list-type=2" "?list-type=2&prefix=$prefix/"; do
        got="$(status GET "/$BUCKET/$prefix/$query")"
        if { [ "$got" = 200 ] || [ "$got" = 404 ]; } && ! grep -q '<Key>' "$WORK/body"; then
            pass "$prefix/$query -> $got, no keys"
        else
            fail "$prefix/$query answered $got: $(head -c 200 "$WORK/body")"
        fi
    done
done

APP_USER="${STORAGE_APP_USER:-dm-app}"
APP_PASSWORD="${STORAGE_APP_PASSWORD:-}"
IMGPROXY_USER="${STORAGE_IMGPROXY_USER:-dm-imgproxy}"
IMGPROXY_PASSWORD="${STORAGE_IMGPROXY_PASSWORD:-}"
BACKUP_USER="${STORAGE_BACKUP_USER:-dm-backup}"
BACKUP_PASSWORD="${STORAGE_BACKUP_PASSWORD:-}"

if [ -z "$APP_PASSWORD" ] || [ -z "$IMGPROXY_PASSWORD" ] || [ -z "$BACKUP_PASSWORD" ]; then
    echo "no storage passwords in the environment: the signed requests are skipped"
    finish
    exit 0
fi

echo "=== Accounts ==="

allowed() {
    local what="$1" out
    shift
    if out="$(aws_as "$@" 2>&1)"; then
        pass "$what"
    else
        fail "$what: $(tail -1 <<< "$out")"
    fi
}

denied() {
    local what="$1" out
    shift
    if out="$(aws_as "$@" 2>&1)"; then
        fail "$what was allowed"
    elif grep -q 'AccessDenied' <<< "$out"; then
        pass "$what denied"
    else
        fail "$what: expected AccessDenied, got: $(tail -1 <<< "$out")"
    fi
}

app=("$APP_USER" "$APP_PASSWORD")
imgproxy=("$IMGPROXY_USER" "$IMGPROXY_PASSWORD")
backup=("$BACKUP_USER" "$BACKUP_PASSWORD")

WROTE=1
for prefix in $PUBLIC_PREFIXES $PRIVATE_PREFIX; do
    allowed "dm-app PutObject $prefix/" "${app[@]}" s3 cp - "s3://$BUCKET/$prefix/$PROBE.webp" \
      --content-type image/webp --cache-control "$CACHE_CONTROL"
done

for prefix in $PUBLIC_PREFIXES; do
    got="$(status GET "/$BUCKET/$prefix/$PROBE.webp")"
    type="$(header content-type)"
    cache="$(header cache-control)"
    if [ "$got" = 200 ] && [ "$type" = image/webp ] && [ "$cache" = "$CACHE_CONTROL" ]; then
        pass "anonymous GET $prefix/ -> 200, $type, $cache"
    else
        fail "anonymous GET $prefix/ -> $got, type '$type', cache '$cache'"
    fi
done
expect "anonymous GET of an existing $PRIVATE_PREFIX object" 403 GET "/$BUCKET/$PRIVATE_PREFIX/$PROBE.webp"

allowed "dm-imgproxy HeadObject" "${imgproxy[@]}" s3api head-object --bucket "$BUCKET" --key "avatars/$PROBE.webp"
allowed "dm-imgproxy GetObject $PRIVATE_PREFIX/" "${imgproxy[@]}" s3 cp "s3://$BUCKET/$PRIVATE_PREFIX/$PROBE.webp" -
denied "dm-imgproxy PutObject" "${imgproxy[@]}" s3 cp - "s3://$BUCKET/avatars/$PROBE-imgproxy.webp"
denied "dm-imgproxy DeleteObject" "${imgproxy[@]}" s3api delete-object --bucket "$BUCKET" --key "avatars/$PROBE.webp"
denied "dm-imgproxy ListObjectsV2" "${imgproxy[@]}" s3api list-objects-v2 --bucket "$BUCKET" --max-keys 1

# Counted by Contents: this store leaves KeyCount out of the answer.
listed="$(aws_as "${backup[@]}" s3api list-objects-v2 --bucket "$BUCKET" --prefix "avatars/$PROBE" \
  --query 'Contents[].Key' --output text 2>&1 || true)"
if grep -qw "avatars/$PROBE.webp" <<< "$listed"; then
    pass "dm-backup ListObjectsV2 sees the probe"
else
    fail "dm-backup ListObjectsV2: $(tail -1 <<< "$listed")"
fi
allowed "dm-backup GetObject" "${backup[@]}" s3 cp "s3://$BUCKET/avatars/$PROBE.webp" -
denied "dm-backup PutObject" "${backup[@]}" s3 cp - "s3://$BUCKET/avatars/$PROBE-backup.webp"
denied "dm-backup DeleteObject" "${backup[@]}" s3api delete-object --bucket "$BUCKET" --key "avatars/$PROBE.webp"

denied "dm-app ListObjectsV2" "${app[@]}" s3api list-objects-v2 --bucket "$BUCKET" --max-keys 1
denied "dm-app DeleteBucket" "${app[@]}" s3api delete-bucket --bucket "$BUCKET"
denied "dm-app PutBucketPolicy" "${app[@]}" s3api put-bucket-policy --bucket "$BUCKET" \
  --policy '{"Version":"2012-10-17","Statement":[]}'
denied "dm-app PutBucketAcl" "${app[@]}" s3api put-bucket-acl --bucket "$BUCKET" --acl public-read
denied "dm-app CreateBucket" "${app[@]}" s3api create-bucket --bucket "$BUCKET-check-storage"

allowed "dm-app DeleteObject" "${app[@]}" s3 rm "s3://$BUCKET/avatars/$PROBE.webp"
expect "anonymous GET after delete" 404 GET "/$BUCKET/avatars/$PROBE.webp"

finish
