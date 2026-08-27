#!/usr/bin/env bash
# Fails when shellcheck reports anything about a shell script of this
# repository.
#
# Every *.sh git knows about plus the pre-push hook, which has no suffix and is
# the one script whose failure is silent by construction: it runs on a push, not
# in CI. The list is taken from git rather than kept by hand - the backup jobs
# run at two in the morning into a log nobody reads, so a typo in one of them is
# found by a failed restore.
#
# Why a script and not the command in the workflow: it used to live only in the
# compose-topology job, and a gate that exists in one place exists for one
# machine. shellcheck needs nothing but docker, so there was never a reason for
# it to be reachable only after a push - and it reddened a build for a `for`
# loop over a single element that any local run would have named in a second.
# The workflow and scripts/gates.sh both call this now, so there is one command
# and one version of it.
#
# In a container so that the version matches the workflow's exactly: shellcheck
# adds checks between releases, and a newer one on a developer machine would
# report findings CI does not have, while an older one passes what CI fails.
set -euo pipefail

IMAGE="koalaman/shellcheck:v0.10.0"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# Git Bash rewrites anything that looks like a Unix path in an argument before
# the Windows docker client sees it, which turns -w /mnt into a directory under
# the Git installation and stops the container. The variable switches that off
# for this command, and cygpath hands docker the native path it wants; on Linux
# and macOS neither exists and the value passes through unchanged.
MOUNT="$ROOT"
if command -v cygpath > /dev/null 2>&1; then
  MOUNT="$(cygpath -w "$ROOT")"
fi
export MSYS_NO_PATHCONV=1

# Through xargs rather than a command substitution: a path with a space in it
# would otherwise arrive as two arguments, and shellcheck would report two
# files it cannot open instead of checking the one that exists.
{
  # --others as well as the tracked files: a script written five minutes ago is
  # the one most likely to be wrong, and taking the index alone would leave its
  # first shellcheck to CI - which is the whole shape of defect this gate moved
  # out of CI to avoid. A checkout has nothing untracked, so the set is the same
  # there. --exclude-standard keeps everything .gitignore already keeps out.
  git ls-files -z --cached --others --exclude-standard '*.sh'
  printf '%s\0' scripts/hooks/pre-push
} | xargs -0 docker run --rm -v "$MOUNT:/mnt" -w /mnt "$IMAGE"
