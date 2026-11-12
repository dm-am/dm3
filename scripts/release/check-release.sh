#!/usr/bin/env bash
# Fails when the release command signs something it is written to refuse, or stops signing
# what it should. The command only ever runs on the signer's machine against GitHub and the
# registry, so nothing else would notice: here gh and docker are stand-ins on PATH, origin is
# a bare repository on disk and the signing key is generated for the run. Needs no docker and
# no network.
#
# The command runs from inside a scratch clone whose commits carry it, as the signer runs it
# from a worktree at the last release tag: it acts on the repository it sits in, and refuses
# a tree that is dirty or not at that tag.
set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/common.sh"

test -f "$ROOT/scripts/release/release.sh" || fail "scripts/release/release.sh is missing"
test -f "$ROOT/scripts/lib/attestation.sh" ||
  fail "scripts/lib/attestation.sh is missing: the release command verifies through it"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT
export RELEASE_CHECK_WORK="$WORK"

# The machine's own git configuration must not decide the outcome: a global gpg.format or a
# system core.autocrlf would turn cases green or the scripts in the clone into CRLF.
export GIT_CONFIG_NOSYSTEM=1
export GIT_CONFIG_GLOBAL="$WORK/gitconfig"
git config --global user.name "Release check"
git config --global user.email "release-check@example.invalid"
git config --global init.defaultBranch dev
git config --global commit.gpgSign false
git config --global tag.gpgSign false

# The command's own calls disable hooks; the check's calls do too, so that only the
# command can leave the mark the clone's hooks write.
tg() { git -C "$CLONE" -c core.hooksPath=/dev/null "$@"; }

ssh-keygen -q -t ed25519 -N '' -C release-check -f "$WORK/key"

echo "a scratch origin: dev with A and B, the release v1.2.0 on A, a branch commit X"
SEED="$WORK/seed"
git init -q "$SEED"
mkdir -p "$SEED/scripts/release" "$SEED/scripts/lib"
cp "$ROOT/scripts/release/release.sh" "$SEED/scripts/release/"
cp "$ROOT/scripts/lib/attestation.sh" "$SEED/scripts/lib/"
git -C "$SEED" add -A
git -C "$SEED" commit -q -m A
A=$(git -C "$SEED" rev-parse HEAD)
git -C "$SEED" -c gpg.format=ssh -c user.signingkey="$WORK/key" tag -s -m "DM3 release v1.2.0" v1.2.0
echo b > "$SEED/b.txt"
git -C "$SEED" add -A
git -C "$SEED" commit -q -m B
B=$(git -C "$SEED" rev-parse HEAD)
git -C "$SEED" switch -q -c feature
echo x > "$SEED/x.txt"
git -C "$SEED" add -A
git -C "$SEED" commit -q -m X
X=$(git -C "$SEED" rev-parse HEAD)

ORIGIN="$WORK/origin.git"
git init -q --bare "$ORIGIN"
git -C "$SEED" push -q "$ORIGIN" dev feature v1.2.0

CLONE="$WORK/clone"
git clone -q "$ORIGIN" "$CLONE"
tg checkout -q --detach v1.2.0
tg config gpg.format ssh
tg config user.signingkey "$WORK/key"

# Any hook of the clone leaves a mark: the command must run none of them.
mkdir -p "$WORK/hooks"
for hook in pre-push reference-transaction post-checkout; do
  printf '#!/usr/bin/env bash\ntouch "%s/hook-ran"\n' "$WORK" > "$WORK/hooks/$hook"
  chmod +x "$WORK/hooks/$hook"
done
tg config core.hooksPath "$WORK/hooks"

# The stand-ins read their answers from WORK/scenario and log every call to WORK/calls. A
# digest is a function of the image, so the expected message can be written down here.
mkdir -p "$WORK/bin"
cat > "$WORK/bin/gh" << 'STANDIN'
#!/usr/bin/env bash
set -euo pipefail
S="$RELEASE_CHECK_WORK/scenario"
printf 'gh %s\n' "$*" >> "$RELEASE_CHECK_WORK/calls"
digest_of() { case $1 in api) n=1 ;; front) n=2 ;; worker-mail) n=3 ;; *) n=4 ;; esac; printf 'sha256:%064d' "$n"; }
case "$1 $2" in
  "auth status") exit 0 ;;
  "run list")
    if [[ -f $S/run-list ]]; then cat "$S/run-list"; else echo "4242 completed success"; fi ;;
  "run view")
    if [[ -f $S/jobs ]]; then
      cat "$S/jobs"
    else
      printf 'success\t%s\n' "changes" "publish (DM.Web.API, api)" "publish (DM.Workers.Mail, worker-mail)" \
        "publish (DM.Workers.NotificationDispatcher, worker-notification)" "publish-frontend" "promote-images"
    fi ;;
  "run download")
    name="" dir=""
    while (($#)); do
      case $1 in -n) name=$2; shift ;; -D) dir=$2; shift ;; esac
      shift
    done
    image=${name#digest-}
    if [[ -f $S/missing-$image ]]; then echo "no artifact matches $name" >&2; exit 1; fi
    mkdir -p "$dir"
    if [[ -f $S/reference-$image ]]; then
      cp "$S/reference-$image" "$dir/reference"
    else
      printf 'ghcr.io/dm-am/dm-%s@%s\n' "$image" "$(digest_of "$image")" > "$dir/reference"
    fi ;;
  "attestation verify")
    for image in api front worker-mail worker-notification; do
      if [[ $3 == *"-$image@"* && -f $S/attest-fail-$image ]]; then
        echo "verification failed" >&2
        exit 1
      fi
    done ;;
  *) echo "gh stand-in: unexpected call: $*" >&2; exit 2 ;;
esac
STANDIN

cat > "$WORK/bin/docker" << 'STANDIN'
#!/usr/bin/env bash
set -euo pipefail
S="$RELEASE_CHECK_WORK/scenario"
printf 'docker %s\n' "$*" >> "$RELEASE_CHECK_WORK/calls"
digest_of() { case $1 in api) n=1 ;; front) n=2 ;; worker-mail) n=3 ;; *) n=4 ;; esac; printf 'sha256:%064d' "$n"; }
if [[ "$1 $2" == "buildx version" ]]; then echo "github.com/docker/buildx v0.0.0 stand-in"; exit 0; fi
[[ "$1 $2 $3" == "buildx imagetools inspect" ]] || { echo "docker stand-in: unexpected call: $*" >&2; exit 2; }
ref=$4 format=$6
sha=$(cat "$S/sha")
for image in api front worker-mail worker-notification; do
  if [[ $ref == *"-$image@sha256:"* || $ref == *"-$image:"* ]]; then
    case $format in
      *revision*)
        if [[ -f $S/label-$image ]]; then cat "$S/label-$image"; else echo "$sha"; fi ;;
      *Manifest.Digest*)
        [[ $ref == *":${sha:0:7}" ]] || { echo "not found: $ref" >&2; exit 1; }
        digest_of "$image"; echo ;;
      *) echo "docker stand-in: unexpected format: $format" >&2; exit 2 ;;
    esac
    exit 0
  fi
done
echo "not found: $ref" >&2
exit 1
STANDIN
chmod +x "$WORK/bin/gh" "$WORK/bin/docker"

digest_of() { case $1 in api) n=1 ;; front) n=2 ;; worker-mail) n=3 ;; *) n=4 ;; esac; printf 'sha256:%064d' "$n"; }

# A case starts from a scenario where everything is in order, for the commit it releases.
scenario() {
  rm -rf "$WORK/scenario" "$WORK/calls" "$WORK/hook-ran"
  mkdir -p "$WORK/scenario"
  echo "$1" > "$WORK/scenario/sha"
}

release() {
  local status=0
  (cd "$WORK" && PATH="$WORK/bin:$PATH" bash "$CLONE/scripts/release/release.sh" "$@") \
    > "$WORK/out" 2> "$WORK/err" || status=$?
  return "$status"
}

# Refused: nonzero, the reason on stderr, no new tag at origin or in the clone.
refuses() {
  local reason=$1
  shift
  local before after before_origin after_origin
  before=$(tg tag -l | sort)
  before_origin=$(git -C "$ORIGIN" tag -l | sort)
  if release "$@"; then
    fail "release $* was accepted, expected a refusal: $reason"
  fi
  grep -qF -- "$reason" "$WORK/err" ||
    fail "release $* refused with '$(cat "$WORK/err")', expected '$reason'"
  after=$(tg tag -l | sort)
  test "$before" = "$after" || fail "release $* refused but left a local tag behind"
  after_origin=$(git -C "$ORIGIN" tag -l | sort)
  test "$before_origin" = "$after_origin" || fail "release $* refused but a tag reached origin"
}

expected_message() {
  local version=$1 commit=$2 prefix=$3
  printf 'DM3 release %s\n\ncommit: %s\n' "$version" "$commit"
  for image in api front worker-mail worker-notification; do
    printf '%s: %s-%s@%s\n' "$image" "$prefix" "$image" "$(digest_of "$image")"
  done
}

# Accepted: an annotated tag at origin on the commit, holding exactly the message, signed by
# the test key over the namespace git, and no hook of the clone ran.
accepts() {
  local version=$1 commit=$2 prefix=$3
  shift 3
  if ! release "$@"; then
    fail "release $* was refused: $(cat "$WORK/err")"
  fi
  test "$(git -C "$ORIGIN" cat-file -t "refs/tags/$version")" = tag ||
    fail "$version at origin is not an annotated tag"
  test "$(git -C "$ORIGIN" rev-parse "refs/tags/$version^{commit}")" = "$commit" ||
    fail "$version at origin does not point at $commit"

  git -C "$ORIGIN" cat-file tag "$version" > "$WORK/tag"
  awk 'body && /^-----BEGIN SSH SIGNATURE-----$/ {exit} body {print} !body && /^$/ {body=1}' \
    "$WORK/tag" > "$WORK/body"
  expected_message "$version" "$commit" "$prefix" > "$WORK/expected"
  cmp -s "$WORK/body" "$WORK/expected" ||
    fail "the message of $version is not the five fields: $(cat "$WORK/body")"

  # The signed payload is the tag object up to its signature.
  awk '/^-----BEGIN SSH SIGNATURE-----$/ {exit} {print}' "$WORK/tag" > "$WORK/payload"
  sed -n '/^-----BEGIN SSH SIGNATURE-----$/,$p' "$WORK/tag" > "$WORK/signature"
  printf 'release-check namespaces="git" %s\n' "$(cat "$WORK/key.pub")" > "$WORK/allowed"
  ssh-keygen -Y verify -f "$WORK/allowed" -I release-check -n git -s "$WORK/signature" \
    < "$WORK/payload" > /dev/null 2>&1 ||
    fail "the signature of $version does not verify with the test key"

  test ! -e "$WORK/hook-ran" || fail "release $* ran a hook of the clone"
}

echo "the version must be vMAJOR.MINOR.PATCH, whole"
scenario "$B"
for version in 1.0.0 v01.0.0 v1.0 v1234567.0.0 v1.0.0-rc1 'v1.0.0;x'; do
  refuses "is not a release version" "$version"
done

echo "the version must be new and above the highest release tag"
refuses "is not above the highest release tag v1.2.0" v1.1.9
refuses "is already a tag" v1.2.0
tg tag v1.3.0 "$A"
refuses "is already a tag" v1.3.0
tg tag -d v1.3.0 > /dev/null
git -C "$ORIGIN" tag v1.4.0 "$A"
refuses "is already a tag" v1.4.0
git -C "$ORIGIN" tag -d v1.4.0 > /dev/null

# A writer can push any tag; only the signer's key makes a release, so only it sets the bar.
# The run listing that answers none is the first refusal after the checks of the version.
echo "the highest release tag is the highest one the signer's key signed under its own name"
scenario "$B"
echo none > "$WORK/scenario/run-list"
git -C "$SEED" tag -a -m "DM3 release v9.9.9" v9.9.9 "$B"
git -C "$SEED" push -q "$ORIGIN" v9.9.9
refuses "no run of dotnet.yml on dev" v1.3.0
git -C "$ORIGIN" tag -d v9.9.9 > /dev/null
git -C "$SEED" tag -d v9.9.9 > /dev/null
git -C "$ORIGIN" update-ref refs/tags/v9.9.9 "$(git -C "$ORIGIN" rev-parse refs/tags/v1.2.0)"
refuses "no run of dotnet.yml on dev" v1.3.0
git -C "$ORIGIN" tag -d v9.9.9 > /dev/null
git -C "$SEED" -c gpg.format=ssh -c user.signingkey="$WORK/key" tag -s -m "DM3 release v9.9.9" v9.9.9 "$B"
git -C "$SEED" push -q "$ORIGIN" v9.9.9
refuses "HEAD is not at v9.9.9" v1.3.0
git -C "$ORIGIN" tag -d v9.9.9 > /dev/null
git -C "$SEED" tag -d v9.9.9 > /dev/null

echo "the commit must be on origin/dev"
scenario "$X"
refuses "is not on origin/dev" v1.3.0 "$X"

echo "the commit's run on dev must be green with the publishers and the promotion"
scenario "$B"
echo none > "$WORK/scenario/run-list"
refuses "no run of dotnet.yml on dev" v1.3.0
scenario "$B"
echo "4242 completed failure" > "$WORK/scenario/run-list"
refuses "is completed/failure, not a success" v1.3.0
scenario "$B"
printf 'success\t%s\n' "publish (DM.Web.API, api)" "publish (DM.Workers.Mail, worker-mail)" \
  "publish (DM.Workers.NotificationDispatcher, worker-notification)" "publish-frontend" > "$WORK/scenario/jobs"
printf 'failure\tpromote-images\n' >> "$WORK/scenario/jobs"
refuses "promote-images did not succeed" v1.3.0
scenario "$B"
printf 'success\t%s\n' "publish (DM.Web.API, api)" "publish (DM.Workers.Mail, worker-mail)" \
  "publish-frontend" "promote-images" > "$WORK/scenario/jobs"
refuses "publish legs succeeded: 2 of 3" v1.3.0

echo "each digest artifact must be there and hold one reference"
scenario "$B"
touch "$WORK/scenario/missing-front"
refuses "cannot download the artifact digest-front" v1.3.0
scenario "$B"
printf 'ghcr.io/dm-am/dm-api@sha256:1234\n' > "$WORK/scenario/reference-api"
refuses "the artifact digest-api does not hold one reference" v1.3.0
scenario "$B"
printf 'ghcr.io/dm-am/dm-front@%s\n' "$(digest_of api)" > "$WORK/scenario/reference-api"
refuses "the artifact digest-api does not hold one reference" v1.3.0

echo "each attestation and each revision label must match"
scenario "$B"
touch "$WORK/scenario/attest-fail-worker-mail"
refuses "the attestation of worker-mail does not verify" v1.3.0
scenario "$B"
echo "$A" > "$WORK/scenario/label-front"
refuses "the revision label of front is $A" v1.3.0

echo "git must be set up to sign"
scenario "$B"
tg config --unset gpg.format
refuses "gpg.format is not ssh" v1.3.0
tg config gpg.format ssh
tg config --unset user.signingkey
refuses "user.signingkey is not set" v1.3.0
tg config user.signingkey "$WORK/key"

echo "the attestation check is skipped only for other image names"
scenario "$B"
export DM_RELEASE_UNATTESTED=1
refuses "DM_RELEASE_UNATTESTED is for the proof" v1.3.0
unset DM_RELEASE_UNATTESTED

echo "the worktree must be clean and at the last release tag"
scenario "$B"
echo dirty > "$CLONE/dirty.txt"
refuses "the worktree has changes" v1.3.0
rm "$CLONE/dirty.txt"
tg checkout -q --detach "$B"
refuses "HEAD is not at v1.2.0" v1.3.0
tg checkout -q --detach v1.2.0

echo "a release with the attestations verified"
scenario "$B"
accepts v1.3.0 "$B" ghcr.io/dm-am/dm v1.3.0
for image in api front worker-mail worker-notification; do
  reference="ghcr.io/dm-am/dm-$image@$(digest_of "$image")"
  grep -F "gh attestation verify oci://$reference " "$WORK/calls" > "$WORK/verify" ||
    fail "no attestation verify for $reference"
  for value in dm-am/dm3 .github/workflows/dotnet.yml refs/heads/dev "$B"; do
    grep -qF -- "$value" "$WORK/verify" ||
      fail "the attestation verify of $image does not name $value"
  done
done
grep -qF -- "-R dm-am/dm3" "$WORK/calls" || fail "the gh calls do not name the repository"

echo "a release for the proof: other image names, no gh at all"
tg checkout -q --detach v1.3.0
scenario "$B"
export DM_RELEASE_IMAGE_PREFIX=localhost:5000/dm DM_RELEASE_UNATTESTED=1
accepts v1.4.0 "$B" localhost:5000/dm v1.4.0
unset DM_RELEASE_IMAGE_PREFIX DM_RELEASE_UNATTESTED
if grep -q '^gh ' "$WORK/calls"; then
  fail "the proof release called gh: $(grep '^gh ' "$WORK/calls")"
fi

echo "release command: OK"
