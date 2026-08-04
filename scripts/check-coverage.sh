#!/usr/bin/env bash
# Fails when the merged coverage of the solution drops below the ratchet below.
#
# Why a script and not a switch on `dotnet test`: the run writes one cobertura
# file per test project, and each of them measures only the assemblies that
# project happened to load. A threshold applied to any one of them is a threshold
# on a fraction of the code, so the reports have to be merged first - which is
# the whole of what ReportGenerator does here.
#
# The results directory is expected to hold one run. Merging two runs of
# different configurations adds their generated sources to the denominator and
# the number drifts down for no reason; CI checks out clean, and by hand the
# directory should be passed in fresh:
#   dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
set -euo pipefail

# Ratchet, not a floor to duck under - the same rule as the frontend thresholds
# in src/DM.Web.Client/vite.config.ts: raise after a gain, never lower after a
# miss. Measured on the merged report of a full run: 65.3% lines, 44.8%
# branches. Lines are the load-bearing number; branches read low because files
# with no tests contribute few branch counters.
MIN_LINE_RATE=64
MIN_BRANCH_RATE=43

REPORTGENERATOR_VERSION=5.3.11

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESULTS="${1:-$REPO_ROOT/TestResults}"

if [ -z "$(find "$RESULTS" -name coverage.cobertura.xml -print -quit 2>/dev/null)" ]; then
  echo "ERROR: no coverage report under $RESULTS." >&2
  echo 'Collect it first: dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults' >&2
  exit 1
fi

# Cached by version: downloaded once per machine rather than once per run.
TOOLS="${DM_COVERAGE_TOOLS:-${TMPDIR:-/tmp}/dm3-reportgenerator-$REPORTGENERATOR_VERSION}"
if [ ! -x "$TOOLS/reportgenerator" ]; then
  dotnet tool install dotnet-reportgenerator-globaltool \
    --tool-path "$TOOLS" --version "$REPORTGENERATOR_VERSION" > /dev/null
fi

MERGED="$(mktemp -d)"
trap 'rm -rf "$MERGED"' EXIT

# The tests are out of the denominator: DM.Testing is the shared harness and the
# *.Tests assemblies are the tests themselves. Covering the tests with the tests
# says nothing about the code they were written for.
"$TOOLS/reportgenerator" \
  "-reports:$RESULTS/**/coverage.cobertura.xml" \
  "-targetdir:$MERGED" \
  "-reporttypes:Cobertura;TextSummary" \
  "-assemblyfilters:-*.Tests;-DM.Testing" \
  -verbosity:Error

rate() {
  sed -n 's/.*<coverage[^>]* '"$1"'="\([0-9.]*\)".*/\1/p' "$MERGED/Cobertura.xml" | head -1
}

LINE_RATE="$(rate line-rate)"
BRANCH_RATE="$(rate branch-rate)"

if [ -z "$LINE_RATE" ] || [ -z "$BRANCH_RATE" ]; then
  echo "ERROR: could not read the rates out of the merged report." >&2
  exit 1
fi

LINE_PCT="$(awk -v r="$LINE_RATE" 'BEGIN { printf "%.1f", r * 100 }')"
BRANCH_PCT="$(awk -v r="$BRANCH_RATE" 'BEGIN { printf "%.1f", r * 100 }')"

echo "Line coverage:   $LINE_PCT% (ratchet $MIN_LINE_RATE%)"
echo "Branch coverage: $BRANCH_PCT% (ratchet $MIN_BRANCH_RATE%)"

below() { awk -v value="$1" -v minimum="$2" 'BEGIN { exit (value < minimum) ? 0 : 1 }'; }

failed=0
if below "$LINE_PCT" "$MIN_LINE_RATE"; then
  echo "FAIL: line coverage $LINE_PCT% is under the $MIN_LINE_RATE% ratchet." >&2
  failed=1
fi
if below "$BRANCH_PCT" "$MIN_BRANCH_RATE"; then
  echo "FAIL: branch coverage $BRANCH_PCT% is under the $MIN_BRANCH_RATE% ratchet." >&2
  failed=1
fi

if [ "$failed" -ne 0 ]; then
  cat <<'EOF'

Write the tests, or - when the drop is a deliberate deletion - say so in review
and raise the numbers back afterwards. Lowering the ratchet to make CI pass is
the one thing it must never be used for.
EOF
  exit 1
fi

echo "OK: coverage holds the ratchet."
