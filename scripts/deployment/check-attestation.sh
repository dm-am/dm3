#!/usr/bin/env bash
# Fails when the attestation check that guards the moving names and the release tag stops
# refusing what it exists to refuse: a reference that is not a digest of one of ours, a
# commit or a ref of the wrong form, a verifier that says no. And when promote-images.sh
# moves a name before all four images passed, or accepts a verifier that would pass any ref
# or any commit. gh and docker are stubs on PATH that write down how they were called, so
# nothing here reaches GitHub or a registry and it costs about a second.
set -euo pipefail
export LC_ALL=C

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/common.sh"
# shellcheck source=/dev/null
. "$ROOT/scripts/lib/attestation.sh"
cd "$ROOT"

WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"' EXIT

mkdir -p "$WORK/bin"
export STUB_LOG="$WORK/calls"

# The stub refuses what a correct verifier refuses: the ref and the commit promote-images.sh
# asks about on purpose. STUB_GH picks a verifier that is wrong in one way.
cat > "$WORK/bin/gh" << 'STUB'
#!/usr/bin/env bash
printf 'gh %s\n' "$*" >> "$STUB_LOG"
case "${STUB_GH:-strict}" in
  refuse) echo "stub: no attestation" >&2; exit 1 ;;
  any-ref) [[ "$*" != *" --source-digest 0000000000000000000000000000000000000000 "* ]] ;;
  any-commit) [[ "$*" != *" --source-ref refs/heads/attestation-wrong-ref "* ]] ;;
  refuse-front)
    [[ "$*" != *"/dm-front@"* && "$*" != *"attestation-wrong-ref"* && "$*" != *" 0000000000000000000000000000000000000000 "* ]] ;;
  *) [[ "$*" != *"attestation-wrong-ref"* && "$*" != *" 0000000000000000000000000000000000000000 "* ]] ;;
esac
STUB

cat > "$WORK/bin/docker" << 'STUB'
#!/usr/bin/env bash
printf 'docker %s\n' "$*" >> "$STUB_LOG"
STUB

chmod +x "$WORK/bin/gh" "$WORK/bin/docker"
export PATH="$WORK/bin:$PATH"

# A real docker reached by mistake would move a real name, so the stubs have to be the ones found.
[ "$(command -v gh)" = "$WORK/bin/gh" ] || fail "the gh stub is not the gh on PATH"
[ "$(command -v docker)" = "$WORK/bin/docker" ] || fail "the docker stub is not the docker on PATH"

COMMIT="0123456789abcdef0123456789abcdef01234567"
DIGEST="sha256:$(printf 'ab%.0s' {1..32})"
API="ghcr.io/dm-am/dm-api@$DIGEST"

calls() {
  grep -c "^$1 " "$STUB_LOG" 2> /dev/null || true
}

reset() {
  : > "$STUB_LOG"
}

# The name of a case and what it hands attestation_verify; it must refuse without asking gh.
refused_before_gh() {
  local reason="$1"
  shift
  reset
  if attestation_verify "$@" 2> "$WORK/stderr"; then
    fail "attestation_verify accepts $reason"
  fi
  [ "$(calls gh)" = 0 ] || fail "attestation_verify asks gh about $reason"
  [ -s "$WORK/stderr" ] || fail "attestation_verify refuses $reason without saying why"
}

echo "attestation_verify asks gh about the repository, the workflow, dev and the commit"
reset
attestation_verify "$API" "$COMMIT" || fail "attestation_verify refuses a digest the verifier accepts"
expected="gh attestation verify oci://$API --repo dm-am/dm3 --signer-workflow dm-am/dm3/.github/workflows/dotnet.yml --source-ref refs/heads/dev --source-digest $COMMIT --deny-self-hosted-runners"
[ "$(cat "$STUB_LOG")" = "$expected" ] || fail "attestation_verify called $(cat "$STUB_LOG"), not $expected"

echo "attestation_verify takes the ref of a run on main"
reset
attestation_verify "$API" "$COMMIT" refs/heads/main || fail "attestation_verify refuses a ref it was given"
grep -q -- "--source-ref refs/heads/main " "$STUB_LOG" || fail "attestation_verify ignores the ref it was given"

echo "attestation_verify says no when gh does, and why"
reset
if STUB_GH=refuse attestation_verify "$API" "$COMMIT" 2> "$WORK/stderr"; then
  fail "attestation_verify accepts what gh refuses"
fi
grep -q "$API" "$WORK/stderr" || fail "attestation_verify refuses without naming the reference"

echo "attestation_verify refuses arguments of the wrong form before asking gh"
refused_before_gh "no arguments"
refused_before_gh "a reference alone" "$API"
refused_before_gh "a fourth argument" "$API" "$COMMIT" refs/heads/dev extra
refused_before_gh "a tag in place of a digest" "ghcr.io/dm-am/dm-api:dev" "$COMMIT"
refused_before_gh "an image of another owner" "ghcr.io/someone/dm-api@$DIGEST" "$COMMIT"
refused_before_gh "an image of another registry" "docker.io/dm-am/dm-api@$DIGEST" "$COMMIT"
refused_before_gh "a short digest" "ghcr.io/dm-am/dm-api@sha256:abab" "$COMMIT"
refused_before_gh "a digest in capitals" "ghcr.io/dm-am/dm-api@${DIGEST^^}" "$COMMIT"
refused_before_gh "a reference with a trailing line" "$API"$'\n' "$COMMIT"
refused_before_gh "a short commit" "$API" "0123456"
refused_before_gh "a commit in capitals" "$API" "${COMMIT^^}"
refused_before_gh "a ref that is not a branch" "$API" "$COMMIT" refs/tags/v1.0.0
refused_before_gh "an empty ref" "$API" "$COMMIT" ""

# The four digest files of a run, as the publishers upload them.
digests() {
  local dir="$WORK/digests" image
  rm -rf "$dir"
  for image in api front worker-mail worker-notification; do
    mkdir -p "$dir/digest-$image"
    printf 'ghcr.io/dm-am/dm-%s@%s\n' "$image" "$DIGEST" > "$dir/digest-$image/reference"
  done
}

promote() {
  reset
  bash scripts/deployment/promote-images.sh "$WORK/digests" "$@" > /dev/null 2> "$WORK/stderr"
}

echo "promote-images.sh moves dev to the four digests after twelve checks"
digests
promote refs/heads/dev "$COMMIT" || fail "promote-images.sh refuses a run whose checks all pass: $(cat "$WORK/stderr")"
[ "$(calls gh)" = 12 ] || fail "promote-images.sh asked gh $(calls gh) times, not three times for each of four images"
[ "$(calls docker)" = 4 ] || fail "promote-images.sh moved $(calls docker) names, not dev on four images"
grep -q "^docker buildx imagetools create --tag ghcr.io/dm-am/dm-front:dev ghcr.io/dm-am/dm-front@$DIGEST$" "$STUB_LOG" ||
  fail "promote-images.sh does not copy dev from the digest the run built"
last_gh=$(grep -n "^gh " "$STUB_LOG" | tail -1 | cut -d: -f1)
first_docker=$(grep -n "^docker " "$STUB_LOG" | head -1 | cut -d: -f1)
[ "$last_gh" -lt "$first_docker" ] || fail "promote-images.sh moves a name before every image is checked"

echo "promote-images.sh moves latest and main on main"
promote refs/heads/main "$COMMIT" || fail "promote-images.sh refuses a run on main: $(cat "$WORK/stderr")"
[ "$(calls docker)" = 8 ] || fail "promote-images.sh moved $(calls docker) names on main, not latest and main on four images"
grep -q -- "--source-ref refs/heads/main " "$STUB_LOG" || fail "promote-images.sh checks a run on main against another ref"

# Each case: the reason, then what must hold true for promote-images.sh to have to refuse.
refused_without_moving() {
  local reason="$1"
  shift
  if promote "$@"; then
    fail "promote-images.sh accepts $reason"
  fi
  [ "$(calls docker)" = 0 ] || fail "promote-images.sh moves a name with $reason"
  [ -s "$WORK/stderr" ] || fail "promote-images.sh refuses $reason without saying why"
}

echo "promote-images.sh refuses before moving anything"
digests
refused_without_moving "a ref that publishes no name" refs/heads/feature "$COMMIT"
refused_without_moving "no commit" refs/heads/dev
STUB_GH=refuse-front refused_without_moving "an image whose attestation fails" refs/heads/dev "$COMMIT"
STUB_GH=any-ref refused_without_moving "a verifier that passes any ref" refs/heads/dev "$COMMIT"
grep -q "accepts any ref" "$WORK/stderr" || fail "promote-images.sh does not say the verifier passes any ref"
STUB_GH=any-commit refused_without_moving "a verifier that passes any commit" refs/heads/dev "$COMMIT"
grep -q "accepts any commit" "$WORK/stderr" || fail "promote-images.sh does not say the verifier passes any commit"

rm "$WORK/digests/digest-worker-mail/reference"
refused_without_moving "a missing digest" refs/heads/dev "$COMMIT"

digests
printf 'ghcr.io/dm-am/dm-front@%s\n' "$DIGEST" > "$WORK/digests/digest-api/reference"
refused_without_moving "the digest of another image" refs/heads/dev "$COMMIT"

digests
printf 'ghcr.io/dm-am/dm-api@%s\nghcr.io/dm-am/dm-api@%s\n' "$DIGEST" "$DIGEST" > "$WORK/digests/digest-api/reference"
refused_without_moving "a digest file of two lines" refs/heads/dev "$COMMIT"

echo "attestation check and promotion: all cases pass"
