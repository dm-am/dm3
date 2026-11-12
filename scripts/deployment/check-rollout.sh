#!/usr/bin/env bash
# Fails when a release stops rolling out without a failed request, or when a release that must
# not land does: runs 1 to 7 and 9 to 12 of the release design (3.16), each under the probe.
#
# Without an argument it holds only the verdict of the probe, on made-up logs: node and nothing
# else. With --layout it is the deployment-smoke job's, as root on a throwaway machine, and goes
# on from the layout check-release-entry.sh --layout left: release A live, the origin, the
# signing key and the registry in /var/tmp/dm3-proof.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/common.sh"
PROBE="$ROOT/scripts/deployment/rollout-probe.mjs"

offline() {
  WORK="$(mktemp -d)"
  trap 'rm -rf "$WORK"' EXIT
  local now=1800000000000

  # One answer per path for a minute, a hub that opens once, and the edits of each case on top.
  log() {
    local t path
    for t in $(seq 0 100 60000); do
      for path in /_ready /v1/boards; do
        printf '{"t":%d,"path":"%s","status":200,"ms":3}\n' "$((now + t))" "$path"
      done
    done
    printf '{"t":%d,"hub":"open"}\n' "$now"
  }
  verdict() { node "$PROBE" verdict --log "$WORK/log" --windows "$WORK/windows" "$@" > "$WORK/out" 2>&1; }
  green() {
    local what=$1
    shift
    verdict "$@" || fail "$what: the verdict is red: $(cat "$WORK/out")"
    echo "  green: $what"
  }
  red() {
    local what=$1 reason=$2
    shift 2
    if verdict "$@"; then
      fail "$what: the verdict is green"
    fi
    grep -qF -- "$reason" "$WORK/out" || fail "$what: red for another reason than \"$reason\": $(cat "$WORK/out")"
    echo "  red: $what"
  }

  echo "the verdict of the probe is green only for a rollout no visitor noticed"
  log > "$WORK/log"
  printf '%d %d\n' "$((now + 10000))" "$((now + 20000))" > "$WORK/windows"
  green "every answer 200 and the hub connected"
  local status
  for status in 502 401 404 429; do
    { log; printf '{"t":%d,"path":"/v1/boards","status":%d,"ms":3}\n' "$((now + 5000))" "$status"; } > "$WORK/log"
    red "one answer $status" "/v1/boards: $status"
  done
  { log; printf '{"t":%d,"path":"/","error":"socket hang up","ms":3}\n' "$((now + 5000))"; } > "$WORK/log"
  red "a request with no answer" "/: socket hang up"
  log | awk -v from="$((now + 10000))" -v to="$((now + 20000))" '{ match($0, /"t":[0-9]+/); t = substr($0, RSTART + 4, RLENGTH - 4) + 0 } t < from || t > to' > "$WORK/log"
  red "a switch window with no answers" "only 0 answers between"
  { log; printf '{"t":%d,"hub":"close"}\n{"t":%d,"hub":"open"}\n' "$((now + 5000))" "$((now + 6000))"; } > "$WORK/log"
  green "a hub that reconnects within a second"
  { log; printf '{"t":%d,"hub":"close"}\n{"t":%d,"hub":"open"}\n' "$((now + 5000))" "$((now + 25000))"; } > "$WORK/log"
  red "a hub that reconnects after 20 s" "did not reconnect within 10 s"
  { log; printf '{"t":%d,"hub":"close"}\n' "$((now + 5000))"; } > "$WORK/log"
  red "a hub that never reconnects" "did not reconnect within 10 s"
  { log; for t in 5000 15000 25000; do printf '{"t":%d,"hub":"close"}\n{"t":%d,"hub":"open"}\n' "$((now + t))" "$((now + t + 1000))"; done; } > "$WORK/log"
  red "three hub disconnects in one rollout" "disconnected 3 times"
  log | grep -v '"hub"' > "$WORK/log"
  red "a hub that never connected" "never connected"

  echo "OK: the verdict turns red on every failure a visitor would see."
}

# --- The layout: runs 1 to 7 and 9 to 12 ---------------------------------------------------

PROOF=/var/tmp/dm3-proof
PREFIX='127.0.0.1:5001/dm-am/'
ENTRY=/usr/local/sbin/dm3-release
STATE=/var/lib/dm3

# git on the origin as root: it belongs to the release account, and git refuses a repository
# another user owns. Scoped to the origin, so the server's own git calls stay as strict as ever.
as_signer() {
  GIT_CONFIG_COUNT=1 GIT_CONFIG_KEY_0=safe.directory GIT_CONFIG_VALUE_0="$PROOF/origin.git" "$@"
}

# make_commit <message> [<path>=<file>]...: a commit on the origin's dev whose parent is the tip
# and whose tree is A's with the given files, so the breakage of one release does not reach the
# next. Sets COMMIT.
make_commit() {
  local message=$1 index="$PROOF/index" spec path file mode
  shift
  rm -f "$index"
  GIT_INDEX_FILE="$index" git -C "$CLONE" read-tree "$A"
  for spec in "$@"; do
    path=${spec%%=*}
    file=${spec#*=}
    mode="$(git -C "$CLONE" ls-tree "$A" -- "$path" | awk '{ print $1 }')"
    GIT_INDEX_FILE="$index" git -C "$CLONE" update-index --add \
      --cacheinfo "${mode:-100644},$(git -C "$CLONE" hash-object -w "$file"),$path"
  done
  COMMIT="$(git -C "$CLONE" commit-tree -p "$TIP" -m "$message" "$(GIT_INDEX_FILE="$index" git -C "$CLONE" write-tree)")"
  as_signer git -C "$CLONE" push -q origin "$COMMIT:refs/heads/dev"
  TIP=$COMMIT
}

# An edited copy of A's file: the path, then a sed program. Prints the copy's path.
edited() {
  local path=$1 program=$2 copy
  copy="$(mktemp "$PROOF/edit.XXXXXX")"
  git -C "$CLONE" show "$A:$path" | sed "$program" > "$copy"
  printf '%s=%s\n' "$path" "$copy"
}

# A's settings list with one more variable, valued per release, so every release changes the
# application's definitions and nothing else.
settings() {
  edited docker/docker-compose.yml "/^x-workload-env: &workload-env\$/a\\
  DM_ProofRelease: '$1'"
}

# make_images <commit> [<api layer>] [<front layer>]: A's four images with one more layer, the
# commit's revision label and the breakage given, pushed under the short commit, which is where
# the release command finds them. The legacy builder, since it reads A from the local images.
make_images() {
  local commit=$1 api_layer=${2:-} front_layer=${3:-} image layer
  for image in dm-api dm-front dm-worker-mail dm-worker-notification; do
    layer=""
    [ "$image" != dm-api ] || layer=$api_layer
    [ "$image" != dm-front ] || layer=$front_layer
    printf 'FROM %s\nLABEL org.opencontainers.image.revision=%s\n%s\n' "${A_REF[$image]}" "$commit" "$layer" |
      DOCKER_BUILDKIT=0 docker build -q -t "$PREFIX$image:${commit:0:7}" - > /dev/null
    docker push -q "$PREFIX$image:${commit:0:7}" > /dev/null
    docker image rm "$PREFIX$image:${commit:0:7}" > /dev/null
  done
}

# release <version> <commit>: the release command signs and pushes the tag, from a worktree at the
# highest release tag, as the signer runs it.
release() {
  local version=$1 commit=$2 highest
  highest="$(git -C "$CLONE" tag -l 'v*' --sort=-version:refname | head -n 1)"
  git -C "$CLONE" switch -q --detach "${highest:-$A}"
  as_signer env DM_RELEASE_UNATTESTED=1 DM_RELEASE_IMAGE_PREFIX="${PREFIX}dm" \
    bash "$CLONE/scripts/release/release.sh" "$version" "$commit" > /dev/null ||
    fail "the release command did not sign $version"
  git -C "$CLONE" fetch -q --tags origin
}

live() { readlink /opt/dm3/rel/current; }
container_id() { docker inspect --format '{{.Id}}' "$1"; }

# same_primaries <run> <api id> <front id>: a release that failed before step 4 left them alone.
same_primaries() {
  test "$(container_id dm-api)" = "$2" || fail "$1: the API primary was replaced"
  test "$(container_id dm-front)" = "$3" || fail "$1: the frontend primary was replaced"
}
tried_lines() { grep -c "	$1	" "$STATE/tried" 2> /dev/null || true; }

probe_start() {
  rm -f "$PROOF/probe.ndjson"
  RUN_SINCE="$(date +%s)"
  node "$PROBE" run --base http://localhost --auth "preview:$PASSWORD" --asset "$ASSET" \
    --volume-file /assets/proof-volume.txt --out "$PROOF/probe.ndjson" &
  PROBE_PID=$!
  sleep 3
}

probe_stop() {
  kill -TERM "$PROBE_PID"
  wait "$PROBE_PID" || true
  # A window per switch of the edge, from the line before it to five seconds after the line
  # that says it took.
  journalctl --quiet -o short-unix --since "@$RUN_SINCE" -u 'dm3-release*' |
    awk '/switching to (next|primary)/ { start = $1 }
      /switched to (next|primary)/ && start { printf "%d %d\n", (start - 1) * 1000, ($1 + 5) * 1000; start = "" }' \
      > "$PROOF/windows"
}

# The edge, the containers and the files as current describes them.
stand_is_current() {
  local service container key
  test -z "$(docker ps -a --format '{{.Names}}' | grep -E '^dm-(api|front)-next$' || true)" || fail "a twin is still there"
  test "$(docker exec dm-nginx readlink /etc/nginx/upstreams-live/api.conf)" = /etc/nginx/upstreams/api-primary.conf ||
    fail "the edge's api link is not on the primary"
  test "$(docker exec dm-nginx readlink /etc/nginx/upstreams-live/frontend.conf)" = /etc/nginx/upstreams/front-primary.conf ||
    fail "the edge's frontend link is not on the primary"
  for service in dmapi:dm-api:DM_IMAGE_API dmfront:dm-front:DM_IMAGE_FRONT \
    dm-mail-worker:dm-mail-worker:DM_IMAGE_WORKER_MAIL dm-notification-worker:dm-notification-worker:DM_IMAGE_WORKER_NOTIFICATION; do
    IFS=: read -r service container key <<< "$service"
    test "$(docker inspect --format '{{.Image}}' "$container")" = \
      "$(docker image inspect --format '{{.Id}}' "$(env_value "$REL/current/release.env" "$key")")" ||
      fail "$container does not run the image current names"
    test "$(docker inspect --format '{{index .Config.Labels "com.docker.compose.config-hash"}}' "$container")" = \
      "$(compose_cmd "$REL/current" config --hash "$service" | awk '{ print $2 }')" ||
      fail "$container is not the definition current names"
  done
  test -z "$(find /opt/dm3/repo.git ! -user dm-release | head -n 3)" || fail "files under repo.git belong to someone other than dm-release"
  test "$(stat -c '%n %i %a %U %Y' /opt/dm3/shared/env /etc/dm3 /etc/dm3/release-signers /etc/dm3/release.conf)" = "$PINS" ||
    fail "the shared file or the pins changed, which only the operator writes"
}

# Zero-failure verdict (3.16). With an argument, the twins are left out of the exit check: a twin
# that never became healthy is removed, not replaced.
zero_failure() {
  local containers='^dm-(api|front|api-next|front-next|mail-worker|notification-worker)$' bad
  probe_stop
  node "$PROBE" verdict --log "$PROOF/probe.ndjson" --windows "$PROOF/windows" || fail "the probe saw failures: $1"
  [ "${2:-}" != twins-broken ] || containers='^dm-(api|front|mail-worker|notification-worker)$'
  bad="$(docker events --since "$RUN_SINCE" --until "$(date +%s)" --filter type=container --filter event=die \
    --format '{{.Actor.Attributes.name}} {{.Actor.Attributes.exitCode}}' | awk -v names="$containers" '$1 ~ names && $2 != 0')"
  test -z "$bad" || fail "$1: containers exited with an error: $bad"
  bad="$(docker events --since "$RUN_SINCE" --until "$(date +%s)" --filter type=container --filter event=kill \
    --format '{{.Actor.Attributes.name}} {{.Actor.Attributes.signal}}' | awk -v names="$containers" '$1 ~ names && $2 == 9')"
  test -z "$bad" || fail "$1: containers outlived their grace and were killed: $bad"
  bad="$(docker logs --since "$RUN_SINCE" dm-nginx 2>&1 | grep -E 'upstream|" 5[0-9]{2} ' | head -n 5 || true)"
  test -z "$bad" || fail "$1: the edge logged upstream errors or 5xx: $bad"
  stand_is_current
  smoke
  echo "  zero failures: $1"
}

smoke() {
  bash "$ROOT/docker/scripts/smoke-edge.sh" http://localhost preview "$PASSWORD" > /dev/null || fail "the edge does not answer as the deployment needs"
}

poll() { "$ENTRY" poll > "$PROOF/poll.log" 2>&1 || true; }

# A signed tag on the given commit with a message from the arguments, each replacing one line,
# dropping it when empty or adding one when it names no field; pushed under <ref name>.
push_tag() {
  local name=$1 ref=$2 key=$3 commit=$4
  shift 4
  local -A line=([commit]="commit: $commit" [api]="api: $(env_value "$REL/current/release.env" DM_IMAGE_API)"
    [front]="front: $(env_value "$REL/current/release.env" DM_IMAGE_FRONT)"
    [worker-mail]="worker-mail: $(env_value "$REL/current/release.env" DM_IMAGE_WORKER_MAIL)"
    [worker-notification]="worker-notification: $(env_value "$REL/current/release.env" DM_IMAGE_WORKER_NOTIFICATION)")
  local extra=() change field
  for change in "$@"; do
    field="${change%%=*}"
    if [ -n "${line[$field]+x}" ]; then line[$field]="${change#*=}"; else extra+=("$change"); fi
  done
  {
    printf 'DM3 release %s\n\n' "$name"
    for field in commit api front worker-mail worker-notification; do
      [ -z "${line[$field]}" ] || printf '%s\n' "${line[$field]}"
    done
    [ "${#extra[@]}" -eq 0 ] || printf '%s\n' "${extra[@]}"
  } > "$PROOF/message"
  if [ "$key" = none ]; then
    git -C "$CLONE" tag -f -a --cleanup=verbatim -F "$PROOF/message" "$name" "$commit"
  else
    git -C "$CLONE" -c gpg.format=ssh -c user.signingkey="$PROOF/$key" tag -f -s --cleanup=verbatim -F "$PROOF/message" "$name" "$commit"
  fi
  as_signer git -C "$CLONE" push -q -f origin "refs/tags/$name:refs/tags/$ref"
  git -C "$CLONE" tag -d "$name" > /dev/null
}

# A tag that must change nothing: pushed, one tick, current and the primaries as they were, and the
# journal of the tick names it.
untouched() {
  local what=$1 says=$2 before api
  before="$(live)"
  api="$(container_id dm-api)"
  poll
  test "$(live)" = "$before" || fail "$what moved current to $(live)"
  test "$(container_id dm-api)" = "$api" || fail "$what replaced the API"
  grep -qF -- "$says" "$PROOF/poll.log" || fail "$what: the tick does not say \"$says\": $(cat "$PROOF/poll.log")"
  echo "  untouched: $what"
}

# The edits of a commit are path=file words, one per edited file, split on purpose.
# shellcheck disable=SC2046
layout() {
  [ "$(id -u)" -eq 0 ] || fail "--layout runs as root, on the layout check-release-entry.sh --layout installed"
  if [ ! -d "$PROOF" ] || [ ! -L /opt/dm3/rel/current ]; then
    fail "no layout to go on from: run check-release-entry.sh --layout first"
  fi
  # shellcheck source=docker/scripts/release-entry.sh
  . "$ENTRY"
  PASSWORD="$(cat "$PROOF/password")"
  PINS="$(stat -c '%n %i %a %U %Y' /opt/dm3/shared/env /etc/dm3 /etc/dm3/release-signers /etc/dm3/release.conf)"
  A="$(env_value "$REL/current/release.env" RELEASE_COMMIT)"
  TIP=$A
  declare -gA A_REF=([dm-api]="$(env_value "$REL/current/release.env" DM_IMAGE_API)"
    [dm-front]="$(env_value "$REL/current/release.env" DM_IMAGE_FRONT)"
    [dm-worker-mail]="$(env_value "$REL/current/release.env" DM_IMAGE_WORKER_MAIL)"
    [dm-worker-notification]="$(env_value "$REL/current/release.env" DM_IMAGE_WORKER_NOTIFICATION)")
  local a_id
  a_id="$(live)"

  CLONE="$PROOF/clone"
  as_signer git clone -q "$PROOF/origin.git" "$CLONE"
  git -C "$CLONE" config user.name smoke
  git -C "$CLONE" config user.email smoke@example.invalid
  git -C "$CLONE" config gpg.format ssh
  git -C "$CLONE" config user.signingkey "$PROOF/release-key"
  ssh-keygen -q -t ed25519 -N '' -C stray -f "$PROOF/stray-key"

  ASSET="$(curl -fsS -u "preview:$PASSWORD" http://localhost/ | grep -o '/assets/[A-Za-z0-9._-]*\.js' | head -n 1)"
  test -n "$ASSET" || fail "the document of A names no script"
  docker exec dm-front sh -c 'echo proof > /var/lib/dm-front/assets/proof-volume.txt'

  echo "before run 1: 50 reloads of the edge under the probe"
  probe_start
  for _ in $(seq 50); do
    docker exec dm-nginx nginx -s reload
    sleep 0.2
  done
  zero_failure "50 reloads"

  echo "run 1: signed B, one tick; an asset of A and a stray file aged to 40 days"
  local aged inode
  aged="$(date -d '40 days ago' +%Y%m%d%H%M)"
  docker exec dm-front sh -c 'touch -t "$1" "/var/lib/dm-front/assets/${2##*/}"; echo stray > /var/lib/dm-front/assets/proof-stray.txt
    touch -t "$1" /var/lib/dm-front/assets/proof-stray.txt' sh "$aged" "$ASSET"
  inode="$(docker exec dm-front stat -c %i "/var/lib/dm-front/assets/${ASSET##*/}")"
  make_commit B $(settings B)
  local commit_b=$COMMIT
  make_images "$commit_b"
  release v1.1.0 "$commit_b"
  probe_start
  poll
  zero_failure "run 1"
  [[ $(live) == v1.1.0-* ]] || fail "run 1: current names $(live), not B: $(cat "$PROOF/poll.log")"
  test "$(docker exec dm-front stat -c %i "/var/lib/dm-front/assets/${ASSET##*/}")" = "$inode" ||
    fail "run 1: A's asset was deleted and published again instead of touched"
  docker exec dm-front test ! -e /var/lib/dm-front/assets/proof-stray.txt || fail "run 1: the stray file outlived its 30 days"

  echo "run 2: a tick again, and deploy of the live tag"
  local b_id api
  b_id="$(live)"
  api="$(container_id dm-api)"
  poll
  if "$ENTRY" deploy v1.1.0 > "$PROOF/deploy.log" 2>&1; then
    fail "run 2: deploy of the live tag succeeded"
  fi
  same_primaries "run 2" "$api" "$(container_id dm-front)"
  test "$(live)" = "$b_id" || fail "run 2: current moved to $(live)"

  echo "run 3: rollback to A, then a tick"
  probe_start
  "$ENTRY" rollback || fail "run 3: the rollback failed"
  zero_failure "run 3"
  test "$(live)" = "$a_id" || fail "run 3: current names $(live), not A"
  poll
  test "$(live)" = "$a_id" || fail "run 3: B came back"

  # Runs 4 to 7 fail; each one's primaries must be the ones it found.
  local front lines
  echo "run 4: an API twin that never becomes healthy"
  make_commit C $(settings C)
  make_images "$COMMIT" 'ENTRYPOINT ["sleep", "infinity"]'
  release v1.2.0 "$COMMIT"
  api="$(container_id dm-api)"
  front="$(container_id dm-front)"
  probe_start
  poll
  zero_failure "run 4" twins-broken
  same_primaries "run 4" "$api" "$front"
  lines="$(tried_lines v1.2.0)"
  poll
  test "$(tried_lines v1.2.0)" = "$lines" || fail "run 4: the failed tag was tried again"

  echo "run 5: a frontend twin that answers another name at /_instance"
  make_commit D $(settings D)
  # shellcheck disable=SC2016 # a Dockerfile line: the shell in the image expands it
  make_images "$COMMIT" "" 'RUN sed -i "s/return 200 \"\$hostname\\\\n\";/return 200 \"elsewhere\\\\n\";/" /etc/nginx/conf.d/default.conf'
  release v1.3.0 "$COMMIT"
  api="$(container_id dm-api)"
  front="$(container_id dm-front)"
  probe_start
  poll
  zero_failure "run 5"
  grep -q "not dm-front-next" "$PROOF/poll.log" || fail "run 5: the switch check did not fail on the frontend: $(cat "$PROOF/poll.log")"
  same_primaries "run 5" "$api" "$front"

  echo "run 6: an API healthy only as the twin"
  local broken
  broken="$(mktemp "$PROOF/edit.XXXXXX")"
  git -C "$CLONE" show "$A:docker/docker-compose.yml" | awk '
    /^  DM_ProofRelease/ { next }
    /^x-workload-env: &workload-env$/ { print; print "  DM_ProofRelease: '\''E'\''"; next }
    /^  dmapi: &api-service$/ { in_api = 1 }
    in_api && /^    environment: \*workload-env$/ {
      print "    environment:"
      print "      <<: *workload-env"
      print "      DM_ConnectionStrings__Rdb: Host=dm-pg-missing;Port=5432;Database=dm3;Timeout=2"
      in_api = 0
      next
    }
    { print }' > "$broken"
  make_commit E "docker/docker-compose.yml=$broken"
  make_images "$COMMIT"
  release v1.4.0 "$COMMIT"
  poll
  grep -q "recovering the release current names" "$PROOF/poll.log" || fail "run 6: no recovery ran: $(cat "$PROOF/poll.log")"
  test "$(live)" = "$a_id" || fail "run 6: current names $(live), not A"
  stand_is_current
  smoke

  echo "run 7: the unit killed right after the first switch, then a tick"
  make_commit F $(settings F)
  make_images "$COMMIT"
  release v1.5.0 "$COMMIT"
  local since tries=0
  since="$(date +%s)"
  systemctl start --no-block dm3-release.service
  until journalctl --quiet -o cat --since "@$since" -u dm3-release.service | grep -q 'switched to next'; do
    tries=$((tries + 1))
    [ "$tries" -lt 600 ] || fail "run 7: the tick never switched to the twins"
    sleep 0.5
  done
  systemctl kill --signal=SIGKILL dm3-release.service
  until [ "$(systemctl is-active dm3-release.service || true)" != active ] && [ "$(systemctl is-active dm3-release.service || true)" != activating ]; do sleep 1; done
  systemctl reset-failed dm3-release.service || true
  test -e "$STATE/in-flight" || fail "run 7: the killed run left no marker"
  poll
  test "$(live)" = "$a_id" || fail "run 7: current names $(live), not A"
  stand_is_current
  smoke
  lines="$(tried_lines v1.5.0)"
  poll
  test "$(tried_lines v1.5.0)" = "$lines" || fail "run 7: the tag was tried again"

  echo "run 9: the timer's unit on a good release; meanwhile deploy and a second start"
  make_commit G $(settings G)
  make_images "$COMMIT"
  release v1.6.0 "$COMMIT"
  probe_start
  since="$(date +%s)"
  systemctl start --no-block dm3-release.service
  tries=0
  until journalctl --quiet -o cat --since "@$since" -u dm3-release.service | grep -q 'v1.6.0 verified'; do
    tries=$((tries + 1))
    [ "$tries" -lt 240 ] || fail "run 9: the tick did not verify v1.6.0"
    sleep 0.5
  done
  if "$ENTRY" deploy v1.6.0 > "$PROOF/deploy.log" 2>&1; then
    fail "run 9: deploy ran beside the timer's tick"
  fi
  grep -q "holds the lock" "$PROOF/deploy.log" || fail "run 9: deploy did not exit on the lock: $(cat "$PROOF/deploy.log")"
  systemctl start dm3-release.service
  test "$(journalctl --quiet -o cat --since "@$since" -u dm3-release.service | grep -c 'is live as')" = 1 ||
    fail "run 9: the second start ran a second tick"
  zero_failure "run 9"
  [[ $(live) == v1.6.0-* ]] || fail "run 9: current names $(live), not G"

  echo "run 10: releases that need maintenance are refused, then applied by maintain"
  local alerts edge postgres script
  # shellcheck disable=SC2016 # a sed program: $a appends after the last line
  alerts="$(edited docker/prometheus/alerts.yml '$a\
# proof')"
  # shellcheck disable=SC2016 # a sed program: $a appends after the last line
  edge="$(edited docker/nginx/edge-locations.conf '$a\
# proof')"
  postgres="$(edited docker/docker-compose.yml 's/"max_connections=160"\]/"max_connections=160", "-c", "log_lock_waits=on"]/')"
  # shellcheck disable=SC2016 # a sed program: $a appends after the last line
  script="$(edited docker/scripts/release-entry.sh '$a\
# proof')"
  local version kind
  for kind in "1 alerts" "2 edge" "3 postgres" "4 script"; do
    version="v1.7.$((${kind%% *} - 1))"
    case "${kind#* }" in
      alerts) make_commit H1 $(settings H) "$alerts" ;;
      edge) make_commit H2 $(settings H) "$edge" ;;
      postgres) make_commit H3 "$postgres" ;;
      script) make_commit H4 $(settings H) "$script" ;;
    esac
    make_images "$COMMIT"
    release "$version" "$COMMIT"
    untouched "a release that changes ${kind#* }" "needs dm3-release maintain"
    lines="$(tried_lines "$version")"
    poll
    test "$(tried_lines "$version")" = "$lines" || fail "run 10: $version was tried again"
  done
  local prometheus
  prometheus="$(container_id dm-prometheus)"
  "$ENTRY" maintain v1.7.0 || fail "run 10: maintain did not apply the release that changes a monitoring file"
  [[ $(live) == v1.7.0-* ]] || fail "run 10: maintain left current on $(live)"
  # current moving is not the file applied: the container must be new and read the new file.
  test "$(container_id dm-prometheus)" != "$prometheus" || fail "run 10: maintain did not recreate dm-prometheus"
  test "$(docker exec dm-prometheus tail -n 1 /etc/prometheus/alerts.yml)" = "# proof" ||
    fail "run 10: dm-prometheus does not read the alerts.yml of v1.7.0"
  local pg
  pg="$(container_id dm-pg)"
  for version in v1.8.0 v1.8.1; do
    make_commit "$version" $(settings "$version") "$alerts"
    make_images "$COMMIT"
    release "$version" "$COMMIT"
    poll
    [[ $(live) == "$version"-* ]] || fail "run 10: $version did not go live: $(cat "$PROOF/poll.log")"
  done
  make_commit K $(settings v1.8.1) "$alerts" "$script"
  make_images "$COMMIT"
  release v1.9.0 "$COMMIT"
  "$ENTRY" maintain v1.9.0 || fail "run 10: maintain of a changed script failed"
  make_commit L $(settings L) "$alerts" "$script"
  make_images "$COMMIT"
  release v1.10.0 "$COMMIT"
  poll
  [[ $(live) == v1.10.0-* ]] || fail "run 10: the release after the maintain did not go live: $(cat "$PROOF/poll.log")"
  test "$(container_id dm-pg)" = "$pg" || fail "run 10: PostgreSQL was recreated"
  stand_is_current

  echo "run 11: dmapi recreated the plain way turns the verdict red"
  probe_start
  compose_cmd "$REL/current" up -d --no-deps --no-build --pull never --force-recreate dmapi
  sleep 20
  # The edge holds the old address until it reloads; the stand is put back for run 12.
  docker exec dm-nginx nginx -s reload
  probe_stop
  if node "$PROBE" verdict --log "$PROOF/probe.ndjson" --windows "$PROOF/windows" > /dev/null; then
    fail "run 11: the probe saw nothing of an API recreated under it"
  fi
  echo "  red, as it must be: run 11"

  echo "run 12: tags that must change nothing, each followed by a tick"
  local live_commit side
  live_commit="$(env_value "$REL/current/release.env" RELEASE_COMMIT)"
  git -C "$CLONE" tag v20.0.0 "$live_commit" && as_signer git -C "$CLONE" push -q origin v20.0.0 && git -C "$CLONE" tag -d v20.0.0 > /dev/null
  untouched "a lightweight tag" "lightweight"
  push_tag v20.0.1 v20.0.1 none "$live_commit"
  untouched "an unsigned tag" "is not signed"
  push_tag v20.0.2 v20.0.2 stray-key "$live_commit"
  untouched "a tag signed with a key that is not pinned" "not signed by a pinned key"
  cp /etc/dm3/release-signers "$PROOF/signers.kept"
  : > /etc/dm3/release-signers
  push_tag v20.0.3 v20.0.3 release-key "$live_commit"
  untouched "a tag whose key line was removed" "not signed by a pinned key"
  cat "$PROOF/signers.kept" > /etc/dm3/release-signers
  push_tag v20.0.4 v20.0.5 release-key "$live_commit"
  untouched "a signed tag pushed under a higher name" "names itself v20.0.4"
  push_tag v20.0.6 v20.0.6 release-key "$live_commit"
  as_signer git -C "$CLONE" fetch -q origin "refs/tags/v20.0.6:refs/tags/v20.0.6"
  { git -C "$CLONE" cat-file tag v20.0.6; printf 'commit: %s\n' "$A"; } > "$PROOF/appended"
  git -C "$CLONE" update-ref refs/tags/v20.0.6 "$(git -C "$CLONE" hash-object -t tag -w --literally --stdin < "$PROOF/appended")"
  as_signer git -C "$CLONE" push -q -f origin refs/tags/v20.0.6 && git -C "$CLONE" tag -d v20.0.6 > /dev/null
  untouched "bytes after the signature" "bytes after its signature"
  push_tag v20.0.7 v20.0.7 release-key "$live_commit" front=
  untouched "a field missing" "no front field"
  push_tag v20.0.8 v20.0.8 release-key "$live_commit" "api: $(env_value "$REL/current/release.env" DM_IMAGE_API)"
  untouched "a field twice" "api twice"
  push_tag v20.0.9 v20.0.9 release-key "$live_commit" "schema: reset"
  untouched "an extra field" "not a field of a release"
  push_tag v20.0.10 v20.0.10 release-key "$live_commit" "api=api: registry.test/elsewhere/dm-api@sha256:$(printf 'a%.0s' {1..64})"
  untouched "a reference under another name" "not ${PREFIX}dm-api@sha256"
  push_tag v20.0.11 v20.0.11 release-key "$live_commit" "commit=commit: $A"
  untouched "commit not the target" "not the commit the tag points at"
  push_tag v1.0.1 v1.0.1 release-key "$live_commit"
  local before
  before="$(live)"
  poll
  test "$(live)" = "$before" || fail "a version not above the live one moved current"
  ! grep -q "v1.0.1 verified" "$PROOF/poll.log" || fail "a version not above the live one was looked at"
  echo "  untouched: a version not above the live one"
  side="$(git -C "$CLONE" commit-tree -p "$A" -m side "$(git -C "$CLONE" rev-parse "$A^{tree}")")"
  push_tag v20.1.0 v20.1.0 release-key "$side" "commit=commit: $side"
  untouched "a commit that does not descend from the live one" "does not descend"
  local name
  # shellcheck disable=SC2016 # names with shell syntax in them, kept literal on purpose
  for name in v030.0.0 "v$(printf '3%.0s' {1..30}).0.0" 'v30.0.0;id' 'v30.0.0$(id)'; do
    git -C "$CLONE" tag "$name" "$live_commit" && as_signer git -C "$CLONE" push -q origin "refs/tags/$name" && git -C "$CLONE" tag -d "$name" > /dev/null
    untouched "the name $name" "passed over the tag"
  done
  push_tag v20.2.0 v20.2.0 release-key "$live_commit"
  as_signer git -C "$CLONE" push -q origin :refs/tags/v20.2.0
  poll
  ! grep -q v20.2.0 "$PROOF/poll.log" || fail "a tag deleted before the tick was seen"
  echo "  untouched: a tag deleted before the tick"
  mv "$PROOF/origin.git" "$PROOF/origin.away"
  untouched "an unreachable origin" "cannot be listed"
  mv "$PROOF/origin.away" "$PROOF/origin.git"
  push_tag v20.3.0 v20.3.0 release-key "$live_commit" "api=api: ${PREFIX}dm-api@sha256:$(printf '0%.0s' {1..64})"
  untouched "a digest the registry does not hold" "waits: an image could not be pulled"
  test "$(tried_lines v20.3.0)" = 0 || fail "a tag whose image could not be pulled was listed as tried"
  smoke

  echo "OK: releases roll out with no failed request, and nothing that must not land does."
}

case "${1:-}" in
  '') offline ;;
  --layout) layout ;;
  *) fail "unknown argument ${1}; expected none or --layout" ;;
esac
