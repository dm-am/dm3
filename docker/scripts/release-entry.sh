#!/bin/bash
# dm3-release, installed as /usr/local/sbin/dm3-release from a release directory. It is the
# one thing on a server that changes which release runs, so everything it accepts comes from a
# tag the pinned key signed, and everything it runs goes through one compose command line.
# The layout and the reasons are in the release design (3.2 to 3.7); this file holds the
# mechanism.
#
# Usage, as root:
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
RELEASE_USER=dm-release

# What an unverified tag may cost: its fetch stops after two minutes or at a file of 256 MiB
# (ulimit counts in 1024-byte blocks), whatever a writer pushes under a release-shaped name.
FETCH_TIMEOUT=120
FETCH_FILE_BLOCKS=262144

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
    listing="$(release_git -C "$DM3_STATE" ls-remote --tags "$origin" 2> /dev/null)" ||
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
    # shellcheck disable=SC2016 # expanded by the shell bash -c starts
    if ! release_git init -q --bare "$dir" ||
        ! as_release_user timeout "$FETCH_TIMEOUT" bash -c 'ulimit -f "$1" && shift && exec "$@"' _ \
            "$FETCH_FILE_BLOCKS" git -C "$dir" fetch -q --no-tags --depth=1 "$origin" \
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
# and verified there. Prints the fields and id=<tag object id>.
fetch_and_verify() {
    local name=$1 id dir fields status=0
    id="$(remote_tag_id "$name")" || return
    dir="$(scratch_fetch "$name" "$id")" || return
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
    release_git -C "$REPO" fetch -q --no-tags "$origin" "+refs/tags/$name:refs/tags/$name" ||
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
# puts them. No profile: watchtower, behind production, never starts on a layout.
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
        docker pull -q "$ref" > /dev/null || { refuse "$ref could not be pulled"; return; }
        label="$(docker image inspect --format '{{index .Config.Labels "org.opencontainers.image.revision"}}' "$ref")"
        [ "$label" = "$commit" ] ||
            { refuse "$ref was built from ${label:-an unlabelled commit}, not $commit"; return; }
    done
}

# The images outside the application, by the references the compose files give, only when
# absent: that is today's trust, taken at the moment the operator chose.
pull_missing_images() {
    local image
    while IFS= read -r image; do
        [ -n "$image" ] || continue
        docker image inspect "$image" > /dev/null 2>&1 && continue
        docker pull -q "$image" > /dev/null || { refuse "$image could not be pulled"; return; }
    done < <(compose_cmd "$REL/current" config --images | sort -u)
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

# The API's request counter, summed over its series, read from inside the edge.
request_count() {
    edge_exec curl -s "http://$1:5000/metrics" |
        awk '/^http_server_request_duration_seconds_count[{ ]/ { sum += $NF } END { printf "%d\n", sum }'
}

# The switch check (3.6) towards one pair: through the loaded configuration, 20 readiness
# answers of 200 that the pair's API counted, and the frontend that answers is the pair's.
# The counter is read again for up to 3 s, since the metrics endpoint answers from a cache.
switch_check() {
    local pair=$1 api front tries before after codes instance hostname
    case "$pair" in
        primary) api=dmapi front=dm-front ;;
        next) api=dmapi-next front=dm-front-next ;;
    esac
    tries=0
    until [ "$(edge_exec curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8081/_ready 2> /dev/null || true)" = 200 ]; do
        tries=$((tries + 1))
        [ "$tries" -lt 90 ] || { refuse "the $pair pair is not ready through the edge after 180 s"; return; }
        sleep 2
    done
    before="$(request_count "$api")"
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
    instance="$(edge_exec curl -s http://127.0.0.1:8081/_instance)"
    hostname="$(docker inspect --format '{{.Config.Hostname}}' "$front")"
    [ "$instance" = "$hostname" ] ||
        { refuse "the edge serves the frontend $(printf '%q' "$instance"), not $front ($hostname)"; return; }
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

# Maintenance (3.7): any verified tag the operator names, downtime allowed. Also the first
# installation, on an empty layout.
verb_maintain() {
    local name=$1 reset=$2 fields key value commit api front mail notification signed id prev rid
    fields="$(fetch_and_verify "$name")" || return
    while IFS='=' read -r key value; do
        case "$key" in
            commit) commit=$value ;;
            api) api=$value ;;
            front) front=$value ;;
            worker-mail) mail=$value ;;
            worker-notification) notification=$value ;;
            signed) signed=$value ;;
            id) id=$value ;;
        esac
    done <<< "$fields"
    say "$name verified: commit $commit"

    mirror_fetch "$name" "$id" || return
    prev="$(live_id)"
    manifest_of "$commit" > "$DM3_STATE/candidate.manifest" ||
        { refuse "the schema sources of $commit could not be listed"; return; }
    local live_manifest="$DM3_STATE/none.manifest"
    [ -z "$prev" ] || live_manifest="$REL/$prev/schema.manifest"
    refuse_schema_change "$(schema_regime)" "$reset" "$live_manifest" "$DM3_STATE/candidate.manifest" "$name" ||
        return
    pull_images "$commit" "$api" "$front" "$mail" "$notification" || return

    rid="$(release_id "$name" "$commit" "$signed")"
    build_release_dir "$rid" "$name" "$commit" "$prev" "$api" "$front" "$mail" "$notification" > /dev/null ||
        return

    # From here on the stand changes; a run that dies leaves the marker, and boot or recover
    # brings back whatever current names.
    : > "$MARKER"
    if [ "$reset" = 1 ]; then
        reset_database || return
    fi
    [ "$prev" = "$rid" ] || commit_current "$rid"
    pull_missing_images || return
    install_machinery || return
    if [ "$prev" = "$rid" ]; then
        recover_stack || return
    else
        recover_stack "$prev" || return
    fi
    prune_release_dirs
    say "$name is live as $rid"
}

verb_recover() {
    [ -n "$(live_id)" ] || { refuse "no release is live: $REL/current does not exist"; return; }
    recover_stack
}

verb_boot() {
    [ -n "$(live_id)" ] || { refuse "no release is live: $REL/current does not exist"; return; }
    recover_stack
}

verb_stop() {
    [ -n "$(live_id)" ] || return 0
    compose_cmd "$REL/current" --profile rollout down
}

# What is live and how its containers are, read only.
verb_status() {
    local id env key
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
    compose_cmd "$REL/current" --profile rollout ps --all --format '{{.Service}}\t{{.State}}'
}

require_root() {
    [ "$(id -u)" -eq 0 ] || { refuse "dm3-release runs as root"; return; }
}

usage() {
    echo "Usage: dm3-release maintain <tag> [--reset-database] | recover | boot | stop | status" >&2
    return 2
}

main() {
    local verb="${1:-}"
    [ "$#" -eq 0 ] || shift
    case "$verb" in
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
