#!/usr/bin/env bash
# Fails when `dotnet list package --vulnerable` reports an advisory that is not
# in .github/vulnerability-allowlist.txt.
#
# Why a script instead of the raw command: `dotnet list package` exits 0 even
# when it prints advisories, so used directly it can only ever be a report.
# Piping it through `tee` (or ending the line with `|| true`) guarantees the
# same thing more subtly.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ALLOWLIST="$REPO_ROOT/.github/vulnerability-allowlist.txt"

# Restore first: `dotnet list package` reads the assets file and answers
# "No assets file was found ... Please run restore" with exit 1 when there is
# none. On a developer machine obj/ is left over from an ordinary build, so the
# script passed by hand; in CI the checkout is clean, so this job failed every
# run — and publish declares needs: dependency-scan, so no image was ever
# published. Restoring here rather than in the workflow keeps the script correct
# wherever it is called from.
dotnet restore "$REPO_ROOT/DM.sln" --verbosity quiet

report="$(dotnet list "$REPO_ROOT/DM.sln" package --vulnerable --include-transitive 2>&1)"
echo "$report"

# Advisory rows look like:   > PackageName   1.2.3   High   https://github.com/advisories/GHSA-xxxx-yyyy-zzzz
findings="$(echo "$report" | grep -E '^[[:space:]]*> ' || true)"

if [ -z "$findings" ]; then
  echo "OK: no vulnerable packages."
  exit 0
fi

unreviewed=0
while IFS= read -r line; do
  package="$(echo "$line" | awk '{print $2}')"
  advisory="$(echo "$line" | grep -oE 'GHSA-[a-z0-9-]+' || true)"
  [ -z "$advisory" ] && continue

  if [ -f "$ALLOWLIST" ] && grep -qE "^[[:space:]]*${package}[[:space:]]+${advisory}([[:space:]]|$)" "$ALLOWLIST"; then
    continue
  fi

  echo "UNREVIEWED ADVISORY: $package $advisory"
  unreviewed=1
done <<< "$findings"

if [ "$unreviewed" -ne 0 ]; then
  cat <<'EOF'

Fix the package version, or — if the upgrade needs a decision — add the
"<Package> <GHSA-id>  # who decided, why, what unblocks it" line to
.github/vulnerability-allowlist.txt. An accepted risk must be written down,
not silenced.
EOF
  exit 1
fi

echo "OK: every reported advisory is on the reviewed allow-list."
