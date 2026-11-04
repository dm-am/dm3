#!/usr/bin/env bash
# Fails when `dotnet list package --vulnerable` reports an advisory that is not
# in .github/vulnerability-allowlist.txt.
#
# Why a script instead of the raw command: `dotnet list package` exits 0 even when it prints
# advisories, so used directly it can only be a report, and so is it with `tee` or `|| true`.
set -euo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ALLOWLIST="$REPO_ROOT/.github/vulnerability-allowlist.txt"

# Restore first: `dotnet list package` reads the assets file and exits 1 without one. A
# developer machine passes on the leftover obj/, a clean CI checkout fails, and publish needs
# this job. Restoring here keeps the script correct wherever it is called from.
dotnet restore "$REPO_ROOT/DM.sln" --verbosity quiet

# A failed listing (the advisory feed out of reach) is printed before the exit: under set -e
# the assignment alone would stop the script with the reason still inside the variable.
status=0
report="$(dotnet list "$REPO_ROOT/DM.sln" package --vulnerable --include-transitive 2>&1)" || status=$?
echo "$report"
[ "$status" -eq 0 ] || exit "$status"

# One advisory per row, and only the first row of a package names it:
#
#   > Example.Package   1.2.3   1.2.3   Moderate   https://github.com/advisories/GHSA-aaaa-bbbb-cccc
#                                              High       https://github.com/advisories/GHSA-dddd-eeee-ffff
#
# NuGet prints every further advisory of a package with the package columns blank, so a
# filter on "> " sees only the first one. The package and its resolved version are carried
# from the "> " row down to the rows below it and dropped at anything that is not an
# advisory row. The sources list at the top is URLs as well and is skipped up to its blank
# line. A row with a URL and no package is reported under "?", which no allow-list line
# matches, and a URL without a GHSA id is reported by the URL: an advisory the script cannot
# name fails the gate.
#
# Output: "<package> <advisory> <version>", one line per distinct advisory.
findings="$(echo "$report" | awk '
  BEGIN { split("Low Moderate High Critical", words, " "); for (w in words) severity[words[w]] = 1 }
  { sub(/\r$/, "") }
  /^The following sources were used:/ { sources = 1; next }
  sources && NF > 0 { next }
  { sources = 0 }
  {
    url = 0
    for (i = 1; i <= NF; i++) if ($i ~ /^https?:\/\//) { url = i }
  }
  url == 0 { package = ""; version = ""; next }
  $1 == ">" {
    package = $2
    version = (url > 1 && ($(url - 1) in severity)) ? $(url - 2) : $(url - 1)
  }
  {
    advisory = $url
    if (match($url, /GHSA-[a-z0-9]+-[a-z0-9]+-[a-z0-9]+/)) advisory = substr($url, RSTART, RLENGTH)
    print (package == "" ? "?" : package), advisory, (version == "" ? "?" : version)
  }
' | sort -u)"

if [ -z "$findings" ]; then
  echo "OK: no vulnerable packages."
  exit 0
fi

# An allow-list line is "<PackageId> <GHSA-id>  # reason"; compared field by
# field, so a dot in a package id is a dot and not "any character".
is_allowed() {
  [ -f "$ALLOWLIST" ] || return 1
  awk -v package="$1" -v advisory="$2" '
    { sub(/\r$/, ""); sub(/#.*/, "") }
    $1 == package && $2 == advisory { found = 1; exit }
    END { exit found ? 0 : 1 }
  ' "$ALLOWLIST"
}

unreviewed=0
while read -r package advisory version; do
  if is_allowed "$package" "$advisory"; then
    continue
  fi

  echo "UNREVIEWED ADVISORY: $package $advisory ($version)"
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
