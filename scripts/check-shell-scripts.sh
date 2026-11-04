#!/usr/bin/env bash
# Fails when shellcheck reports anything about a shell script of this repository: every *.sh
# git knows about plus the pre-push hook, which has no suffix and is the one script whose
# failure is silent by construction, since it runs on a push and not in CI. The list comes
# from git, not by hand: the backup jobs run at two in the morning into a log nobody reads.
#
# In a container, so that the version matches the workflow's exactly: new releases add
# checks, and a newer one on a developer machine would report findings CI does not have.
set -euo pipefail

IMAGE="koalaman/shellcheck:v0.10.0"

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT"

# MSYS_NO_PATHCONV: Git Bash would otherwise rewrite -w /mnt into a directory under the Git
# installation and stop the container; cygpath hands docker the native path (on Linux and
# macOS neither exists).
MOUNT="$ROOT"
if command -v cygpath > /dev/null 2>&1; then
  MOUNT="$(cygpath -w "$ROOT")"
fi
export MSYS_NO_PATHCONV=1

# Through xargs, not a command substitution: a path with a space would arrive as two arguments.
{
  # --others as well as the tracked files: a script written five minutes ago is the one most
  # likely to be wrong, and its first check should not be CI's. --exclude-standard keeps out
  # what .gitignore keeps out.
  git ls-files -z --cached --others --exclude-standard '*.sh'
  printf '%s\0' scripts/hooks/pre-push
} | xargs -0 docker run --rm -v "$MOUNT:/mnt" -w /mnt "$IMAGE"
