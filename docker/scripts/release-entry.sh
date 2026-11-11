#!/bin/bash
# dm3-release, installed as /usr/local/sbin/dm3-release from a release directory. It is the
# one thing on a server that changes which release runs, so everything it accepts comes from a
# tag the pinned key signed, and everything it runs goes through one compose command line.
# The layout and the reasons are in the release design (3.2 to 3.7); this file holds the
# mechanism.
#
# Usage, as root:
#   dm3-release poll                                one tick: the highest new signed tag, rolled out
#   dm3-release deploy <tag>                        a tick for one tag, the tried list ignored
#   dm3-release rollback                            roll back to the release the live one replaced
#   dm3-release maintain <tag> [--reset-database]   install a verified tag, downtime allowed
#   dm3-release recover                             start what current names, edge on the primaries
#   dm3-release boot | stop                         the dm3 unit's start and stop
#   dm3-release status                              what is live and how its containers are
#
# Sourced rather than run, it only defines functions: check-release-entry.sh calls them one by
# one. The roots move only through DM3_ROOT, DM3_ETC and DM3_STATE, for that check.
set -euo pipefail
# Every comparison and every message below is about bytes; a locale would change both.
export LC_ALL=C

DM3_ROOT="${DM3_ROOT:-/opt/dm3}"
DM3_ETC="${DM3_ETC:-/etc/dm3}"
DM3_STATE="${DM3_STATE:-/var/lib/dm3}"

REPO="$DM3_ROOT/repo.git"
REL="$DM3_ROOT/rel"
SHARED="$DM3_ROOT/shared"
SIGNERS="$DM3_ETC/release-signers"
CONF="$DM3_ETC/release.conf"
MARKER="$DM3_STATE/in-flight"
SCRATCH="$DM3_STATE/scratch"
# One line per decision about a tag, appended and never rewritten: the timer takes no version at
# or below one listed here, and the newest failure in it is what ReleaseFailed reports.
TRIED="$DM3_STATE/tried"
# Object ids that failed verification, so a writer's tag costs one fetch, not one per tick.
REJECTED="$DM3_STATE/rejected"
WAITING="$DM3_STATE/waiting"
FETCHED="$DM3_STATE/fetched"
RELEASE_USER=dm-release
# Where the node exporter's textfile collector reads, as for the backup check.
TEXTFILE_DIR="${TEXTFILE_DIR:-/var/lib/node_exporter/textfile}"
MEMINFO=/proc/meminfo
# A tick verifies at most this many tags, so a flood of pushed tags cannot hold the lock for long.
MAX_CANDIDATES=20
# What a rollout replaces; any other service changes only by maintain (3.8).
APP_SERVICES='["migration","dmapi","dmapi-next","dmfront","dmfront-next","dm-mail-worker","dm-notification-worker"]'
# The return code of a release that waits: the site is untouched and the next tick asks again.
WAITS=3

# What an unverified tag may cost: its fetch stops after two minutes or at a file of 256 MiB
# (ulimit counts in 1024-byte blocks), whatever a writer pushes under a release-shaped name.
FETCH_TIMEOUT=120
FETCH_FILE_BLOCKS=262144
# One image pull under the lock: a registry that stops answering would otherwise hold it for good.
PULL_TIMEOUT=900

refuse() {
    printf 'dm3-release: refused: %s\n' "$*" >&2
    return 1
}

say() {
    printf 'dm3-release: %s\n' "$*"
}

# The strict form, matched whole before the name is used for anything: a name is passed to git
# and printed, and anything a writer can push under refs/tags/ reaches this line first.
valid_tag_name() {
    [[ $1 =~ ^v(0|[1-9][0-9]{0,5})(\.(0|[1-9][0-9]{0,5})){2}$ ]]
}

refuse_bad_name() {
    refuse "the tag name $(printf '%q' "$1") is not vMAJOR.MINOR.PATCH with numbers of at most six digits"
}

# git in the mirror and the scratch repositories runs as the account that owns them, never as
# root: a repository a writer filled is then read with no more rights than that account has,
# and git refuses a repository another user owns anyway. Unprivileged, as in the check, there is
# no one to switch to. Its own home and a readable directory: under sudo HOME is still /root,
# and git dies on a configuration file it may not read.
as_release_user() {
    if [ "$(id -u)" -eq 0 ]; then
        (cd / && runuser -u "$RELEASE_USER" -- env -u XDG_CONFIG_HOME HOME="$DM3_STATE" "$@")
    else
        "$@"
    fi
}

release_git() {
    as_release_user git "$@"
}

# git that talks to the origin, under the time limit: the timer's unit has no start timeout, so a
# connection that hangs would hold the lock from every later tick and from the operator's verbs.
release_git_timed() {
    as_release_user timeout "$FETCH_TIMEOUT" git "$@"
}

# And under the size limit too, for a fetch that writes whatever the origin sends.
release_git_bounded() {
    # shellcheck disable=SC2016 # expanded by the shell bash -c starts
    as_release_user timeout "$FETCH_TIMEOUT" bash -c 'ulimit -f "$1" && shift && exec "$@"' _ \
        "$FETCH_FILE_BLOCKS" git "$@"
}

# One key of release.conf, read as text: the file is the operator's, and sourcing it would run
# whatever it holds as root.
conf_value() {
    local key=$1 value
    value="$(sed -n "s/^${key}=//p" "$CONF" 2> /dev/null | tail -n 1)"
    [ -n "$value" ] || { refuse "$CONF names no $key"; return; }
    printf '%s\n' "$value"
}

# The id of the annotated tag object the origin holds under the name, without fetching it. The
# whole listing, filtered here, because a pattern given to ls-remote matches the tail of a ref.
remote_tag_id() {
    local name=$1 origin listing tag peeled
    valid_tag_name "$name" || { refuse_bad_name "$name"; return; }
    origin="$(conf_value origin)" || return
    case "$origin" in
        -*) refuse "the origin in $CONF starts with a dash"; return ;;
    esac
    listing="$(release_git_timed -C "$DM3_STATE" ls-remote --tags "$origin" 2> /dev/null)" ||
        { refuse "the origin $origin cannot be reached"; return; }
    tag="$(awk -v ref="refs/tags/$name" '$2 == ref { print $1 }' <<< "$listing")"
    peeled="$(awk -v ref="refs/tags/$name^{}" '$2 == ref { print $1 }' <<< "$listing")"
    [ -n "$tag" ] || { refuse "$name is not on the origin"; return; }
    # Only an annotated tag has a peeled line, and only it can carry a signature.
    [[ -n $peeled && $peeled != "$tag" ]] || { refuse "$name is a lightweight tag"; return; }
    printf '%s\n' "$tag"
}

# The tag fetched alone into a new scratch repository, under the time and size limit. Prints the
# repository's path; the caller removes it.
scratch_fetch() {
    local name=$1 id=$2 origin dir got
    origin="$(conf_value origin)" || return
    dir="$(as_release_user mktemp -d "$SCRATCH/tag.XXXXXX")" || { refuse "no scratch repository in $SCRATCH"; return; }
    if ! release_git init -q --bare "$dir" ||
        ! release_git_bounded -C "$dir" fetch -q --no-tags --depth=1 "$origin" \
            "+refs/tags/$name:refs/tags/$name" 2> /dev/null; then
        rm -rf "$dir"
        refuse "$name could not be fetched within ${FETCH_TIMEOUT} s and the size limit"
        return
    fi
    got="$(release_git -C "$dir" rev-parse -q --verify "refs/tags/$name" 2> /dev/null || true)"
    if [ "$got" != "$id" ]; then
        rm -rf "$dir"
        refuse "the origin moved $name between the listing and the fetch"
        return
    fi
    printf '%s\n' "$dir"
}

# Verification of one tag object (3.5): its header names it and a commit, its signed text is
# everything before the signature block and nothing follows the block, the pinned key signed it
# in the git namespace, and its fields are the five of a release. Prints the fields as
# key=value lines, and signed=<sha256 of the signed text>. Nothing read here is evaluated.
verify_tag_object() (
    repo=$1
    name=$2
    id=$3
    valid_tag_name "$name" || { refuse_bad_name "$name"; exit 1; }
    [[ $id =~ ^[0-9a-f]{40}$ ]] || { refuse "$name: $(printf '%q' "$id") is not an object id"; exit 1; }
    type="$(release_git -C "$repo" cat-file -t "$id" 2> /dev/null || true)"
    [ -n "$type" ] || { refuse "$name: the object $id is not in the repository"; exit 1; }
    [ "$type" = tag ] || { refuse "$name is a lightweight tag"; exit 1; }

    work="$(mktemp -d)" || { refuse "no temporary directory"; exit 1; }
    trap 'rm -rf "$work"' EXIT
    release_git -C "$repo" cat-file tag "$id" > "$work/raw" 2> /dev/null ||
        { refuse "$name: the tag object cannot be read"; exit 1; }

    object="$(sed -n '1s/^object //p' "$work/raw")"
    [[ $object =~ ^[0-9a-f]{40}$ ]] || { refuse "$name: the tag object names no object"; exit 1; }
    [ "$(sed -n 2p "$work/raw")" = "type commit" ] || { refuse "$name does not point at a commit"; exit 1; }
    inner="$(sed -n '3s/^tag //p' "$work/raw")"
    [ "$inner" = "$name" ] ||
        { refuse "$name names itself $(printf '%q' "$inner"): a signed tag pushed under another name"; exit 1; }

    begin="$(grep -a -n -x -- '-----BEGIN SSH SIGNATURE-----' "$work/raw" | head -n 1 | cut -d: -f1 || true)"
    [ -n "$begin" ] || { refuse "$name is not signed"; exit 1; }
    end="$(grep -a -n -x -- '-----END SSH SIGNATURE-----' "$work/raw" | head -n 1 | cut -d: -f1 || true)"
    [[ -n $end && $end -gt $begin ]] || { refuse "$name: the signature block is not closed"; exit 1; }
    head -n "$end" "$work/raw" | cmp -s - "$work/raw" ||
        { refuse "$name carries bytes after its signature"; exit 1; }
    head -n "$((begin - 1))" "$work/raw" > "$work/signed"
    sed -n "${begin},${end}p" "$work/raw" > "$work/signature"

    principal="$(ssh-keygen -Y find-principals -s "$work/signature" -f "$SIGNERS" 2> /dev/null | head -n 1 || true)"
    [ -n "$principal" ] || { refuse "$name is not signed by a pinned key"; exit 1; }
    ssh-keygen -Y verify -f "$SIGNERS" -I "$principal" -n git -s "$work/signature" \
        < "$work/signed" > /dev/null 2>&1 ||
        { refuse "the signature of $name does not verify for $principal in the git namespace"; exit 1; }

    parse_fields "$name" "$object" "$work/signed" || exit 1
    printf 'signed=%s\n' "$(sha256sum < "$work/signed" | cut -d' ' -f1)"
)

# The message of a signed tag: its title, then exactly the five fields, each once and of its
# form. Read only after the signature verified, and only as text.
parse_fields() (
    name=$1
    object=$2
    signed=$3
    prefix="$(conf_value image_prefix)" || exit 1
    declare -A image=([api]=dm-api [front]=dm-front [worker-mail]=dm-worker-mail
        [worker-notification]=dm-worker-notification)
    declare -A seen=()

    # The message starts after the first empty line of the tag object.
    awk 'body { print } !body && $0 == "" { body = 1 }' "$signed" > "$signed.message"
    title="$(grep -v -x -m 1 '' "$signed.message" || true)"
    [ "$title" = "DM3 release $name" ] ||
        { refuse "$name: the message does not open with \"DM3 release $name\""; exit 1; }

    titled=0
    while IFS= read -r line || [ -n "$line" ]; do
        [ -n "$line" ] || continue
        if [ "$titled" = 0 ]; then
            titled=1
            continue
        fi
        key="${line%%: *}"
        value="${line#*: }"
        if [ "$key" = "$line" ] || { [ "$key" != commit ] && [ -z "${image[$key]+x}" ]; }; then
            refuse "$name: $(printf '%q' "$line") is not a field of a release"
            exit 1
        fi
        [ -z "${seen[$key]+x}" ] || { refuse "$name names $key twice"; exit 1; }
        seen[$key]="$value"
    done < "$signed.message"

    for key in commit api front worker-mail worker-notification; do
        [ -n "${seen[$key]+x}" ] || { refuse "$name has no $key field"; exit 1; }
    done
    [ "${seen[commit]}" = "$object" ] ||
        { refuse "$name: commit ${seen[commit]} is not the commit the tag points at"; exit 1; }
    for key in api front worker-mail worker-notification; do
        value="${seen[$key]}"
        digest="${value#"$prefix${image[$key]}@"}"
        if [ "$value" != "$prefix${image[$key]}@$digest" ] || ! [[ $digest =~ ^sha256:[0-9a-f]{64}$ ]]; then
            refuse "$name: the $key reference is not $prefix${image[$key]}@sha256:<64 hex>"
            exit 1
        fi
    done
    for key in commit api front worker-mail worker-notification; do
        printf '%s=%s\n' "$key" "${seen[$key]}"
    done
)

# A tag from the origin, as far as the server is concerned: listed, fetched alone into scratch
# and verified there. Prints the fields and id=<tag object id>. A listing or a fetch that fails
# returns WAITS: an origin that did not answer says nothing of the tag, and only a refused
# verification may reject it for good.
fetch_and_verify() {
    local name=$1 id dir fields status=0
    id="$(remote_tag_id "$name")" || return "$WAITS"
    dir="$(scratch_fetch "$name" "$id")" || return "$WAITS"
    fields="$(verify_tag_object "$dir" "$name" "$id")" || status=$?
    rm -rf "$dir"
    [ "$status" -eq 0 ] || return "$status"
    printf '%s\nid=%s\n' "$fields" "$id"
}

# The tag into the mirror, by the object id that verified: the content is addressed by it, so
# the same id is the same signed bytes.
mirror_fetch() {
    local name=$1 id=$2 origin got
    origin="$(conf_value origin)" || return
    release_git_bounded -C "$REPO" fetch -q --no-tags "$origin" "+refs/tags/$name:refs/tags/$name" ||
        { refuse "$name could not be fetched into the mirror"; return; }
    got="$(release_git -C "$REPO" rev-parse -q --verify "refs/tags/$name" || true)"
    [ "$got" = "$id" ] || { refuse "the origin moved $name after it verified"; return; }
}

SCHEMA_SOURCES=(src/DM.Infrastructure.Persistence/Migrations
    src/DM.Infrastructure.Persistence/RelationalStorage/ExpressionIndexInitializer.cs)

manifest_of() {
    release_git -C "$REPO" ls-tree -r "$1" -- "${SCHEMA_SOURCES[@]}"
}

# The regime of 3.9 decides what a schema change may be. In migrate an existing migration never
# changes, since the database holds what it applied; in reset any change needs the operator's
# --reset-database, which wipes what the stand holds. The model snapshot changes with every
# migration and says nothing of its own; the index initializer counts as existing unless a
# migration comes with it. A server with no release yet has no schema to keep.
refuse_schema_change() {
    local regime=$1 reset=$2 live=$3 candidate=$4 name=$5 difference changed added
    if [ "$regime" = migrate ] && [ "$reset" = 1 ]; then
        refuse "--reset-database in regime migrate: the database holds what it applied"
        return
    fi
    [ -f "$live" ] || return 0
    # Each line tagged with its side, so an empty manifest still counts as one.
    difference="$({ sed 's/^/live\t/' "$live"; sed 's/^/candidate\t/' "$candidate"; } | awk -F '\t' '
        $3 ~ /ModelSnapshot\.cs$/ { next }
        $1 == "live" { live[$3] = $2; next }
        { candidate[$3] = $2 }
        END {
            for (path in live) if (!(path in candidate) || candidate[path] != live[path]) print "changed\t" path
            for (path in candidate) if (!(path in live)) print "added\t" path
        }' | sort)"
    added="$(awk -F '\t' '$1 == "added" { print $2 }' <<< "$difference")"
    changed="$(awk -F '\t' -v with_migration="${added:+1}" '
        $1 == "changed" && !(with_migration && $2 ~ /ExpressionIndexInitializer\.cs$/) { print $2 }' <<< "$difference")"
    case "$regime" in
        migrate)
            [ -z "$changed" ] ||
                { refuse "$name: an existing migration changed ($(tr '\n' ' ' <<< "$changed")), which regime migrate never takes"; return; }
            ;;
        *)
            [ "$reset" = 1 ] || [ -z "$difference" ] ||
                { refuse "$name changes the schema in regime reset; dm3-release maintain $name --reset-database wipes the database and takes it"; return; }
            ;;
    esac
}

# One value of an environment file, read as text rather than sourced: shared/env is the
# operator's, release.env the release's, and neither is run as root.
env_value() {
    sed -n "s/^$2=//p" "$1" 2> /dev/null | tail -n 1
}

schema_regime() {
    local regime
    regime="$(env_value "$SHARED/env" DM_SCHEMA_REGIME)"
    printf '%s\n' "${regime:-reset}"
}

# The tag, the short commit and 12 hex of the signed text: a tag re-signed over other bytes is a
# different release under the same name.
release_id() {
    printf '%s-%s-%s\n' "$1" "${2:0:7}" "${3:0:12}"
}

valid_release_id() {
    [[ $1 =~ ^v[0-9.]+-[0-9a-f]{7}-[0-9a-f]{12}$ ]]
}

# rel/<id>: the commit's docker/ tree, release.env and schema.manifest, built aside and renamed
# into place read-only. An id that exists is the same signed bytes and is kept as it is.
build_release_dir() {
    local id=$1 tag=$2 commit=$3 replaces=$4 api=$5 front=$6 mail=$7 notification=$8
    local dir="$REL/$id" tmp="$REL/.building-$id"
    if [ -d "$dir" ]; then
        printf '%s\n' "$dir"
        return 0
    fi
    if [ -e "$tmp" ]; then
        chmod -R u+w "$tmp" && rm -rf "$tmp"
    fi
    mkdir -p "$tmp"
    release_git -C "$REPO" archive --format=tar "$commit" docker | tar -x -C "$tmp" ||
        { refuse "the docker/ tree of $commit could not be exported"; return; }
    printf '%s\n' "RELEASE_TAG=$tag" "RELEASE_COMMIT=$commit" "RELEASE_REPLACES=$replaces" \
        "DM_IMAGE_API=$api" "DM_IMAGE_FRONT=$front" "DM_IMAGE_WORKER_MAIL=$mail" \
        "DM_IMAGE_WORKER_NOTIFICATION=$notification" "SOURCE_REVISION=$commit" > "$tmp/release.env"
    manifest_of "$commit" > "$tmp/schema.manifest" ||
        { refuse "the schema sources of $commit could not be listed"; return; }
    chmod -R a-w "$tmp"
    mv "$tmp" "$dir"
    printf '%s\n' "$dir"
}

# The id rel/current names, or nothing before the first release.
live_id() {
    local id
    id="$(readlink "$REL/current" 2> /dev/null || true)"
    if [ -n "$id" ] && valid_release_id "$id"; then
        printf '%s\n' "$id"
    fi
}

# The commit point: a new link renamed over the old one, so current is never absent.
commit_current() {
    rm -f "$REL/.current.new"
    ln -s "$1" "$REL/.current.new"
    mv -T "$REL/.current.new" "$REL/current"
}

# The one compose command line (3.2). The project directory goes through current, so a relative
# mount lands on the same path whatever release is live, while the definitions and release.env
# are the given release's. The edge's machine files come from where the release's own helper
# puts them. No profile but the rollout one, which only the rollout passes.
compose_cmd() {
    local dir=$1 edge
    shift
    # shellcheck disable=SC2016 # expanded by the shell bash -c starts
    edge="$(env -u DM_LAYOUT_ROOT bash -c '. "$1/docker/scripts/_paths.sh" && printf "%s" "$DM_EDGE_DIR"' _ "$dir")"
    DM_EDGE_DIR="$edge" docker compose --project-directory "$REL/current/docker" --env-file "$SHARED/env" --env-file "$dir/release.env" -f "$dir/docker/docker-compose.yml" -f "$dir/docker/docker-compose.preview.yml" "$@"
}

# One lock for every verb that changes something, held by this shell until it exits. boot and
# stop wait for it, since the unit must not fail on a run in progress; the others leave, since a
# second maintain behind the first would apply a stale decision.
take_lock() {
    local mode=$1
    command -v flock > /dev/null || { refuse "flock is missing"; return; }
    mkdir -p "$DM3_STATE"
    exec 9>> "$DM3_STATE/lock"
    if [ "$mode" = wait ]; then
        flock 9
    elif ! flock -n 9; then
        refuse "another dm3-release run holds the lock $DM3_STATE/lock"
        return
    fi
}

# maintain and recover outlive the session that started them: a dropped ssh connection in the
# middle of a recreate would otherwise leave the stand half replaced. The journal is printed
# while the unit runs, and its exit status is the command's.
run_in_unit() {
    local verb=$1 unit="dm3-release-$1" since follower status=0
    shift
    since="@$(date +%s)"
    journalctl --quiet --follow --output cat --unit "$unit" --since "$since" &
    follower=$!
    systemd-run --quiet --unit "$unit" --collect --wait \
        --setenv DM3_IN_UNIT=1 --setenv "DM3_ROOT=$DM3_ROOT" --setenv "DM3_ETC=$DM3_ETC" \
        --setenv "DM3_STATE=$DM3_STATE" \
        "$(readlink -f "${BASH_SOURCE[0]}")" "$verb" "$@" || status=$?
    # The last lines reach the journal after the unit ends.
    sleep 1
    kill "$follower" 2> /dev/null || true
    wait "$follower" 2> /dev/null || true
    return "$status"
}

# The four by digest, each labelled with the commit it claims; a mismatched label is an image the
# signer did not mean, whatever its digest.
pull_images() {
    local commit=$1 ref label
    shift
    for ref in "$@"; do
        # 2, not 1: a registry that does not answer is a release that waits, not a refused one.
        timeout "$PULL_TIMEOUT" docker pull -q "$ref" > /dev/null || { refuse "$ref could not be pulled"; return 2; }
        label="$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$ref")"
        [ "$label" = "$commit" ] ||
            { refuse "$ref was built from ${label:-an unlabelled commit}, not $commit"; return; }
    done
}

# The images outside the application, by the references the compose files of the release (current
# by default) give, only when absent: that is today's trust, taken at the moment the operator chose.
pull_missing_images() {
    local dir=${1:-$REL/current} image
    while IFS= read -r image; do
        [ -n "$image" ] || continue
        docker image inspect "$image" > /dev/null 2>&1 && continue
        timeout "$PULL_TIMEOUT" docker pull -q "$image" > /dev/null || { refuse "$image could not be pulled"; return; }
    done < <(compose_cmd "$dir" config --images | sort -u)
}

# The edge's links set to one pair, tested and reloaded in the container; a failed test puts the
# links back, so the configuration the master runs is never one that did not parse.
edge_switch() {
    # shellcheck disable=SC2016 # expanded by the shell in the edge container
    compose_cmd "$REL/current" exec -T nginx sh -c '
        live=/etc/nginx/upstreams-live
        files=/etc/nginx/upstreams
        api="$(readlink "$live/api.conf")"
        front="$(readlink "$live/frontend.conf")"
        ln -sfn "$files/api-$1.conf" "$live/api.conf"
        ln -sfn "$files/front-$1.conf" "$live/frontend.conf"
        if nginx -t -q; then
            exec nginx -s reload
        fi
        ln -sfn "$api" "$live/api.conf"
        ln -sfn "$front" "$live/frontend.conf"
        exit 1' sh "$1" || { refuse "the edge did not take the $1 pair"; return; }
}

edge_exec() {
    compose_cmd "$REL/current" exec -T nginx "$@"
}

# The API's request counter, summed over its series, read from inside the edge; with a second
# argument only the series of 5xx answers. An instance that is not there counts 0, which is what
# the switch check wants of an old instance that recovery already removed.
request_count() {
    { edge_exec curl -s "http://$1:5000/metrics" 2> /dev/null || true; } |
        awk -v errors="${2:-}" '/^http_server_request_duration_seconds_count[{ ]/ {
            if (errors && $0 !~ /http_response_status_code="5[0-9][0-9]"/) next
            sum += $NF
        } END { printf "%d\n", sum }'
}

api_host() {
    case "$1" in
        primary) echo dmapi ;;
        next) echo dmapi-next ;;
    esac
}

# The switch check (3.6) towards one pair: through the loaded configuration, 20 readiness
# answers of 200 that the pair's API counted while the old pair's counted fewer than 5, and the
# frontend that answers is the pair's. The counters are read again for up to 3 s, since the
# metrics endpoint answers from a cache. With seconds, the pair then serves that long at two
# requests a second with no failure and no 5xx on its counter.
switch_check() {
    local pair=$1 old=${2:-} seconds=${3:-0} api front tries before after codes instance hostname
    local old_api="" old_before=0 old_after=0 errors_before errors_after
    api="$(api_host "$pair")"
    case "$pair" in
        primary) front=dm-front ;;
        next) front=dm-front-next ;;
    esac
    [ -z "$old" ] || old_api="$(api_host "$old")"
    tries=0
    until [ "$(edge_exec curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8081/_ready 2> /dev/null || true)" = 200 ]; do
        tries=$((tries + 1))
        [ "$tries" -lt 90 ] || { refuse "the $pair pair is not ready through the edge after 180 s"; return; }
        sleep 2
    done
    before="$(request_count "$api")"
    [ -z "$old_api" ] || old_before="$(request_count "$old_api")"
    # shellcheck disable=SC2016 # expanded by the shell in the edge container
    codes="$(edge_exec sh -c 'for i in $(seq 20); do curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8081/_ready; done')"
    [ "$(grep -cx 200 <<< "$codes" || true)" = 20 ] ||
        { refuse "of 20 requests through the edge to the $pair pair not all answered 200: $(tr '\n' ' ' <<< "$codes")"; return; }
    tries=0
    while :; do
        after="$(request_count "$api")"
        [ "$((after - before))" -lt 20 ] || break
        tries=$((tries + 1))
        [ "$tries" -lt 6 ] || { refuse "$api counted $((after - before)) of the 20 requests: the edge serves another API"; return; }
        sleep 0.5
    done
    if [ -n "$old_api" ]; then
        old_after="$(request_count "$old_api")"
        [ "$((old_after - old_before))" -lt 5 ] ||
            { refuse "$old_api still counted $((old_after - old_before)) requests after the switch to the $pair pair"; return; }
    fi
    instance="$(edge_exec curl -s http://127.0.0.1:8081/_instance)"
    hostname="$(docker inspect --format '{{.Config.Hostname}}' "$front")"
    [ "$instance" = "$hostname" ] ||
        { refuse "the edge serves the frontend $(printf '%q' "$instance"), not $front ($hostname)"; return; }
    [ "$seconds" -gt 0 ] || return 0

    errors_before="$(request_count "$api" 5xx)"
    # shellcheck disable=SC2016 # expanded by the shell in the edge container
    codes="$(edge_exec sh -c 'end=$(($(date +%s) + $1)); while [ "$(date +%s)" -lt "$end" ]; do
        curl -s -o /dev/null --max-time 5 -w "%{http_code}\n" http://127.0.0.1:8081/_ready; sleep 0.5; done' sh "$seconds")"
    [ -z "$(grep -vx 200 <<< "$codes" || true)" ] ||
        { refuse "the $pair pair failed requests within $seconds s: $(grep -vx 200 <<< "$codes" | sort | uniq -c | tr '\n' ' ')"; return; }
    errors_after="$(request_count "$api" 5xx)"
    [ "$errors_after" = "$errors_before" ] ||
        { refuse "$api answered $((errors_after - errors_before)) requests with 5xx within $seconds s"; return; }
}

# Services whose bind-mounted files under the project directory differ from the previous
# release's: docker resolved current when it created them, so they still read the old files.
services_with_changed_mounts() {
    local prev=$1 base="$REL/current/docker/" mounts service relative
    # shellcheck disable=SC2016 # a jq program, whose variables jq expands
    mounts="$(compose_cmd "$REL/current" config --format json | jq -r --arg base "$base" '
        .services | to_entries[] | .key as $service | (.value.volumes // [])[]
        | select(.type == "bind" and (.source | startswith($base)))
        | "\($service)\t\(.source | ltrimstr($base))"')" ||
        { refuse "the configuration of the release could not be resolved"; return; }
    while IFS=$'\t' read -r service relative; do
        [ -n "$service" ] || continue
        diff -r -q "$REL/$prev/docker/$relative" "$REL/current/docker/$relative" > /dev/null 2>&1 ||
            printf '%s\n' "$service"
    done <<< "$mounts" | sort -u
}

# Recovery (3.7): start what current names from its own definitions, put the edge on the
# primaries with the check, remove the twins and the marker. With a previous release, as in
# maintain, also recreate what reads changed files and remove what the release no longer defines.
recover_stack() {
    local prev=${1:-} changed recreate=()
    local up=(up -d --no-build --pull never)
    if [ -n "$prev" ]; then
        compose_cmd "$REL/current" "${up[@]}" --remove-orphans || { refuse "compose could not start the release"; return; }
        changed="$(services_with_changed_mounts "$prev")" || return
        if [ -n "$changed" ]; then
            mapfile -t recreate <<< "$changed"
            say "recreating what reads changed files: ${recreate[*]}"
            compose_cmd "$REL/current" "${up[@]}" --no-deps --force-recreate "${recreate[@]}" ||
                { refuse "compose could not recreate ${recreate[*]}"; return; }
        fi
    else
        compose_cmd "$REL/current" "${up[@]}" || { refuse "compose could not start the release"; return; }
    fi
    # The edge starts last under a quarter of a CPU, and nginx -s reload fails until its master
    # has written the pid file.
    wait_healthy dm-nginx 90 || return
    edge_switch primary || return
    switch_check primary || return
    compose_cmd "$REL/current" --profile rollout rm -s -f dmapi-next dmfront-next > /dev/null ||
        { refuse "the twins could not be removed"; return; }
    rm -f "$MARKER"
}

# The script, the units and the cron lines of the live release. The script is renamed into
# place, since bash reads a running script as it goes and an overwrite would change it under
# the run that is installing it.
install_machinery() {
    local src="$REL/current/docker" unit reload=0
    install -m 755 "$src/scripts/release-entry.sh" /usr/local/sbin/.dm3-release.new
    mv -f /usr/local/sbin/.dm3-release.new /usr/local/sbin/dm3-release
    for unit in dm3.service dm3-release.service dm3-release.timer; do
        cmp -s "$src/$unit" "/etc/systemd/system/$unit" && continue
        install -m 644 "$src/$unit" "/etc/systemd/system/.$unit.new"
        mv -f "/etc/systemd/system/.$unit.new" "/etc/systemd/system/$unit"
        reload=1
    done
    if [ "$reload" = 1 ]; then
        systemctl daemon-reload
    fi
    env -u DM_LAYOUT_ROOT bash "$src/scripts/install-cron.sh" > /dev/null
}

# Release directories other than the live one and the one it replaced, unless a container still
# mounts a file from one: a container outside the application keeps the files of the release
# that was live when it was created.
prune_release_dirs() {
    local keep prev path id
    keep="$(live_id)"
    prev="$(env_value "$REL/$keep/release.env" RELEASE_REPLACES)"
    for path in "$REL"/*/; do
        id="$(basename "$path")"
        [[ $id != current && $id != "$keep" && $id != "$prev" ]] || continue
        valid_release_id "$id" || continue
        if grep -qsF "/rel/$id/" /proc/[0-9]*/mountinfo; then
            say "kept $id: a running container mounts a file from it"
            continue
        fi
        chmod -R u+w "$REL/$id" && rm -rf "${REL:?}/$id"
    done
}

# The database volume of the stand, in regime reset only, with the stack down first.
reset_database() {
    if [ -n "$(live_id)" ]; then
        compose_cmd "$REL/current" --profile rollout down || { refuse "the stack could not be stopped"; return; }
    fi
    if docker volume inspect dm_pgdata > /dev/null 2>&1; then
        docker volume rm dm_pgdata > /dev/null || { refuse "the database volume could not be removed"; return; }
        say "the database volume was removed"
    fi
}

# The fields fetch_and_verify printed, into the F_ globals every verb after it reads.
take_fields() {
    local key value
    F_COMMIT="" F_API="" F_FRONT="" F_MAIL="" F_NOTIFICATION="" F_SIGNED="" F_ID=""
    while IFS='=' read -r key value; do
        case "$key" in
            commit) F_COMMIT=$value ;;
            api) F_API=$value ;;
            front) F_FRONT=$value ;;
            worker-mail) F_MAIL=$value ;;
            worker-notification) F_NOTIFICATION=$value ;;
            signed) F_SIGNED=$value ;;
            id) F_ID=$value ;;
        esac
    done <<< "$1"
}

# The marker names the run, so the tick that finds it lists the right tag as tried. A maintain
# also names the release it replaced: its containers still read that release's files, so the
# recovery of a run that died recreates against it.
write_marker() {
    printf 'tag=%s\nsigned=%s\nprevious=%s\n' "$1" "$2" "${3:-}" > "$MARKER"
}

# The release the marker names as replaced, when it is kept and is not the live one.
marker_previous() {
    local previous
    previous="$(sed -n 's/^previous=//p' "$MARKER" 2> /dev/null || true)"
    if valid_release_id "$previous" && [ -d "$REL/$previous" ] && [ "$previous" != "$(live_id)" ]; then
        printf '%s\n' "$previous"
    fi
}

live_tag() {
    local id
    id="$(live_id)"
    [ -z "$id" ] || env_value "$REL/$id/release.env" RELEASE_TAG
}

# version_above <a> <b>: a is the higher release version; anything is above an empty b.
version_above() {
    local -a a b
    [ -n "$2" ] || return 0
    IFS=. read -r -a a <<< "${1#v}"
    IFS=. read -r -a b <<< "${2#v}"
    if [ "${a[0]}" -ne "${b[0]}" ]; then
        [ "${a[0]}" -gt "${b[0]}" ]
    elif [ "${a[1]}" -ne "${b[1]}" ]; then
        [ "${a[1]}" -gt "${b[1]}" ]
    else
        [ "${a[2]}" -gt "${b[2]}" ]
    fi
}

# tried_add <tag> <signed> <outcome> <reason>: outcome is started, released, refused, failed or
# rolled-back. The reason is folded onto its line, since one line is one decision.
tried_add() {
    local reason=${4//[$'\t\n']/ }
    mkdir -p "$DM3_STATE"
    printf '%s\t%s\t%s\t%s\t%s\n' "$(date +%s)" "$1" "${2:0:12}" "$3" "$reason" >> "$TRIED"
}

max_tried_version() {
    local version max=""
    [ -f "$TRIED" ] || return 0
    while IFS=$'\t' read -r _ version _; do
        valid_tag_name "$version" || continue
        if version_above "$version" "$max"; then
            max=$version
        fi
    done < "$TRIED"
    printf '%s\n' "$max"
}

is_rejected() {
    grep -qxF -- "$1" "$REJECTED" 2> /dev/null
}

remember_rejected() {
    mkdir -p "$DM3_STATE"
    is_rejected "$1" || printf '%s\n' "$1" >> "$REJECTED"
}

# wait_for <tag> <reason>: the candidate is not taken now and nothing is listed. The time kept is
# the first time anything waited, which is what ReleaseWaiting counts the hour from.
wait_for() {
    say "$1 waits: $2"
    [ -s "$WAITING" ] || printf '%s\t%s\t%s\n' "$(date +%s)" "$1" "${2//[$'\t\n']/ }" > "$WAITING"
}

# candidates <ls-remote listing> <live version>: what a tick may look at, highest first, as
# kind<TAB>name<TAB>id lines. A strict name above the live version and every tried one, whose id
# was not rejected before: "try" when annotated, "lightweight" when not, since only an annotated
# tag carries a signature. A release-shaped name that is not strict is "misnamed", quoted, so the
# journal names it without running it.
candidates() {
    local listing=$1 live=$2 floor id ref name major minor patch
    local -A tag=() peeled=()
    floor="$(max_tried_version)"
    while read -r id ref; do
        [[ $ref == refs/tags/* ]] || continue
        name=${ref#refs/tags/}
        if [[ $name == *'^{}' ]]; then
            peeled[${name%'^{}'}]=$id
        else
            tag[$name]=$id
        fi
    done <<< "$listing"
    for name in "${!tag[@]}"; do
        if ! valid_tag_name "$name"; then
            if [[ $name =~ ^v[0-9] ]]; then
                printf '%s\tmisnamed\t%q\n' 000000000000000000 "$name"
            fi
            continue
        fi
        version_above "$name" "$live" || continue
        version_above "$name" "$floor" || continue
        id=${tag[$name]}
        ! is_rejected "$id" || continue
        IFS=. read -r major minor patch <<< "${name#v}"
        if [[ -n ${peeled[$name]:-} && ${peeled[$name]} != "$id" ]]; then
            printf '%06d%06d%06d\ttry\t%s\t%s\n' "$major" "$minor" "$patch" "$name" "$id"
        else
            printf '%06d%06d%06d\tlightweight\t%s\t%s\n' "$major" "$minor" "$patch" "$name" "$id"
        fi
    done | sort -r | cut -f2- | awk -F '\t' -v max="$MAX_CANDIDATES" '$1 != "try" || ++tries <= max'
}

# The build time of the oldest image the live release runs, as epoch seconds.
oldest_build() {
    local env=$1 ref created epoch oldest=""
    while IFS= read -r ref; do
        created="$(docker image inspect --format '{{.Created}}' "$ref" 2> /dev/null || true)"
        [ -n "$created" ] || continue
        epoch="$(date -d "$created" +%s 2> /dev/null || true)"
        [ -n "$epoch" ] || continue
        if [ -z "$oldest" ] || [ "$epoch" -lt "$oldest" ]; then
            oldest=$epoch
        fi
    done < <(sed -n 's/^DM_IMAGE_[A-Z_]*=//p' "$env")
    printf '%s\n' "$oldest"
}

# write_metric <rejected this tick>: the one family of 3.15, from what is true now, so a run that
# died cannot leave it stale. Renamed into place, as the collector may read at any moment.
write_metric() {
    local rejected=${1:-0} id tag built tmp
    mkdir -p "$TEXTFILE_DIR"
    tmp="$(mktemp "$TEXTFILE_DIR/.dm_release.XXXXXX")"
    {
        echo "# HELP dm_release What the release script last found; field names the fact (release design 3.15)."
        echo "# TYPE dm_release gauge"
        id="$(live_id)"
        if [ -n "$id" ]; then
            tag="$(env_value "$REL/$id/release.env" RELEASE_TAG)"
            # The link itself, not its target: renaming current is the change.
            printf 'dm_release{field="changed",tag="%s"} %s\n' "$tag" "$(stat -c %Y "$REL/current")"
            built="$(oldest_build "$REL/$id/release.env")"
            if [ -n "$built" ]; then
                printf 'dm_release{field="built"} %s\n' "$built"
            fi
        fi
        if [ -f "$TRIED" ]; then
            awk -F '\t' '$4 == "failed" || $4 == "refused" { time = $1; tag = $2 }
                END { if (time) printf "dm_release{field=\"failed\",tag=\"%s\"} %s\n", tag, time }' "$TRIED"
        fi
        printf 'dm_release{field="waiting_since"} %s\n' "$(cut -f1 "$WAITING" 2> /dev/null || echo 0)"
        printf 'dm_release{field="fetched"} %s\n' "$(cat "$FETCHED" 2> /dev/null || echo 0)"
        printf 'dm_release{field="rejected"} %s\n' "$rejected"
    } > "$tmp"
    chmod 644 "$tmp"
    mv -f "$tmp" "$TEXTFILE_DIR/dm_release.prom"
}

# Application images of releases older than the live one and the one it replaced. docker refuses
# to remove an image a container runs, and that is left as it is.
prune_app_images() {
    local id prev keep="" prefix repo ref
    id="$(live_id)"
    [ -n "$id" ] || return 0
    prev="$(env_value "$REL/$id/release.env" RELEASE_REPLACES)"
    for id in "$id" "$prev"; do
        if [ -n "$id" ] && [ -f "$REL/$id/release.env" ]; then
            keep+="$(sed -n 's/^DM_IMAGE_[A-Z_]*=//p' "$REL/$id/release.env")"$'\n'
        fi
    done
    prefix="$(conf_value image_prefix)" || return 0
    for repo in dm-api dm-front dm-worker-mail dm-worker-notification; do
        while IFS= read -r ref; do
            if [ -z "$ref" ] || grep -qxF -- "$ref" <<< "$keep"; then
                continue
            fi
            docker image rm "$ref" > /dev/null 2>&1 || true
        done < <(docker image ls --digests --format '{{.Repository}}@{{.Digest}}' "$prefix$repo" | grep -v '@<none>$' || true)
    done
}

# memory_fits: what a rollout adds still leaves a tenth of the memory, the line of HighMemoryUsage
# (4.3). The headroom is the operator's, in shared/env.
memory_fits() {
    local total available headroom
    headroom="$(env_value "$SHARED/env" DM_RELEASE_HEADROOM_MIB)"
    headroom=${headroom:-512}
    [[ $headroom =~ ^[0-9]{1,7}$ ]] || { refuse "DM_RELEASE_HEADROOM_MIB in $SHARED/env is not a number of MiB"; return; }
    total="$(awk '$1 == "MemTotal:" { print $2 }' "$MEMINFO")"
    available="$(awk '$1 == "MemAvailable:" { print $2 }' "$MEMINFO")"
    [[ $total =~ ^[0-9]+$ && $available =~ ^[0-9]+$ ]] || { refuse "$MEMINFO gives no MemTotal or MemAvailable"; return; }
    [ "$(((available - headroom * 1024) * 10))" -ge "$total" ]
}

descends() {
    release_git -C "$REPO" merge-base --is-ancestor "$1" "$2" 2> /dev/null
}

# A rollback keeps the schema the newer release migrated to, so it passes over migrations only the
# live release has and refuses one that both have in different forms.
refuse_schema_rollback() {
    local live=$1 target=$2 name=$3 differing
    differing="$({ sed 's/^/live\t/' "$live"; sed 's/^/target\t/' "$target"; } | awk -F '\t' '
        $3 ~ /ModelSnapshot\.cs$/ { next }
        $1 == "live" { live[$3] = $2; next }
        ($3 in live) && live[$3] != $2 { print $3 }')"
    [ -z "$differing" ] ||
        { refuse "rolling back to $name: an existing migration differs ($(tr '\n' ' ' <<< "$differing"))"; return; }
}

# Migrations the candidate adds, which regime migrate runs before the twins start.
added_migrations() {
    awk -F '\t' '$1 == "live" { live[$3] = 1; next } !($3 in live) && $3 ~ /\/Migrations\// && $3 !~ /ModelSnapshot\.cs$/ { print $3 }' \
        <(sed 's/^/live\t/' "$1") <(sed 's/^/candidate\t/' "$2")
}

# The configuration as the server runs it: the rollout profile and no other. The seeder (tools)
# never starts on a server, and it takes the application's settings, so counting it would
# refuse every change of the settings list.
resolved_config() {
    compose_cmd "$REL/$1" --profile rollout config --format json
}

# classify_configs <live.json> <candidate.json>: one line per difference outside the application
# services. jq compares objects whatever the order of their keys.
classify_configs() {
    jq -n -r --argjson app "$APP_SERVICES" --slurpfile live "$1" --slurpfile candidate "$2" '
        def outside: (.services // {}) | with_entries(select(.key as $k | $app | index($k) | not));
        ($live[0] | outside) as $l | ($candidate[0] | outside) as $c
        | ([$l, $c] | map(keys) | add | unique | .[] | select($l[.] != $c[.]) | "the service \(.)"),
          ([$live[0], $candidate[0]] | map(del(.services) | keys) | add | unique | .[]
           | select($live[0][.] != $candidate[0][.]) | "the top-level \(.)")'
}

# classify_files <live dir> <candidate dir> <project dir/> <config.json>...: the mounted files and
# the release machinery, which a container or root's cron reads by path.
classify_files() {
    local live=$1 candidate=$2 base=$3 relative unit
    shift 3
    while IFS= read -r relative; do
        [ -n "$relative" ] || continue
        if [ -e "$live/docker/$relative" ] || [ -e "$candidate/docker/$relative" ]; then
            diff -r -q "$live/docker/$relative" "$candidate/docker/$relative" > /dev/null 2>&1 ||
                printf 'the mounted file docker/%s\n' "$relative"
        fi
    done < <(jq -r --arg base "$base" '.services[] | (.volumes // [])[]
        | select(.type == "bind" and (.source | startswith($base))) | .source | ltrimstr($base)' "$@" | sort -u)
    diff -r -q "$live/docker/scripts" "$candidate/docker/scripts" > /dev/null 2>&1 ||
        echo "the release machinery under docker/scripts"
    for unit in dm3.service dm3-release.service dm3-release.timer; do
        cmp -s "$live/docker/$unit" "$candidate/docker/$unit" 2> /dev/null || echo "the unit docker/$unit"
    done
}

# classify <live id> <candidate id> <timer|deploy|rollback> (3.8): succeeds when the candidate
# differs from the live release only in the application; otherwise prints the one-line reason.
classify() {
    local live=$1 candidate=$2 mode=$3 work differences
    work="$(mktemp -d)"
    if ! resolved_config "$live" > "$work/live.json" || ! resolved_config "$candidate" > "$work/candidate.json"; then
        rm -rf "$work"
        echo "the configuration could not be resolved with $SHARED/env; compose names what is missing"
        return 1
    fi
    differences="$(classify_configs "$work/live.json" "$work/candidate.json"
        classify_files "$REL/$live" "$REL/$candidate" "$REL/current/docker/" "$work/live.json" "$work/candidate.json")"
    rm -rf "$work"
    if [ -n "$differences" ]; then
        printf 'needs dm3-release maintain: %s\n' "$(paste -sd ',' <<< "$differences" | sed 's/,/, /g')"
        return 1
    fi
    if [ "$mode" = rollback ]; then
        refuse_schema_rollback "$REL/$live/schema.manifest" "$REL/$candidate/schema.manifest" \
            "$(env_value "$REL/$candidate/release.env" RELEASE_TAG)" 2>&1 | sed 's/^dm3-release: refused: //'
        return "${PIPESTATUS[0]}"
    fi
    refuse_schema_change "$(schema_regime)" 0 "$REL/$live/schema.manifest" "$REL/$candidate/schema.manifest" \
        "$(env_value "$REL/$candidate/release.env" RELEASE_TAG)" 2>&1 | sed 's/^dm3-release: refused: //'
    return "${PIPESTATUS[0]}"
}

health_of() {
    docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{end}}' "$1" 2> /dev/null || true
}

# wait_healthy <container> <seconds>
wait_healthy() {
    local deadline=$((SECONDS + $2))
    until [ "$(health_of "$1")" = healthy ]; do
        [ "$SECONDS" -lt "$deadline" ] || { refuse "$1 is not healthy within $2 s"; return; }
        sleep 1
    done
}

image_matches() {
    local running wanted
    running="$(docker inspect --format '{{.Image}}' "$1" 2> /dev/null || true)"
    wanted="$(docker image inspect --format '{{.Id}}' "$2" 2> /dev/null || true)"
    [ -n "$running" ] && [ "$running" = "$wanted" ]
}

# The stand a rollout may start from (3.5 step 5): the edge and both primaries healthy on the live
# images, no twin, the links on the primaries. Says what is wrong.
stand_healthy() {
    local env="$REL/$1/release.env" container
    for container in dm-nginx dm-api dm-front; do
        [ "$(health_of "$container")" = healthy ] || { say "$container is not healthy"; return 1; }
    done
    image_matches dm-api "$(env_value "$env" DM_IMAGE_API)" || { say "dm-api does not run the live image"; return 1; }
    image_matches dm-front "$(env_value "$env" DM_IMAGE_FRONT)" || { say "dm-front does not run the live image"; return 1; }
    [ -z "$(docker ps -a -q --filter name='^dm-(api|front)-next$')" ] || { say "a twin is there"; return 1; }
    if [ "$(edge_exec readlink /etc/nginx/upstreams-live/api.conf 2> /dev/null || true)" != /etc/nginx/upstreams/api-primary.conf ] ||
        [ "$(edge_exec readlink /etc/nginx/upstreams-live/frontend.conf 2> /dev/null || true)" != /etc/nginx/upstreams/front-primary.conf ]; then
        say "the edge is not on the primaries"
        return 1
    fi
}

# The start gate (3.6) of one pair, 90 s at most: docker health; three readiness answers of 200
# in a row and one board list asked inside the API; the document and one asset it names asked of
# the frontend from the edge; each image the one release.env names.
start_gate() {
    local dir=$1 pair=$2 api front service deadline container ok=0 code document asset
    case "$pair" in
        primary) api=dm-api front=dm-front service=dmfront ;;
        next) api=dm-api-next front=dm-front-next service=dmfront-next ;;
    esac
    deadline=$((SECONDS + 90))
    for container in "$api" "$front"; do
        until [ "$(health_of "$container")" = healthy ]; do
            [ "$SECONDS" -lt "$deadline" ] || { refuse "$container is not healthy within 90 s"; return; }
            sleep 2
        done
    done
    while [ "$ok" -lt 3 ]; do
        code="$(docker exec "$api" curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/_ready 2> /dev/null || true)"
        if [ "$code" = 200 ]; then ok=$((ok + 1)); else ok=0; fi
        [ "$ok" -eq 3 ] || [ "$SECONDS" -lt "$deadline" ] || { refuse "$api is not ready three times in a row within 90 s"; return; }
        [ "$ok" -eq 3 ] || sleep 1
    done
    code="$(docker exec "$api" curl -s -o /dev/null -w '%{http_code}' http://localhost:5000/v1/boards 2> /dev/null || true)"
    [ "$code" = 200 ] || { refuse "$api answered $code to GET /v1/boards"; return; }
    document="$(edge_exec curl -fsS "http://$service/" 2> /dev/null)" || { refuse "$front does not serve the document"; return; }
    asset="$(grep -o '/assets/[A-Za-z0-9._-]*' <<< "$document" | head -n 1 || true)"
    [ -n "$asset" ] || { refuse "the document of $front names no asset"; return; }
    edge_exec curl -fsS -o /dev/null "http://$service$asset" 2> /dev/null || { refuse "$front does not serve $asset"; return; }
    image_matches "$api" "$(env_value "$dir/release.env" DM_IMAGE_API)" || { refuse "$api does not run the image the release names"; return; }
    image_matches "$front" "$(env_value "$dir/release.env" DM_IMAGE_FRONT)" || { refuse "$front does not run the image the release names"; return; }
}

remove_twins() {
    compose_cmd "$REL/current" --profile rollout rm -s -f dmapi-next dmfront-next > /dev/null 2>&1 ||
        { refuse "the twins could not be removed"; return; }
}

# 3.12: the outgoing frontend's files are touched first, so the live bundle is never older than
# the cut, and only then is what no release touched for 30 days deleted.
retire_assets() {
    # shellcheck disable=SC2016 # expanded by the shell in the frontend container
    docker exec dm-front sh -c 'for file in /usr/share/nginx/html/assets/*; do
        touch -c "/var/lib/dm-front/assets/${file##*/}"; done
        find /var/lib/dm-front/assets -type f -mtime +30 -delete'
}

# The schema step (3.9) from the candidate's definition and image. The gate is the newest
# migration being in the history table, not the exit code.
schema_step() {
    local dir=$1 newest found
    say "regime migrate with added migrations: the nightly backup, then the schema step"
    bash "$REL/current/docker/scripts/backup-postgres.sh" || { refuse "the backup before the schema step failed"; return; }
    compose_cmd "$dir" up --no-deps --no-build --pull never --force-recreate --exit-code-from migration migration ||
        { refuse "the migration service failed"; return; }
    newest="$(awk -F '\t' '$2 ~ /\/Migrations\/[^\/]+\.cs$/ && $2 !~ /(Designer|ModelSnapshot)\.cs$/ {
        sub(/.*\//, "", $2); sub(/\.cs$/, "", $2); print $2 }' "$dir/schema.manifest" | sort | tail -n 1)"
    [[ $newest =~ ^[A-Za-z0-9_]+$ ]] || { refuse "the manifest names no migration"; return; }
    found="$(docker exec dm-pg psql -U postgres -d dm3 -tAc \
        "SELECT 1 FROM \"__EFMigrationsHistory\" WHERE \"MigrationId\" = '$newest'" 2> /dev/null || true)"
    [ "$found" = 1 ] || { refuse "$newest is not in __EFMigrationsHistory after the schema step"; return; }
}

# From step 4 on the old primaries are gone: recovery brings back what current names.
recover_after() {
    say "$1; recovering the release current names"
    if ! recover_stack; then
        tried_add "$(live_tag)" "" failed "recovery after a failed rollout failed"
    fi
    return 1
}

# rollout <live id> <candidate id> <forward|back> (3.6): from a healthy stand on the live release to
# the candidate, the candidate's definitions throughout. current moves only at the end, so a
# failure leaves recovery the definitions it needs. The fixed "switched to" lines are what the
# proof kills a run on.
rollout() {
    local live=$1 candidate=$2 how=$3 dir="$REL/$2" added
    local up=(up -d --no-deps --no-build --pull never)
    if [ "$how" = forward ] && [ "$(schema_regime)" = migrate ]; then
        added="$(added_migrations "$REL/$live/schema.manifest" "$dir/schema.manifest")"
        if [ -n "$added" ]; then
            schema_step "$dir" || { remove_twins; rm -f "$MARKER"; return 1; }
        fi
    fi

    retire_assets || say "the assets of the outgoing frontend could not be retired; the next release tries again"
    if ! compose_cmd "$dir" --profile rollout "${up[@]}" dmapi-next dmfront-next || ! start_gate "$dir" next; then
        refuse "the twins did not pass the start gate"
        remove_twins || true
        rm -f "$MARKER"
        return 1
    fi

    say "switching to next"
    if ! edge_switch next || ! { say "switched to next" && switch_check next primary 30; }; then
        if edge_switch primary && switch_check primary next; then
            refuse "the switch check towards the twins failed; the primaries serve again"
            remove_twins || true
            rm -f "$MARKER"
            return 1
        fi
        recover_after "the edge could not be put back on the primaries"
        return 1
    fi

    compose_cmd "$dir" stop dmapi dmfront || { recover_after "the primaries could not be stopped"; return 1; }
    compose_cmd "$dir" up -d --no-deps --no-build --pull never --force-recreate dmapi dmfront ||
        { recover_after "the primaries could not be recreated"; return 1; }
    start_gate "$dir" primary || { recover_after "the new primaries did not pass the start gate"; return 1; }
    say "switching to primary"
    { edge_switch primary && say "switched to primary" && switch_check primary next; } ||
        { recover_after "the switch check towards the new primaries failed"; return 1; }
    remove_twins || { recover_after "the twins could not be removed"; return 1; }
    compose_cmd "$dir" up -d --no-deps --no-build --pull never --force-recreate --wait dm-mail-worker dm-notification-worker ||
        { recover_after "the workers did not become healthy"; return 1; }
    commit_current "$candidate"
    rm -f "$MARKER"
}

refuse_release() {
    refuse "$1: $2"
    tried_add "$1" "$F_SIGNED" refused "$2"
    rm -f "$WAITING"
}

# Steps 4 to 9 of a tick (3.5) for one verified tag, whose fields are in F_. Returns 0 when it is
# live, WAITS when it waits, 1 when it was refused or failed.
try_candidate() {
    local name=$1 mode=$2 live live_commit rid reason status=0
    live="$(live_id)"
    [ -n "$live" ] || { refuse "no release is live: install one with dm3-release maintain"; return 1; }
    mirror_fetch "$name" "$F_ID" || { wait_for "$name" "the mirror could not fetch it"; return "$WAITS"; }
    live_commit="$(env_value "$REL/$live/release.env" RELEASE_COMMIT)"
    descends "$live_commit" "$F_COMMIT" ||
        { refuse_release "$name" "commit $F_COMMIT does not descend from the live commit $live_commit"; return 1; }
    stand_healthy "$live" || { wait_for "$name" "the stand is not healthy"; return "$WAITS"; }
    # Before the pull: the next tick prunes the images of a release that is not live, so a
    # candidate waiting after its pull would fetch its four images again on every tick.
    memory_fits || { wait_for "$name" "the memory would leave less than a tenth free"; return "$WAITS"; }
    pull_images "$F_COMMIT" "$F_API" "$F_FRONT" "$F_MAIL" "$F_NOTIFICATION" || status=$?
    case "$status" in
        0) ;;
        2) wait_for "$name" "an image could not be pulled"; return "$WAITS" ;;
        *) refuse_release "$name" "an image is not labelled with commit $F_COMMIT"; return 1 ;;
    esac
    rid="$(release_id "$name" "$F_COMMIT" "$F_SIGNED")"
    build_release_dir "$rid" "$name" "$F_COMMIT" "$live" "$F_API" "$F_FRONT" "$F_MAIL" "$F_NOTIFICATION" > /dev/null ||
        { refuse_release "$name" "the release directory could not be built"; return 1; }
    reason="$(classify "$live" "$rid" "$mode")" || { refuse_release "$name" "$reason"; return 1; }

    rm -f "$WAITING"
    write_marker "$name" "$F_SIGNED"
    tried_add "$name" "$F_SIGNED" started "the rollout began"
    if ! rollout "$live" "$rid" forward; then
        tried_add "$name" "$F_SIGNED" failed "the rollout failed; the journal names the step"
        return 1
    fi
    tried_add "$name" "$F_SIGNED" released "live as $rid"
    say "$name is live as $rid"
}

# A run that died left its marker: recovery, and its tag is not taken again.
recover_died() {
    local tag signed previous
    tag="$(sed -n 's/^tag=//p' "$MARKER")"
    signed="$(sed -n 's/^signed=//p' "$MARKER")"
    previous="$(marker_previous)"
    valid_tag_name "$tag" || tag="$(live_tag)"
    say "a run of $tag did not finish; recovering the release current names"
    if recover_stack "$previous"; then
        tried_add "$tag" "$signed" failed "the run did not finish"
    else
        tried_add "$tag" "$signed" failed "recovery after a run that did not finish failed"
        return 1
    fi
}

# One tick of the timer (3.5).
verb_poll() {
    local origin listing line kind name id fields rejected=0 tried=0 status
    local -a lines=()
    if [ -e "$MARKER" ]; then
        recover_died || true
        write_metric 0
        return 0
    fi
    [ -n "$(live_id)" ] || { say "no release is live; dm3-release maintain installs one"; return 0; }
    prune_app_images
    origin="$(conf_value origin)" || return
    if ! listing="$(release_git_timed -C "$DM3_STATE" ls-remote --tags -- "$origin" 2> /dev/null)"; then
        say "the origin $origin cannot be listed"
        write_metric 0
        return 0
    fi
    date +%s > "$FETCHED"
    # Read whole first: the docker commands of a rollout would otherwise read the rest of the list.
    mapfile -t lines < <(candidates "$listing" "$(live_tag)")
    for line in "${lines[@]}"; do
        IFS=$'\t' read -r kind name id <<< "$line"
        case "$kind" in
            misnamed)
                say "passed over the tag $name: not vMAJOR.MINOR.PATCH with numbers of at most six digits"
                ;;
            lightweight)
                say "rejected $name: a lightweight tag carries no signature"
                remember_rejected "$id"
                rejected=$((rejected + 1))
                ;;
            try)
                status=0
                fields="$(fetch_and_verify "$name")" || status=$?
                if [ "$status" = "$WAITS" ]; then
                    wait_for "$name" "it could not be listed or fetched"
                    tried=1
                    break
                elif [ "$status" != 0 ]; then
                    remember_rejected "$id"
                    rejected=$((rejected + 1))
                    continue
                fi
                take_fields "$fields"
                say "$name verified: commit $F_COMMIT"
                tried=1
                try_candidate "$name" timer || true
                break
                ;;
        esac
    done
    [ "$tried" = 1 ] || rm -f "$WAITING"
    write_metric "$rejected"
}

# deploy <tag>: a tick for one named tag, the tried list ignored; the operator's retry.
verb_deploy() {
    local name=$1 live status=0 fields
    live="$(live_tag)"
    [ -n "$live" ] || { refuse "no release is live: install one with dm3-release maintain"; return; }
    version_above "$name" "$live" ||
        { refuse "$name is not above the live version $live; dm3-release rollback goes back"; return; }
    fields="$(fetch_and_verify "$name")" || return
    take_fields "$fields"
    say "$name verified: commit $F_COMMIT"
    try_candidate "$name" deploy || status=$?
    write_metric 0
    return "$status"
}

# rollback (3.7): the rollout towards the release the live one replaced; the one left is listed as
# tried, so the timer does not bring it back.
verb_rollback() {
    local live target left reason target_tag
    local -a images=()
    live="$(live_id)"
    [ -n "$live" ] || { refuse "no release is live"; return; }
    target="$(env_value "$REL/$live/release.env" RELEASE_REPLACES)"
    if [ -z "$target" ] || [ ! -d "$REL/$target" ]; then
        refuse "$live replaced no release that is kept"
        return
    fi
    stand_healthy "$live" || { refuse "the stand is not healthy: dm3-release recover first"; return; }
    reason="$(classify "$live" "$target" rollback)" || { refuse "$reason"; return; }
    mapfile -t images < <(sed -n 's/^DM_IMAGE_[A-Z_]*=//p' "$REL/$target/release.env")
    pull_images "$(env_value "$REL/$target/release.env" RELEASE_COMMIT)" "${images[@]}" || return
    left="$(live_tag)"
    target_tag="$(env_value "$REL/$target/release.env" RELEASE_TAG)"
    write_marker "$target_tag" "${target##*-}"
    if ! rollout "$live" "$target" back; then
        tried_add "$target_tag" "${target##*-}" failed "the rollback from $left failed; the journal names the step"
        write_metric 0
        return 1
    fi
    tried_add "$left" "${live##*-}" rolled-back "rolled back to $target_tag"
    write_metric 0
    say "rolled back from $left to $target_tag ($target)"
}

# Maintenance (3.7): any verified tag the operator names, downtime allowed. Also the first
# installation, on an empty layout.
verb_maintain() {
    local name=$1 reset=$2 fields prev rid replaced
    fields="$(fetch_and_verify "$name")" || return
    take_fields "$fields"
    say "$name verified: commit $F_COMMIT"

    mirror_fetch "$name" "$F_ID" || return
    prev="$(live_id)"
    # A rerun after a maintain that died past the move of current: the containers still read the
    # files of the release that one replaced.
    replaced="$(marker_previous)"
    [ -n "$replaced" ] || replaced=$prev
    manifest_of "$F_COMMIT" > "$DM3_STATE/candidate.manifest" ||
        { refuse "the schema sources of $F_COMMIT could not be listed"; return; }
    local live_manifest="$DM3_STATE/none.manifest"
    [ -z "$prev" ] || live_manifest="$REL/$prev/schema.manifest"
    refuse_schema_change "$(schema_regime)" "$reset" "$live_manifest" "$DM3_STATE/candidate.manifest" "$name" ||
        return
    pull_images "$F_COMMIT" "$F_API" "$F_FRONT" "$F_MAIL" "$F_NOTIFICATION" || return

    rid="$(release_id "$name" "$F_COMMIT" "$F_SIGNED")"
    build_release_dir "$rid" "$name" "$F_COMMIT" "$prev" "$F_API" "$F_FRONT" "$F_MAIL" "$F_NOTIFICATION" > /dev/null ||
        return

    # Before current moves: a registry that does not answer then leaves the live release whole.
    if [ -n "$prev" ]; then
        pull_missing_images "$REL/$rid" || return
    fi

    # From here on the stand changes; a run that dies leaves the marker, and boot or recover
    # brings back whatever current names.
    write_marker "$name" "$F_SIGNED" "$replaced"
    if [ "$reset" = 1 ]; then
        reset_database || return
    fi
    [ "$prev" = "$rid" ] || commit_current "$rid"
    if [ -z "$prev" ]; then
        pull_missing_images || return
    fi
    install_machinery || return
    if [ "$replaced" = "$rid" ]; then
        recover_stack || return
    else
        recover_stack "$replaced" || return
    fi
    prune_release_dirs
    write_metric 0
    say "$name is live as $rid"
}

verb_recover() {
    local status=0
    [ -n "$(live_id)" ] || { refuse "no release is live: $REL/current does not exist"; return; }
    if [ -e "$MARKER" ]; then
        recover_died || status=$?
    elif ! recover_stack; then
        tried_add "$(live_tag)" "" failed "recovery failed"
        status=1
    fi
    write_metric 0
    return "$status"
}

# A run the machine went down under is a failure with its letter, as when the timer finds it.
verb_boot() {
    local status=0
    [ -n "$(live_id)" ] || { refuse "no release is live: $REL/current does not exist"; return; }
    if [ ! -e "$MARKER" ]; then
        recover_stack
        return
    fi
    recover_died || status=$?
    write_metric 0
    return "$status"
}

verb_stop() {
    [ -n "$(live_id)" ] || return 0
    compose_cmd "$REL/current" --profile rollout down
}

# What is live and how its containers are, read only.
verb_status() {
    local id env key since tag reason
    id="$(live_id)"
    if [ -z "$id" ]; then
        echo "no release is live: $REL/current does not exist"
        return 0
    fi
    env="$REL/$id/release.env"
    printf 'release: %s\n' "$id"
    printf 'tag: %s\n' "$(env_value "$env" RELEASE_TAG)"
    printf 'commit: %s\n' "$(env_value "$env" RELEASE_COMMIT)"
    for key in api:DM_IMAGE_API front:DM_IMAGE_FRONT worker-mail:DM_IMAGE_WORKER_MAIL \
        worker-notification:DM_IMAGE_WORKER_NOTIFICATION; do
        printf '%s: %s\n' "${key%%:*}" "$(env_value "$env" "${key#*:}")"
    done
    if [ -e "$MARKER" ]; then
        echo "in-flight: yes, a run did not finish; dm3-release recover or the next boot brings back $id"
    else
        echo "in-flight: no"
    fi
    if [ -s "$WAITING" ]; then
        IFS=$'\t' read -r since tag reason < "$WAITING"
        printf 'waiting: %s since %s: %s\n' "$tag" "$(date -d "@$since" '+%F %T' 2> /dev/null || echo "$since")" "$reason"
    else
        echo "waiting: nothing"
    fi
    if [ -s "$TRIED" ]; then
        echo "tried, newest last:"
        tail -n 10 "$TRIED" | awk -F '\t' '{ printf "  %s %s %s: %s\n", $2, $4, $3, $5 }'
    fi
    compose_cmd "$REL/current" --profile rollout ps --all --format '{{.Service}}\t{{.State}}'
}

require_root() {
    [ "$(id -u)" -eq 0 ] || { refuse "dm3-release runs as root"; return; }
}

usage() {
    echo "Usage: dm3-release poll | deploy <tag> | rollback | maintain <tag> [--reset-database] | recover | boot | stop | status" >&2
    return 2
}

main() {
    local verb="${1:-}"
    [ "$#" -eq 0 ] || shift
    case "$verb" in
        poll)
            [ "$#" -eq 0 ] || { usage; return; }
            require_root || return
            if [ "${DM3_IN_UNIT:-}" != 1 ]; then
                run_in_unit poll
                return
            fi
            # A tick that finds a run in progress has nothing to do; the next one comes in minutes.
            if ! take_lock nowait 2> /dev/null; then
                say "another dm3-release run holds the lock; this tick does nothing"
                return 0
            fi
            verb_poll
            ;;
        deploy)
            [ "$#" -eq 1 ] || { usage; return; }
            valid_tag_name "$1" || { refuse_bad_name "$1"; return; }
            require_root || return
            if [ "${DM3_IN_UNIT:-}" != 1 ]; then
                run_in_unit deploy "$1"
                return
            fi
            take_lock nowait || return
            verb_deploy "$1"
            ;;
        rollback)
            [ "$#" -eq 0 ] || { usage; return; }
            require_root || return
            if [ "${DM3_IN_UNIT:-}" != 1 ]; then
                run_in_unit rollback
                return
            fi
            take_lock nowait || return
            verb_rollback
            ;;
        maintain)
            local name="" reset=0 argument
            for argument in "$@"; do
                case "$argument" in
                    --reset-database) reset=1 ;;
                    -*) usage; return ;;
                    *) [ -z "$name" ] || { usage; return; }; name=$argument ;;
                esac
            done
            valid_tag_name "$name" || { refuse_bad_name "$name"; return; }
            require_root || return
            if [ "${DM3_IN_UNIT:-}" != 1 ]; then
                run_in_unit maintain "$@"
                return
            fi
            take_lock nowait || return
            verb_maintain "$name" "$reset"
            ;;
        recover)
            [ "$#" -eq 0 ] || { usage; return; }
            require_root || return
            if [ "${DM3_IN_UNIT:-}" != 1 ]; then
                run_in_unit recover
                return
            fi
            take_lock nowait || return
            verb_recover
            ;;
        boot)
            [ "$#" -eq 0 ] || { usage; return; }
            require_root || return
            take_lock wait || return
            verb_boot
            ;;
        stop)
            [ "$#" -eq 0 ] || { usage; return; }
            require_root || return
            take_lock wait || return
            verb_stop
            ;;
        status)
            [ "$#" -eq 0 ] || { usage; return; }
            verb_status
            ;;
        *)
            usage
            ;;
    esac
}

# Run, not sourced: check-release-entry.sh sources this file for its functions.
if [ "${BASH_SOURCE[0]}" = "$0" ]; then
    main "$@"
fi
