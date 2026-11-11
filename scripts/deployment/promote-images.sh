#!/usr/bin/env bash
# Moves the names a deployment pulls (dev on dev, latest and main on main) to the digests this
# run's publishers built, once each of the four is shown to carry this run's attestation. A
# script rather than a step of the workflow so that shellcheck reads it and
# check-attestation.sh runs it against stubs.
#
# Usage: promote-images.sh <digest-dir> <ref> <commit>
#   <digest-dir> holds digest-<image>/reference, one line each, as the publishers upload them.
#   gh reads GH_TOKEN; docker has to be logged in to the registry.
set -euo pipefail
export LC_ALL=C

# shellcheck source=/dev/null
. "$(dirname "${BASH_SOURCE[0]}")/../lib/attestation.sh"

IMAGES=(api worker-mail worker-notification front)

# Neither a ref nor a commit a run can have, so a verifier that accepts them accepts anything.
WRONG_REF="refs/heads/attestation-wrong-ref"
WRONG_COMMIT="0000000000000000000000000000000000000000"

die() {
  echo "promote-images: $1" >&2
  exit 1
}

[ "$#" -eq 3 ] || die "usage: promote-images.sh <digest-dir> <ref> <commit>"
DIR="$1"
REF="$2"
COMMIT="$3"

case "$REF" in
  refs/heads/main) NAMES=(latest main) ;;
  refs/heads/dev) NAMES=(dev) ;;
  *) die "$REF publishes no moving name" ;;
esac

# The one line of digest-<image>/reference, refused unless it is a digest of that very image:
# a file swapped between legs would move one image's name onto another's bytes.
read_reference() {
  local image="$1" file="$DIR/digest-$1/reference" lines
  [ -f "$file" ] || die "$file is missing; the publisher of $image uploaded no digest"
  mapfile -t lines < "$file"
  [ "${#lines[@]}" -eq 1 ] || die "$file holds ${#lines[@]} lines, not one"
  [[ "${lines[0]}" =~ ^ghcr\.io/dm-am/dm-${image}@sha256:[0-9a-f]{64}$ ]] ||
    die "$file holds '${lines[0]}', not a digest of dm-$image"
  printf '%s\n' "${lines[0]}"
}

declare -A REFERENCES
for image in "${IMAGES[@]}"; do
  reference="$(read_reference "$image")"
  REFERENCES[$image]="$reference"

  echo "$image: $reference must verify for $REF at $COMMIT"
  attestation_verify "$reference" "$COMMIT" "$REF" || die "$reference does not verify for this run"

  # Both refusals run on every push, so a check that stopped checking is found by the next
  # one, not by the release it was meant to stop. gh's own refusal below is expected.
  echo "$image: the same digest must be refused for $WRONG_REF"
  if attestation_verify "$reference" "$COMMIT" "$WRONG_REF"; then
    die "the check accepts any ref: $reference verified for $WRONG_REF"
  fi

  echo "$image: the same digest must be refused at commit $WRONG_COMMIT"
  if attestation_verify "$reference" "$WRONG_COMMIT" "$REF"; then
    die "the check accepts any commit: $reference verified at $WRONG_COMMIT"
  fi
done

# Only after all four passed. A broken verification (an outage, a changed gh flag) holds the
# moving names on the previous commit on purpose: a name that stops moving is noticed and
# costs a deployment, a name moved onto unverified bytes is pulled by every server.
for image in "${IMAGES[@]}"; do
  reference="${REFERENCES[$image]}"
  for name in "${NAMES[@]}"; do
    docker buildx imagetools create --tag "${reference%@*}:$name" "$reference"
  done
done
