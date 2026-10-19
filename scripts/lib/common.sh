#!/usr/bin/env bash
# Shared by the check scripts; sourced, never run.

# A path spelled the way a native program resolves it. Under Git Bash, MSYS rewrites path
# arguments for Windows binaries (docker, ReportGenerator, openssl), but not one that
# carries a wildcard or sits inside a file. On Linux and macOS cygpath does not exist and
# the value passes through.
host() {
  if command -v cygpath > /dev/null 2>&1; then
    cygpath -m "$1"
  else
    printf '%s' "$1"
  fi
}

fail() {
  echo "ERROR: $1" >&2
  exit 1
}
