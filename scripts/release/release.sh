#!/usr/bin/env bash
# The release command: checks that a commit of dev was built, attested and labelled by CI,
# then writes, signs and pushes the one release tag naming its four image digests. The server
# deploys only what such a tag names, so everything this script refuses is a release that
# never happens. It does not wait for the server: the signer learns the outcome from the
# letter. The procedure around it (a worktree at the last release tag, the review of this
# folder since that tag) is in docs/guides/DEPLOYMENT.md, section "Выпуск релиза".
#
# It acts on the repository it sits in, not the current directory: the command that signs is
# the one the signer reviewed at the tag. Every git call runs with hooks off and the push
# skips pre-push, since hooks run code from the tree and could sign other digests.
set -euo pipefail
export LC_ALL=C

usage() {
  cat << 'USAGE'
release.sh <vMAJOR.MINOR.PATCH> [<commit>]     commit defaults to origin/dev after the fetch
env: DM_RELEASE_IMAGE_PREFIX   default ghcr.io/dm-am/dm (images <prefix>-api|-front|-worker-mail|-worker-notification)
     DM_RELEASE_UNATTESTED=1   proof only; refused when the prefix is ghcr.io/dm-am/dm
USAGE
}

REPOSITORY=dm-am/dm3
PRODUCTION_PREFIX=ghcr.io/dm-am/dm
IMAGES=(api front worker-mail worker-notification)
# The whole name, each number 0 or without a leading zero and at most six digits: the server
# matches tags the same way, and a name it would reject is a release that never lands.
NUMBER='(0|[1-9][0-9]{0,5})'
RELEASE_NAME="^v$NUMBER\\.$NUMBER\\.$NUMBER\$"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT"

WORK="$(mktemp -d)"
VERSION=""
TAG_CREATED=0
TAG_PUSHED=0
cleanup() {
  # A local tag that never reached origin would block the same version on the next attempt
  # and look like a release that happened.
  if ((TAG_CREATED && !TAG_PUSHED)); then
    g tag -d "$VERSION" > /dev/null 2>&1 || true
  fi
  rm -rf "$WORK"
}
trap cleanup EXIT

g() { git -c core.hooksPath=/dev/null "$@"; }

refuse() {
  printf 'release: %s\n' "$1" >&2
  exit 1
}

parse_args() {
  if (($# == 1)) && [[ $1 == -h || $1 == --help ]]; then
    usage
    exit 0
  fi
  if (($# < 1 || $# > 2)); then
    usage >&2
    exit 1
  fi
  VERSION=$1
  COMMIT_ARG=${2:-}
  [[ $VERSION =~ $RELEASE_NAME ]] ||
    refuse "'$VERSION' is not a release version: vMAJOR.MINOR.PATCH, no leading zeros, at most six digits each"
  [[ $COMMIT_ARG != -* ]] || refuse "'$COMMIT_ARG' is not a commit"

  PREFIX=${DM_RELEASE_IMAGE_PREFIX:-$PRODUCTION_PREFIX}
  [[ $PREFIX =~ ^[a-z0-9][a-z0-9._:/-]*$ ]] || refuse "DM_RELEASE_IMAGE_PREFIX '$PREFIX' is not an image name prefix"
  case ${DM_RELEASE_UNATTESTED:-} in
    "") UNATTESTED=0 ;;
    1) UNATTESTED=1 ;;
    *) refuse "DM_RELEASE_UNATTESTED must be 1 or unset" ;;
  esac
  # The switch exists for the proof, whose images live in a registry of its own; for the
  # images the server pulls, an unattested digest is exactly what the check is there to stop.
  if ((UNATTESTED)) && [[ $PREFIX == "$PRODUCTION_PREFIX" ]]; then
    refuse "DM_RELEASE_UNATTESTED is for the proof and works only with another DM_RELEASE_IMAGE_PREFIX"
  fi
}

# at_least <have> <want>, both MAJOR.MINOR.
at_least() {
  local have_major=${1%%.*} have_minor=${1#*.} want_major=${2%%.*} want_minor=${2#*.}
  ((have_major > want_major || (have_major == want_major && have_minor >= want_minor)))
}

require_tools() {
  local version
  # 2.34 is the first git that signs and verifies tags with SSH keys; 8.2 the first OpenSSH
  # whose ssh-keygen signs with -Y.
  [[ $(git --version) =~ ([0-9]+)\.([0-9]+) ]] || refuse "cannot read the version of git"
  version="${BASH_REMATCH[1]}.${BASH_REMATCH[2]}"
  at_least "$version" 2.34 || refuse "git $version is older than 2.34"
  [[ $(ssh -V 2>&1) =~ OpenSSH_([0-9]+)\.([0-9]+) ]] || refuse "cannot read the version of OpenSSH"
  version="${BASH_REMATCH[1]}.${BASH_REMATCH[2]}"
  at_least "$version" 8.2 || refuse "OpenSSH $version is older than 8.2"

  [[ $(g config --get gpg.format || true) == ssh ]] ||
    refuse "git is not set up to sign: gpg.format is not ssh"
  SIGNING_KEY=$(g config --get user.signingkey || true)
  [[ -n $SIGNING_KEY ]] || refuse "git is not set up to sign: user.signingkey is not set"

  command -v docker > /dev/null 2>&1 || refuse "docker is not installed"
  docker buildx version > /dev/null 2>&1 || refuse "docker has no buildx"
  if ((!UNATTESTED)); then
    command -v gh > /dev/null 2>&1 || refuse "gh is not installed"
    gh auth status > /dev/null 2>&1 || refuse "gh is not signed in: gh auth login"
  fi
}

# Release tags from origin and from this clone: name -> commit and name -> the object the ref
# names. Origin's answer wins, it is what the server reads.
declare -A TAG_COMMIT=() TAG_OBJECT=()
HIGHEST=""
collect_tags() {
  local listing sha ref name
  local -a missing=()
  listing=$(g ls-remote --tags origin) || refuse "cannot list the tags of origin"
  while read -r sha ref; do
    [[ -n $ref ]] || continue
    name=${ref#refs/tags/}
    if [[ $name == *'^{}' ]]; then
      name=${name%'^{}'}
      [[ $name =~ $RELEASE_NAME ]] && TAG_COMMIT[$name]=$sha
      continue
    fi
    [[ $name =~ $RELEASE_NAME ]] || continue
    TAG_OBJECT[$name]=$sha
    [[ -n ${TAG_COMMIT[$name]:-} ]] || TAG_COMMIT[$name]=$sha
  done <<< "$listing"
  while read -r name; do
    [[ -n $name && $name =~ $RELEASE_NAME && -z ${TAG_COMMIT[$name]:-} ]] || continue
    TAG_COMMIT[$name]=$(g rev-parse "refs/tags/$name^{commit}")
    TAG_OBJECT[$name]=$(g rev-parse "refs/tags/$name")
  done <<< "$(g tag -l)"
  # Any name takes a version, but only the signer's tags set the bar: anyone who can push
  # could otherwise block every release with an unsigned v9.9.9. The objects only, no refs.
  for name in "${!TAG_OBJECT[@]}"; do
    g cat-file -e "${TAG_OBJECT[$name]}" 2> /dev/null || missing+=("refs/tags/$name")
  done
  if ((${#missing[@]})); then
    g fetch --quiet --no-tags --no-write-fetch-head origin "${missing[@]}" ||
      refuse "cannot fetch the release tags of origin"
  fi
  write_allowed_signers
  for name in "${!TAG_OBJECT[@]}"; do
    signed_by_signer "$name" "${TAG_OBJECT[$name]}" || continue
    if [[ -z $HIGHEST ]] || above "$name" "$HIGHEST"; then
      HIGHEST=$name
    fi
  done
}

# above <a> <b>: a is the higher release version.
above() {
  local -a a b
  IFS=. read -r -a a <<< "${1#v}"
  IFS=. read -r -a b <<< "${2#v}"
  ((a[0] != b[0])) && { ((a[0] > b[0])); return; }
  ((a[1] != b[1])) && { ((a[1] > b[1])); return; }
  ((a[2] > b[2]))
}

require_worktree() {
  [[ -z $(g status --porcelain) ]] || refuse "the worktree has changes: run from a clean worktree"
  # The command that signs must be the one reviewed: the one at the last release tag, plus
  # the diff of scripts/release the signer read.
  if [[ -n $HIGHEST && $(g rev-parse HEAD) != "${TAG_COMMIT[$HIGHEST]}" ]]; then
    refuse "HEAD is not at $HIGHEST, the last release tag: run from a worktree at it"
  fi
}

check_version() {
  [[ -z ${TAG_COMMIT[$VERSION]:-} ]] || refuse "$VERSION is already a tag"
  if [[ -n $HIGHEST ]] && ! above "$VERSION" "$HIGHEST"; then
    refuse "$VERSION is not above the highest release tag $HIGHEST"
  fi
}

check_commit() {
  SHA=$(g rev-parse --verify --quiet "${COMMIT_ARG:-refs/remotes/origin/dev}^{commit}") ||
    refuse "'${COMMIT_ARG:-origin/dev}' is not a commit"
  g merge-base --is-ancestor "$SHA" refs/remotes/origin/dev ||
    refuse "$SHA is not on origin/dev"
}

declare -A REFERENCE=()

resolve_from_registry() {
  local image digest
  for image in "${IMAGES[@]}"; do
    digest=$(docker buildx imagetools inspect "$PREFIX-$image:${SHA:0:7}" --format '{{.Manifest.Digest}}') ||
      refuse "the registry has no $PREFIX-$image:${SHA:0:7}"
    [[ $digest =~ ^sha256:[0-9a-f]{64}$ ]] || refuse "the registry answered '$digest' for $PREFIX-$image:${SHA:0:7}"
    REFERENCE[$image]="$PREFIX-$image@$digest"
  done
}

find_run() {
  local newest status conclusion jobs job publish
  newest=$(gh run list -R "$REPOSITORY" --workflow dotnet.yml --branch dev --event push \
    --commit "$SHA" --json databaseId,status,conclusion \
    --jq 'if length == 0 then "none" else (.[0] | "\(.databaseId) \(.status) \(.conclusion)") end') ||
    refuse "cannot list the runs of $REPOSITORY"
  read -r RUN status conclusion <<< "$newest"
  [[ $RUN != none ]] || refuse "no run of dotnet.yml on dev for $SHA"
  [[ $RUN =~ ^[0-9]+$ ]] || refuse "gh answered '$newest' for the runs of $SHA"
  [[ $status == completed && $conclusion == success ]] ||
    refuse "run $RUN of $SHA is $status/$conclusion, not a success"

  jobs=$(gh run view "$RUN" -R "$REPOSITORY" --json jobs --jq '.jobs[] | "\(.conclusion)\t\(.name)"') ||
    refuse "cannot read the jobs of run $RUN"
  # A run is green when its publishers were skipped by the path map: such a run published
  # nothing for this commit.
  for job in publish-frontend promote-images; do
    grep -qxF "success"$'\t'"$job" <<< "$jobs" || refuse "job $job did not succeed in run $RUN"
  done
  publish=$(grep -c "^success"$'\t'"publish (" <<< "$jobs" || true)
  ((publish == 3)) || refuse "publish legs succeeded: $publish of 3 in run $RUN"
}

download_digests() {
  local image content
  for image in "${IMAGES[@]}"; do
    gh run download "$RUN" -R "$REPOSITORY" -n "digest-$image" -D "$WORK/$image" > /dev/null 2>&1 ||
      refuse "cannot download the artifact digest-$image of run $RUN"
    content=""
    [[ -f $WORK/$image/reference ]] && content=$(< "$WORK/$image/reference")
    [[ $content == "$PREFIX-$image@sha256:"* && ${content#"$PREFIX-$image@sha256:"} =~ ^[0-9a-f]{64}$ ]] ||
      refuse "the artifact digest-$image does not hold one reference to $PREFIX-$image@sha256:<64 hex>"
    REFERENCE[$image]=$content
  done
}

verify_attestations() {
  local image
  # shellcheck source=/dev/null
  . "$ROOT/scripts/lib/attestation.sh"
  for image in "${IMAGES[@]}"; do
    attestation_verify "${REFERENCE[$image]}" "$SHA" ||
      refuse "the attestation of $image does not verify for $SHA"
  done
}

check_labels() {
  local image label
  for image in "${IMAGES[@]}"; do
    label=$(docker buildx imagetools inspect "${REFERENCE[$image]}" \
      --format '{{index .Image.Config.Labels "org.opencontainers.image.revision"}}') ||
      refuse "cannot read ${REFERENCE[$image]} from the registry"
    [[ $label == "$SHA" ]] || refuse "the revision label of $image is $label, not $SHA"
  done
}

write_message() {
  local image
  {
    printf 'DM3 release %s\n\ncommit: %s\n' "$VERSION" "$SHA"
    for image in "${IMAGES[@]}"; do
      printf '%s: %s\n' "$image" "${REFERENCE[$image]}"
    done
  } > "$WORK/message"
}

# The public half of user.signingkey, which git accepts as a literal key, a public key file or
# a private key file next to its .pub.
signing_public_key() {
  local key=$1 line
  case $key in
    key::*) printf '%s\n' "${key#key::}"; return ;;
    ssh-* | sk-* | ecdsa-*) printf '%s\n' "$key"; return ;;
  esac
  key=${key/#\~/$HOME}
  if [[ -f $key ]] && IFS= read -r line < "$key" && [[ $line =~ ^(ssh-|sk-|ecdsa-) ]]; then
    printf '%s\n' "$line"
  elif [[ -f $key.pub ]]; then
    head -n 1 "$key.pub"
  else
    ssh-keygen -y -f "$key"
  fi
}

write_allowed_signers() {
  local public
  public=$(signing_public_key "$SIGNING_KEY") || public=""
  [[ -n $public ]] || refuse "cannot read the public half of user.signingkey"
  printf 'release namespaces="git" %s\n' "$public" > "$WORK/allowed_signers"
}

# signed_by_signer <name> <object>: an annotated tag that names itself <name>, signed with
# user.signingkey. A signed tag pushed under a higher name verifies too, so the name is read.
signed_by_signer() {
  [[ $(g cat-file -t "$2" 2> /dev/null) == tag ]] || return 1
  [[ $(g cat-file tag "$2" | sed -n '/^$/q; s/^tag //p') == "$1" ]] || return 1
  g -c "gpg.ssh.allowedSignersFile=$WORK/allowed_signers" verify-tag "$2" > /dev/null 2>&1
}

verify_own_tag() {
  write_allowed_signers
  g -c "gpg.ssh.allowedSignersFile=$WORK/allowed_signers" verify-tag "$VERSION" > /dev/null 2>&1 ||
    refuse "the signature of $VERSION does not verify with the key that made it"
  g cat-file tag "$VERSION" |
    awk 'body && /^-----BEGIN SSH SIGNATURE-----$/ {exit} body {print} !body && /^$/ {body=1}' > "$WORK/body"
  cmp -s "$WORK/body" "$WORK/message" || refuse "the message of $VERSION is not the one printed"
}

main() {
  parse_args "$@"
  require_tools
  collect_tags
  require_worktree
  g fetch --quiet --no-tags origin +refs/heads/dev:refs/remotes/origin/dev ||
    refuse "cannot fetch dev from origin"
  check_version
  check_commit
  if ((UNATTESTED)); then
    resolve_from_registry
  else
    find_run
    download_digests
    verify_attestations
  fi
  check_labels
  write_message
  cat "$WORK/message"

  g tag -s --cleanup=verbatim -F "$WORK/message" "$VERSION" "$SHA" ||
    refuse "git could not sign $VERSION"
  TAG_CREATED=1
  verify_own_tag
  g push --quiet --no-verify origin "refs/tags/$VERSION:refs/tags/$VERSION" ||
    refuse "cannot push $VERSION to origin"
  TAG_PUSHED=1
  echo "release: $VERSION pushed; the server reports by letter"
}

main "$@"
