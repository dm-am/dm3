#!/usr/bin/env bash
# Shared by promote-images.sh and the release command; sourced, never run. One spelling of the
# check, so that what moves the names on dev and what signs a release cannot come to accept
# different things.

# Fixed rather than read from the run: a fork's attestation names the fork, and nothing it
# built is ours to move or to sign.
ATTESTATION_REPOSITORY="dm-am/dm3"
ATTESTATION_WORKFLOW="$ATTESTATION_REPOSITORY/.github/workflows/dotnet.yml"

# attestation_verify <reference> <commit> [<ref>]
#
# Succeeds when the build provenance attestation of <reference> was signed by the publishing
# workflow of this repository, for <ref> (refs/heads/dev unless given) at <commit>, on a
# GitHub-hosted runner. Fails with the reason on stderr otherwise. The ref is an argument
# because promote-images.sh checks a run on main against main and asks about a wrong ref on
# purpose; the release command signs only what dev built and leaves it out.
attestation_verify() {
  local LC_ALL=C

  if [ "$#" -lt 2 ] || [ "$#" -gt 3 ]; then
    echo "attestation_verify: expected <reference> <commit> [<ref>], got $# arguments" >&2
    return 1
  fi

  local reference="$1" commit="$2" ref="${3-refs/heads/dev}"

  # The digest, never a tag: a tag can be moved between this check and the pull it guards.
  if ! [[ "$reference" =~ ^ghcr\.io/dm-am/dm-[a-z]+(-[a-z]+)*@sha256:[0-9a-f]{64}$ ]]; then
    echo "attestation_verify: '$reference' is not ghcr.io/dm-am/dm-<image>@sha256:<64 hex>" >&2
    return 1
  fi

  # The full commit: gh compares the source digest as a string, and a short one matches nothing.
  if ! [[ "$commit" =~ ^[0-9a-f]{40}$ ]]; then
    echo "attestation_verify: '$commit' is not a commit of 40 lowercase hex digits" >&2
    return 1
  fi

  if ! [[ "$ref" =~ ^refs/heads/[^[:space:]]+$ ]]; then
    echo "attestation_verify: '$ref' is not a branch ref" >&2
    return 1
  fi

  if ! gh attestation verify "oci://$reference" \
    --repo "$ATTESTATION_REPOSITORY" \
    --signer-workflow "$ATTESTATION_WORKFLOW" \
    --source-ref "$ref" \
    --source-digest "$commit" \
    --deny-self-hosted-runners; then
    echo "attestation_verify: $reference has no attestation of $ATTESTATION_WORKFLOW for $ref at $commit" >&2
    return 1
  fi
}
