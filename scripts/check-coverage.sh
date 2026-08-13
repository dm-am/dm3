#!/usr/bin/env bash
# Fails when the merged coverage of the solution drops below the ratchet below.
#
# Why a script and not a switch on `dotnet test`: the run writes one cobertura
# file per test project, and each of them measures only the assemblies that
# project happened to load. A threshold applied to any one of them is a threshold
# on a fraction of the code, so the reports have to be merged first - which is
# the whole of what ReportGenerator does here.
#
# Why one pair of numbers and not one per assembly: a per-assembly floor is a
# second set of numbers, each measured, written down and raised by hand, plus a
# list of assemblies kept by hand - and an assembly missing from that list slips
# out from under the floor silently, which makes the gate worse than one honest
# number. More numbers also mean more occasions for a red build under time
# pressure to be fixed by lowering one, the single use this must never be put to.
# A drop inside one assembly is caught reading the diff. The frontend thresholds
# are global for the same reason, though vitest offers perFile.
#
# The results directory is expected to hold one run. Merging two runs of
# different configurations adds their generated sources to the denominator and
# the number drifts down for no reason; CI checks out clean, and by hand the
# directory should be passed in fresh:
#   dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
set -euo pipefail

# Ratchet, not a floor to duck under - the same rule as the frontend thresholds
# in src/DM.Web.Client/vite.config.ts: raise after a gain, never lower after a
# miss. Measured on the merged report of a full run of the solution with every
# test project green: 72.3% lines, 52.5% branches. Those are the two numbers this
# script prints, read off the line-rate and branch-rate attributes of the merged
# Cobertura report, which are the values the comparison below is made against.
# The TextSummary of that same run reads a tenth lower on both, 72.2% and 52.4%,
# because the awk below rounds the attribute and the summary does not. Taking the
# pair from one source while comparing against the other spends a tenth of the
# margin before anything is measured. Lines are the load-bearing number; branches
# read low because files with no tests contribute few branch counters.
#
# The gap the two numbers below leave is a little over two points, and it is
# deliberately not smaller: a ratchet set a hair under the last measurement turns
# any difference between the CI runner and a developer's machine into a red
# build, and a red build under time pressure gets fixed by lowering the number -
# the one thing this must never be used for. What too wide a gap costs is what
# the pair these replace cost. 64 and 43 trailed that measurement by more than
# eight points and more than nine, and the same run with DM.Domain.Game.Tests
# left out of the merge - one project, 637 tests - still measured 69.5% lines and
# 46.0% branches and still cleared both of them. It clears neither of these.
MIN_LINE_RATE=70
MIN_BRANCH_RATE=50

REPORTGENERATOR_VERSION=5.3.11

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
RESULTS="${1:-$REPO_ROOT/TestResults}"

# The same path, spelled so a native program can resolve it. Under Git Bash every
# path built above is an MSYS one (/d/Code Projects/dm3/...) and ReportGenerator
# is a Windows binary, so MSYS rewrites such an argument on its way out - unless
# the argument carries a wildcard, and then it hands it over untouched. That is
# the shape of the -reports argument below, which therefore reached the tool as
# /d/... and matched nothing. Called with no argument - the form the CI step uses,
# and the only form that needs no knowledge of the layout - the tool answered "No
# report files specified." and the script exited before reading a single rate, so
# the one gate on backend coverage could not be run on Windows at all. A space in
# the path is not the cause and moving the checkout does not help: MSYS converts
# paths with spaces. On Linux cygpath does not exist and the path passes through.
native() {
  if command -v cygpath > /dev/null 2>&1; then
    cygpath -m "$1"
  else
    printf '%s' "$1"
  fi
}

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
  "-reports:$(native "$RESULTS")/**/coverage.cobertura.xml" \
  "-targetdir:$(native "$MERGED")" \
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
