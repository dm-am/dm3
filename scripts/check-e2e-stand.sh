#!/usr/bin/env bash
# Fails, and names the command that fixes it, when the local stack is not one
# the end-to-end suite can run against. Brings nothing up itself: starting,
# rebuilding or recreating the stack is the developer's call, and a push that
# quietly restarted the API under an open tab would be a worse surprise than a
# refusal.
#
# Run by the pre-push hook and by scripts/gates.sh when the suite is asked for (DM_GATES_FULL), well before the suite:
# the suite is the last and the dearest gate, and a stack that is not there is
# a thing to learn in a second, not after twenty minutes of the others.
#
# What the suite needs of the stack, and which of it is checked here:
#   1. The API answers its liveness route. Checked.
#   2. Rate limiting is off. Checked. One page load is a dozen and more API
#      requests against a budget of a hundred per address per minute, so under
#      the limits the suite fails on 429s that read exactly like stale
#      selectors. CI switches them off with an overlay in the e2e job of
#      .github/workflows/dotnet.yml; "dm.ps1 e2e" and "dm.sh e2e" recreate the
#      API with the same switch, and this reads it back off the container.
#   3. The database is seeded. Not checked here: global setup signs in as the
#      seeded accounts before the first spec and says so in its own words.
#   4. The API is built from the server being pushed. Checked. The image is
#      built once, by "e2e", and nothing rebuilds it after that, so a server
#      change committed later would be pushed with the suite measuring the
#      server before it, and the verdict would be wrong in either direction.
#      "e2e" writes the commit it built on into the container, and the server
#      part of the tree is compared between that commit and HEAD. What was
#      uncommitted at build time is not told apart: the check is about
#      commits that landed after the build.
set -euo pipefail

API="http://localhost:5000"
CONTAINER="dm-api"
SWITCH="DM_RateLimiting__Enabled=false"
SOURCE="E2E_SOURCE_COMMIT"
# What the API image is built from, as docker/app.Dockerfile copies it.
SERVER=(DM.sln Directory.Build.props Directory.Packages.props .editorconfig docker/app.Dockerfile src ":(exclude)src/DM.Web.Client")

refuse() {
  echo "ERROR: $1" >&2
  echo "From the repository root:" >&2
  shift
  for step in "$@"; do
    echo "  Windows:      powershell -File .\\scripts\\dm.ps1 $step" >&2
  done
  for step in "$@"; do
    echo "  Linux, macOS: ./scripts/dm.sh $step" >&2
  done
  echo "A push that has to go without the suite, knowingly: git push --no-verify" >&2
  exit 1
}

# The liveness route, the one the container healthcheck and both management
# scripts ask.
if ! curl -sf --max-time 5 "$API/_health" > /dev/null; then
  refuse "the API does not answer at $API/_health, so there is no stack for the suite." start seed e2e
fi

if ! command -v docker > /dev/null 2>&1; then
  refuse "docker is not on PATH, so whether rate limiting is off cannot be read off $CONTAINER." e2e
fi

if ! environment="$(docker inspect "$CONTAINER" --format '{{range .Config.Env}}{{println .}}{{end}}' 2> /dev/null)"; then
  refuse "no $CONTAINER container, so the API that answers is not the one the stack runs." start seed e2e
fi

if ! printf '%s\n' "$environment" | grep -qx "$SWITCH"; then
  refuse "$CONTAINER runs with rate limiting on, and the suite would fail on 429s rather than on defects." e2e
fi

built="$(printf '%s\n' "$environment" | sed -n "s/^$SOURCE=//p")"
root="$(git rev-parse --show-toplevel)"
if [ -z "$built" ] || ! git -C "$root" cat-file -e "$built^{commit}" 2> /dev/null; then
  refuse "$CONTAINER does not name the commit it was built on, so it may be an older server than the one being pushed." e2e
fi

if ! git -C "$root" diff --quiet "$built" HEAD -- "${SERVER[@]}"; then
  refuse "$CONTAINER was built on ${built:0:8}, and commits since then change the server, so the suite would measure the one before them." e2e
fi

echo "OK: the API answers, runs with rate limiting off and is built from the server at HEAD."
