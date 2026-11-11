#!/usr/bin/env bash
# Fails when the release script on the server stops holding what the server is built on: a
# tag that is not exactly a signed release of the pinned key is refused with its reason, the
# compose command line is the one every verb shares, a release directory has its exact form,
# and status reads the layout without changing it.
#
# Without an argument nothing here needs docker, root or a network: plain git, ssh-keygen and
# a docker stand-in on PATH, against a layout in a temporary directory. The script under test
# is sourced, so its functions are called one by one without the verbs around them.
#
# With --layout it is the other half, for the deployment-smoke job only: root, systemd and
# docker on a throwaway machine. It installs the server layout from a tag it signs with a key
# it generates, then holds runs 0 and 8 of the release design and the ownership of the files.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/common.sh"
ENTRY="$ROOT/docker/scripts/release-entry.sh"

# Linux has what the server has; Git Bash lacks flock, symlinks and a file-size limit, and a
# case that needs one of them says so in one line instead of failing on the tool.
only_where() {
  local tool=$1 what=$2
  if [ "$tool" = ok ]; then
    return 0
  fi
  echo "  skipped here, runs on Linux: $what ($tool)"
  return 1
}

offline() {
  WORK="$(cd "$(mktemp -d)" && pwd)"
  trap 'chmod -R u+w "$WORK" 2> /dev/null; rm -rf "$WORK"' EXIT

  export DM3_ROOT="$WORK/opt" DM3_ETC="$WORK/etc" DM3_STATE="$WORK/state"
  mkdir -p "$DM3_ROOT/rel" "$DM3_ROOT/shared" "$DM3_ETC" "$DM3_STATE/scratch"

  # shellcheck source=docker/scripts/release-entry.sh
  . "$ENTRY"

  local prefix='registry.test/dm-am/' digest
  digest="sha256:$(printf 'a%.0s' {1..64})"
  ORIGIN="$WORK/origin"
  printf 'origin=%s\nimage_prefix=%s\n' "$ORIGIN" "$prefix" > "$DM3_ETC/release.conf"

  # Two commits with what a release directory is exported from: docker/ with the helper that
  # places the machine files, and the schema sources the manifest lists, named as the script
  # names them.
  local migrations=${SCHEMA_SOURCES[0]} initializer=${SCHEMA_SOURCES[1]}
  git init -q "$ORIGIN"
  git -C "$ORIGIN" config user.name smoke
  git -C "$ORIGIN" config user.email smoke@example.invalid
  git -C "$ORIGIN" config core.autocrlf false
  mkdir -p "$ORIGIN/docker/scripts" "$ORIGIN/$migrations" "$(dirname "$ORIGIN/$initializer")"
  cp "$ROOT/docker/scripts/_paths.sh" "$ORIGIN/docker/scripts/"
  printf 'name: dm\n' > "$ORIGIN/docker/docker-compose.yml"
  printf 'services: {}\n' > "$ORIGIN/docker/docker-compose.preview.yml"
  printf 'initial\n' > "$ORIGIN/$migrations/20250101_InitialCreate.cs"
  printf 'snapshot\n' > "$ORIGIN/$migrations/DmDbContextModelSnapshot.cs"
  printf 'indexes\n' > "$ORIGIN/$initializer"
  printf 'not exported\n' > "$ORIGIN/README.md"
  git -C "$ORIGIN" add -A
  git -C "$ORIGIN" commit -q -m one
  local commit other
  commit="$(git -C "$ORIGIN" rev-parse HEAD)"
  git -C "$ORIGIN" commit -q --allow-empty -m two
  other="$(git -C "$ORIGIN" rev-parse HEAD)"

  ssh-keygen -q -t ed25519 -N '' -C pinned -f "$WORK/pinned"
  ssh-keygen -q -t ed25519 -N '' -C stray -f "$WORK/stray"
  printf 'release namespaces="git" %s\n' "$(cat "$WORK/pinned.pub")" > "$DM3_ETC/release-signers"

  # The message of a release; each argument after the name replaces one line, or drops it when
  # empty, or adds a line when it does not name a field.
  message() {
    local name=$1; shift
    local -A line=(
      [commit]="commit: $commit"
      [api]="api: ${prefix}dm-api@$digest"
      [front]="front: ${prefix}dm-front@$digest"
      [worker-mail]="worker-mail: ${prefix}dm-worker-mail@$digest"
      [worker-notification]="worker-notification: ${prefix}dm-worker-notification@$digest"
    )
    local extra=() change key
    for change in "$@"; do
      key="${change%%=*}"
      if [ -n "${line[$key]+x}" ]; then
        line[$key]="${change#*=}"
      else
        extra+=("$change")
      fi
    done
    printf 'DM3 release %s\n\n' "$name"
    for key in commit api front worker-mail worker-notification; do
      [ -z "${line[$key]}" ] || printf '%s\n' "${line[$key]}"
    done
    [ "${#extra[@]}" -eq 0 ] || printf '%s\n' "${extra[@]}"
  }

  # Signs a tag with the key given, on the first commit.
  sign() {
    local name=$1 key=$2; shift 2
    message "$name" "$@" > "$WORK/message"
    git -C "$ORIGIN" -c gpg.format=ssh -c user.signingkey="$WORK/$key" \
      tag -s -F "$WORK/message" "$name" "$commit"
  }

  tag_id() { git -C "$ORIGIN" rev-parse "refs/tags/$1"; }

  # Runs a function of the script that must refuse, and asserts the reason it gives.
  refused() {
    local what=$1 reason=$2; shift 2
    if "$@" > /dev/null 2> "$WORK/reason"; then
      fail "accepted $what"
    fi
    grep -qF -- "$reason" "$WORK/reason" ||
      fail "refused $what for another reason than \"$reason\": $(cat "$WORK/reason")"
    echo "  refused: $what"
  }

  echo "a tag that is not exactly a signed release of the pinned key is refused with its reason"
  sign v1.0.0 pinned
  git -C "$ORIGIN" tag v1.0.1 "$commit"
  refused "a lightweight tag" "lightweight" remote_tag_id v1.0.1
  git -C "$ORIGIN" tag -a -F "$WORK/message" v1.0.2 "$commit"
  refused "an unsigned tag" "not signed" verify_tag_object "$ORIGIN" v1.0.2 "$(tag_id v1.0.2)"
  sign v1.0.3 stray
  refused "a tag signed with a key that is not pinned" "not signed by a pinned key" \
    verify_tag_object "$ORIGIN" v1.0.3 "$(tag_id v1.0.3)"

  cp "$DM3_ETC/release-signers" "$WORK/signers.kept"
  : > "$DM3_ETC/release-signers"
  refused "a tag whose key line was removed" "not signed by a pinned key" \
    verify_tag_object "$ORIGIN" v1.0.0 "$(tag_id v1.0.0)"
  cp "$WORK/signers.kept" "$DM3_ETC/release-signers"

  git -C "$ORIGIN" update-ref refs/tags/v9.0.0 "$(tag_id v1.0.0)"
  refused "a signed tag pushed under a higher name" "names itself v1.0.0" \
    verify_tag_object "$ORIGIN" v9.0.0 "$(tag_id v9.0.0)"

  sign v1.0.4 pinned
  { git -C "$ORIGIN" cat-file tag v1.0.4; printf 'commit: %s\n' "$other"; } > "$WORK/appended"
  git -C "$ORIGIN" update-ref refs/tags/v1.0.4 \
    "$(git -C "$ORIGIN" hash-object -t tag -w --literally --stdin < "$WORK/appended")"
  refused "a tag with bytes after its signature" "bytes after its signature" \
    verify_tag_object "$ORIGIN" v1.0.4 "$(tag_id v1.0.4)"

  sign v1.0.5 pinned front=
  refused "a tag missing a field" "no front field" verify_tag_object "$ORIGIN" v1.0.5 "$(tag_id v1.0.5)"
  sign v1.0.6 pinned "api: ${prefix}dm-api@$digest"
  refused "a tag naming a field twice" "api twice" verify_tag_object "$ORIGIN" v1.0.6 "$(tag_id v1.0.6)"
  sign v1.0.7 pinned "schema: reset"
  refused "a tag with an extra field" "not a field of a release" \
    verify_tag_object "$ORIGIN" v1.0.7 "$(tag_id v1.0.7)"
  sign v1.0.8 pinned "api=api: registry.test/elsewhere/dm-api@$digest"
  refused "a reference under another prefix" "not ${prefix}dm-api@sha256" \
    verify_tag_object "$ORIGIN" v1.0.8 "$(tag_id v1.0.8)"
  sign v1.0.9 pinned "commit=commit: $other"
  refused "a commit that is not the target" "not the commit the tag points at" \
    verify_tag_object "$ORIGIN" v1.0.9 "$(tag_id v1.0.9)"

  local name
  # shellcheck disable=SC2016 # a name with shell syntax in it, kept literal on purpose
  for name in v01.0.0 "v$(printf '1%.0s' {1..30}).0.0" 'v1.0.0;id' 'v1.0.0$(id)' 'v1.0.0 ' '-v1.0.0'; do
    refused "the name $name" "is not vMAJOR.MINOR.PATCH" verify_tag_object "$ORIGIN" "$name" "$(tag_id v1.0.0)"
    refused "the name $name on the origin" "is not vMAJOR.MINOR.PATCH" remote_tag_id "$name"
  done

  sign v1.1.0 pinned
  git -C "$ORIGIN" tag -d v1.1.0 > /dev/null
  refused "a tag deleted before the call" "not on the origin" remote_tag_id v1.1.0

  printf 'origin=%s\nimage_prefix=%s\n' "$WORK/missing" "$prefix" > "$DM3_ETC/release.conf"
  refused "an unreachable origin" "cannot be reached" remote_tag_id v1.0.0
  printf 'origin=%s\nimage_prefix=%s\n' "$ORIGIN" "$prefix" > "$DM3_ETC/release.conf"

  echo "the signed release of the pinned key is accepted and its fields printed"
  local id fields
  id="$(remote_tag_id v1.0.0)" || fail "remote_tag_id refused the good tag"
  test "$id" = "$(tag_id v1.0.0)" || fail "remote_tag_id gave $id, not the id of the tag object"
  fields="$(verify_tag_object "$ORIGIN" v1.0.0 "$id")" || fail "verify_tag_object refused the good tag"
  for line in "commit=$commit" "api=${prefix}dm-api@$digest" "front=${prefix}dm-front@$digest" \
    "worker-mail=${prefix}dm-worker-mail@$digest" \
    "worker-notification=${prefix}dm-worker-notification@$digest"; do
    grep -qxF -- "$line" <<< "$fields" || fail "the fields of the good tag lack $line: $fields"
  done
  grep -qxE 'signed=[0-9a-f]{64}' <<< "$fields" || fail "the fields of the good tag carry no hash of the signed text"

  local limits=missing
  (ulimit -f 1024) 2> /dev/null && limits=ok
  if only_where "$limits" "the fetch into a scratch repository under a size limit"; then
    fields="$(fetch_and_verify v1.0.0)" || fail "fetch_and_verify refused the good tag"
    grep -qxF "commit=$commit" <<< "$fields" || fail "fetch_and_verify printed no commit: $fields"
    grep -qxF "id=$id" <<< "$fields" || fail "fetch_and_verify printed no tag id: $fields"
    test -z "$(ls -A "$DM3_STATE/scratch")" || fail "fetch_and_verify left its scratch repository behind"
    refused "a fetched tag signed with a key that is not pinned" "not signed by a pinned key" fetch_and_verify v1.0.3
    test -z "$(ls -A "$DM3_STATE/scratch")" || fail "a refused fetch left its scratch repository behind"
  fi

  echo "a schema change is told apart: an edited migration, an added one, the snapshot"
  manifest() {
    local line
    for line in "$@"; do
      printf '100644 blob %s\t%s\n' "${line%% *}" "${line#* }"
    done
  }
  manifest "$(printf '1%.0s' {1..40}) $migrations/1_InitialCreate.cs" \
    "$(printf '2%.0s' {1..40}) $migrations/DmDbContextModelSnapshot.cs" \
    "$(printf '3%.0s' {1..40}) $initializer" > "$WORK/live.manifest"
  manifest "$(printf '1%.0s' {1..40}) $migrations/1_InitialCreate.cs" \
    "$(printf '4%.0s' {1..40}) $migrations/DmDbContextModelSnapshot.cs" \
    "$(printf '3%.0s' {1..40}) $initializer" > "$WORK/snapshot.manifest"
  manifest "$(printf '5%.0s' {1..40}) $migrations/1_InitialCreate.cs" \
    "$(printf '2%.0s' {1..40}) $migrations/DmDbContextModelSnapshot.cs" \
    "$(printf '3%.0s' {1..40}) $initializer" > "$WORK/edited.manifest"
  { cat "$WORK/live.manifest"; manifest "$(printf '6%.0s' {1..40}) $migrations/2_Added.cs"; } > "$WORK/added.manifest"
  manifest "$(printf '1%.0s' {1..40}) $migrations/1_InitialCreate.cs" \
    "$(printf '2%.0s' {1..40}) $migrations/DmDbContextModelSnapshot.cs" \
    "$(printf '7%.0s' {1..40}) $initializer" > "$WORK/indexes.manifest"
  { cat "$WORK/indexes.manifest"; manifest "$(printf '6%.0s' {1..40}) $migrations/2_Added.cs"; } > "$WORK/indexes-added.manifest"

  schema() { refuse_schema_change "$1" "$2" "$WORK/live.manifest" "$WORK/$3.manifest" v1.0.0; }
  schema migrate 0 live || fail "an unchanged schema was refused"
  schema migrate 0 snapshot || fail "a changed model snapshot alone was refused"
  schema migrate 0 added || fail "an added migration was refused in regime migrate"
  schema migrate 0 indexes-added || fail "a new expression index with its migration was refused in regime migrate"
  schema reset 1 edited || fail "an edited migration was refused with --reset-database in regime reset"
  schema reset 1 added || fail "an added migration was refused with --reset-database in regime reset"
  refused "an edited migration in regime migrate" "existing migration changed" schema migrate 0 edited
  refused "--reset-database in regime migrate" "regime migrate" schema migrate 1 live
  refused "an index change without a migration in regime migrate" "existing migration changed" schema migrate 0 indexes
  refused "an edited migration in regime reset" "--reset-database" schema reset 0 edited
  refused "an added migration in regime reset" "--reset-database" schema reset 0 added
  if [ -z "$(refuse_schema_change migrate 0 "$WORK/none.manifest" "$WORK/edited.manifest" v1.0.0 2>&1)" ]; then
    echo "  accepted: any schema on a server with no release yet"
  else
    fail "the first release of a server was refused for its schema"
  fi

  echo "a release directory holds the commit's docker/, release.env and schema.manifest, read-only"
  git clone -q --bare "$ORIGIN" "$DM3_ROOT/repo.git"
  local signed rid dir
  signed="$(sed -n 's/^signed=//p' <<< "$(verify_tag_object "$ORIGIN" v1.0.0 "$id")")"
  rid="$(release_id v1.0.0 "$commit" "$signed")"
  test "$rid" = "v1.0.0-${commit:0:7}-${signed:0:12}" ||
    fail "the release id is $rid, not the tag, the short commit and 12 hex of the signed text"
  dir="$(build_release_dir "$rid" v1.0.0 "$commit" v0.9.0-0000000-000000000000 \
    "${prefix}dm-api@$digest" "${prefix}dm-front@$digest" \
    "${prefix}dm-worker-mail@$digest" "${prefix}dm-worker-notification@$digest")" ||
    fail "build_release_dir refused a verified release"
  test "$dir" = "$DM3_ROOT/rel/$rid" || fail "the release directory is $dir, not rel/$rid"
  test "$(ls -A "$DM3_ROOT/rel")" = "$rid" || fail "building a release left more than rel/$rid: $(ls -A "$DM3_ROOT/rel")"
  test -f "$dir/docker/scripts/_paths.sh" || fail "the release directory lacks the commit's docker/"
  if [ -e "$dir/README.md" ] || [ -e "$dir/src" ]; then
    fail "the release directory holds more than docker/"
  fi
  printf '%s\n' "RELEASE_TAG=v1.0.0" "RELEASE_COMMIT=$commit" "RELEASE_REPLACES=v0.9.0-0000000-000000000000" \
    "DM_IMAGE_API=${prefix}dm-api@$digest" "DM_IMAGE_FRONT=${prefix}dm-front@$digest" \
    "DM_IMAGE_WORKER_MAIL=${prefix}dm-worker-mail@$digest" \
    "DM_IMAGE_WORKER_NOTIFICATION=${prefix}dm-worker-notification@$digest" \
    "SOURCE_REVISION=$commit" | cmp -s - "$dir/release.env" ||
    fail "release.env is not the eight keys of a release: $(cat "$dir/release.env")"
  git -C "$ORIGIN" ls-tree -r "$commit" -- "$migrations" "$initializer" | cmp -s - "$dir/schema.manifest" ||
    fail "schema.manifest is not the ls-tree lines of the migrations and the index initializer"
  mkdir "$WORK/mode-probe" && chmod a-w "$WORK/mode-probe"
  local modes=missing
  [ -n "$(find "$WORK/mode-probe" -perm -u=w)" ] || modes=ok
  if only_where "$modes" "the release directory is read-only"; then
    test -z "$(find "$dir" -perm -u=w)" || fail "the release directory is writable: $(find "$dir" -perm -u=w | head -n 3)"
  fi

  echo "every verb reaches compose through one command line"
  mkdir -p "$WORK/bin"
  cat > "$WORK/bin/docker" << 'STUB'
#!/bin/bash
# Stands in for docker: records the call, answers ps like compose would.
printf '%s\n' "DM_EDGE_DIR=${DM_EDGE_DIR:-}" "$@" > "$DOCKER_CALL"
case " $* " in
  *' ps '*) printf 'dmapi\trunning\nnginx\trunning\n' ;;
esac
STUB
  chmod +x "$WORK/bin/docker"
  export DOCKER_CALL="$WORK/docker.call"
  local path_before=$PATH
  PATH="$WORK/bin:$PATH"
  compose_cmd "$dir" config > /dev/null
  printf '%s\n' "DM_EDGE_DIR=$DM3_ROOT/shared/edge" compose \
    --project-directory "$DM3_ROOT/rel/current/docker" \
    --env-file "$DM3_ROOT/shared/env" --env-file "$dir/release.env" \
    -f "$dir/docker/docker-compose.yml" -f "$dir/docker/docker-compose.preview.yml" config |
    cmp -s - "$DOCKER_CALL" ||
    fail "the compose command line is not the one of the design: $(tr '\n' ' ' < "$DOCKER_CALL")"

  echo "status reads an empty layout and one release, and changes neither"
  local listing
  listing="$(find "$WORK/opt" "$WORK/etc" "$WORK/state" | sort)"
  verb_status > "$WORK/status" || fail "status failed on a layout with no release"
  grep -q "no release is live" "$WORK/status" || fail "status on an empty layout says: $(cat "$WORK/status")"
  local links=missing
  ln -s "$rid" "$WORK/link-probe" 2> /dev/null && [ -L "$WORK/link-probe" ] && links=ok
  if only_where "$links" "status of a live release through rel/current"; then
    commit_current "$rid"
    test "$(readlink "$DM3_ROOT/rel/current")" = "$rid" || fail "rel/current does not name $rid"
    listing="$(find "$WORK/opt" "$WORK/etc" "$WORK/state" | sort)"
    verb_status > "$WORK/status" || fail "status failed on a live release"
    for line in "release: $rid" "tag: v1.0.0" "commit: $commit" "api: ${prefix}dm-api@$digest" \
      "front: ${prefix}dm-front@$digest" "worker-mail: ${prefix}dm-worker-mail@$digest" \
      "worker-notification: ${prefix}dm-worker-notification@$digest" "in-flight: no"; do
      grep -qxF -- "$line" "$WORK/status" || fail "status does not say \"$line\": $(cat "$WORK/status")"
    done
    grep -qxF "$(printf 'dmapi\trunning')" "$WORK/status" || fail "status does not list the containers"
    touch "$DM3_STATE/in-flight"
    verb_status | grep -q "^in-flight: yes" || fail "status does not report the marker of a run that died"
    rm "$DM3_STATE/in-flight"
  fi
  test "$(find "$WORK/opt" "$WORK/etc" "$WORK/state" | sort)" = "$listing" || fail "status changed the layout"
  PATH=$path_before

  echo "a second run that changes something exits while the lock is held, boot waits for it"
  local locking=missing
  command -v flock > /dev/null && locking=ok
  if only_where "$locking" "the lock between runs"; then
    flock "$DM3_STATE/lock" sleep 3 &
    local holder=$! started
    sleep 1
    refused "a second run while the lock is held" "holds the lock" take_lock nowait
    started=$SECONDS
    ( take_lock wait ) || fail "a waiting run failed instead of waiting for the lock"
    [ "$((SECONDS - started))" -ge 1 ] || fail "a waiting run went ahead while the lock was held"
    wait "$holder"
  fi

  echo "OK: the release script refuses every tag but a signed release of the pinned key, and its layout holds its form."
}

# Runs 0 and 8 of the release design (3.16) on a throwaway machine, as root: the four images the
# job built go through a registry on loopback, this commit is signed into a local origin with a
# key made here, and the layout is installed by its own installer. Nothing here can run twice
# on one machine: the layout's guard refuses a second installation.
layout() {
  [ "$(id -u)" -eq 0 ] || fail "--layout installs the server layout and runs as root"
  local tool
  for tool in docker systemctl systemd-run runuser flock jq ssh-keygen; do
    command -v "$tool" > /dev/null || fail "--layout needs $tool"
  done

  # 5001, since the API publishes 5000 on loopback. Loopback is a registry docker trusts over
  # plain http.
  local prefix='127.0.0.1:5001/dm-am/' name=v1.0.0 password=smoke-test-password
  WORK="$(mktemp -d /var/tmp/dm3-layout.XXXXXX)"
  # The release account reads the origin from here.
  chmod 755 "$WORK"
  trap 'docker rm -f dm-smoke-registry > /dev/null 2>&1 || true; rm -rf "$WORK"' EXIT

  echo "run 0: maintain of a signed tag on an empty layout"
  docker run -d --name dm-smoke-registry -p 127.0.0.1:5001:5000 registry:2.8.3 > /dev/null
  local tries=0
  until curl -fsS -o /dev/null http://127.0.0.1:5001/v2/; do
    tries=$((tries + 1))
    [ "$tries" -lt 30 ] || fail "the local registry does not answer"
    sleep 1
  done

  local commit image ref
  commit="$(git -c safe.directory="$ROOT" -C "$ROOT" rev-parse HEAD)"
  declare -A refs=()
  for image in dm-api dm-front dm-worker-mail dm-worker-notification; do
    docker tag "ghcr.io/dm-am/$image:latest" "$prefix$image:smoke"
    docker push -q "$prefix$image:smoke" > /dev/null
    ref="$(docker image inspect --format '{{range .RepoDigests}}{{println .}}{{end}}' "$prefix$image:smoke" |
      grep -F "$prefix$image@sha256:" | head -n 1 || true)"
    test -n "$ref" || fail "$image has no digest in the local registry"
    refs[$image]=$ref
    # Removed again, so the release pulls it from the registry by digest as a server does.
    docker image rm "$prefix$image:smoke" > /dev/null
  done

  # The account first: git refuses a repository another user owns, so the origin is handed to it.
  id -u dm-release > /dev/null 2>&1 ||
    useradd --system --home-dir /var/lib/dm3 --no-create-home --shell /usr/sbin/nologin dm-release
  local origin="$WORK/origin.git"
  git init -q --bare "$origin"
  git -c safe.directory="$ROOT" -C "$ROOT" push -q "$origin" "HEAD:refs/heads/dev"
  ssh-keygen -q -t ed25519 -N '' -C smoke -f "$WORK/release-key"
  printf 'smoke namespaces="git" %s\n' "$(cat "$WORK/release-key.pub")" > "$WORK/allowed_signers"
  printf '%s\n' "DM3 release $name" "" "commit: $commit" "api: ${refs[dm-api]}" "front: ${refs[dm-front]}" \
    "worker-mail: ${refs[dm-worker-mail]}" "worker-notification: ${refs[dm-worker-notification]}" > "$WORK/message"
  git -C "$origin" -c user.name=smoke -c user.email=smoke@example.invalid -c gpg.format=ssh \
    -c user.signingkey="$WORK/release-key" tag -s -F "$WORK/message" "$name" "$commit"
  chown -R dm-release: "$origin"

  DM_PREVIEW_PASSWORD="$password" bash "$ROOT/docker/scripts/install-release.sh" \
    --signers "$WORK/allowed_signers" --tag "$name" --origin "file://$origin" --image-prefix "$prefix" \
    --mail-host dm-mailpit --mail-port 1025 --no-timer ||
    fail "install-release.sh did not install the signed release"

  local rid
  rid="$(readlink /opt/dm3/rel/current || true)"
  [[ $rid =~ ^v1\.0\.0-${commit:0:7}-[0-9a-f]{12}$ ]] || fail "rel/current names $rid, not a release of $name"
  local container
  for container in dm-api:dm-api dm-front:dm-front dm-mail-worker:dm-worker-mail \
    dm-notification-worker:dm-worker-notification; do
    test "$(docker inspect --format '{{.Config.Image}}' "${container%%:*}")" = "${refs[${container#*:}]}" ||
      fail "${container%%:*} does not run the image the tag signed"
  done
  # shellcheck source=docker/scripts/release-entry.sh
  . /usr/local/sbin/dm3-release
  while IFS= read -r image; do
    docker image inspect "$image" > /dev/null 2>&1 || fail "the release runs $image, which was never pulled"
  done < <(compose_cmd "$REL/current" config --images)
  bash "$ROOT/docker/scripts/smoke-edge.sh" http://localhost preview "$password" > /dev/null ||
    fail "the edge of the installed release does not answer as the deployment needs"
  test -z "$(find /opt/dm3/repo.git ! -user dm-release | head -n 3)" ||
    fail "files under repo.git belong to someone other than dm-release"
  test -z "$(ls -A /var/lib/dm3/scratch)" || fail "a scratch repository outlived the run"
  test "$(systemctl is-active dm3.service)" = active || fail "dm3.service is not active after the installation"
  test "$(systemctl is-enabled dm3-release.timer)" = enabled || fail "the release timer is not enabled"
  test "$(systemctl is-active dm3-release.timer || true)" = inactive || fail "--no-timer started the release timer"
  local pinned
  pinned="$(stat -c '%n %i %a %U %Y' /opt/dm3/shared/env /etc/dm3 /etc/dm3/release-signers /etc/dm3/release.conf)"

  echo "run 8: a rollout that died after the primaries were recreated, the daemon's restarts, then boot"
  compose_cmd "$REL/current" --profile rollout up -d --no-deps --no-build --pull never dmapi-next dmfront-next
  for container in dm-api-next dm-front-next; do
    tries=0
    until [ "$(docker inspect --format '{{.State.Health.Status}}' "$container")" = healthy ]; do
      tries=$((tries + 1))
      [ "$tries" -lt 90 ] || fail "$container is not healthy after 180 s"
      sleep 2
    done
  done
  edge_switch next || fail "the edge did not take the twins"
  test "$(docker exec dm-nginx readlink /etc/nginx/upstreams-live/api.conf)" = /etc/nginx/upstreams/api-next.conf ||
    fail "the edge links do not point at the twins"
  : > /var/lib/dm3/in-flight
  docker restart dm-nginx dm-api-next dm-front-next dm-api dm-front > /dev/null
  /usr/local/sbin/dm3-release boot || fail "boot did not bring the release back"
  if [ "$(docker exec dm-nginx readlink /etc/nginx/upstreams-live/api.conf)" != /etc/nginx/upstreams/api-primary.conf ] ||
    [ "$(docker exec dm-nginx readlink /etc/nginx/upstreams-live/frontend.conf)" != /etc/nginx/upstreams/front-primary.conf ]; then
    fail "after boot the edge links do not point at the primaries"
  fi
  test -z "$(docker ps -a --format '{{.Names}}' | grep -E '^dm-(api|front)-next$' || true)" || fail "a twin outlived boot"
  test ! -e /var/lib/dm3/in-flight || fail "boot left the marker of the run that died"
  test "$(readlink /opt/dm3/rel/current)" = "$rid" || fail "boot changed what rel/current names"
  bash "$ROOT/docker/scripts/smoke-edge.sh" http://localhost preview "$password" > /dev/null ||
    fail "the edge does not answer after boot"
  test "$(stat -c '%n %i %a %U %Y' /opt/dm3/shared/env /etc/dm3 /etc/dm3/release-signers /etc/dm3/release.conf)" = "$pinned" ||
    fail "the shared file or the pins changed, which only the operator writes"

  echo "OK: the server layout installs a signed release and boots back to it."
}

case "${1:-}" in
  '') offline ;;
  --layout) layout ;;
  *) fail "unknown argument ${1}; expected none or --layout" ;;
esac
