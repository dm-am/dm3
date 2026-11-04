#!/usr/bin/env bash
# Fails when the merged coverage of the solution drops below the ratchet below.
#
# A script and not a switch on `dotnet test`: each test project's cobertura file measures
# only the assemblies it loaded, so the reports are merged first (ReportGenerator).
# One pair of numbers, not one per assembly: a per-assembly floor is a list kept by hand,
# an assembly missing from it slips out silently, and more numbers are more occasions to
# fix a red build by lowering one.
#
# The results directory has to hold one run: a report merged out of two describes neither,
# and the span check below refuses it. CI checks out clean; by hand pass it in fresh:
#   dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults
set -euo pipefail

# Ratchet, as the frontend thresholds in src/DM.Web.Client/vite.config.ts: raise after a
# gain, never lower after a miss. Lines are the load-bearing number; branches read low
# because files with no tests contribute few branch counters. The gap below the last
# measurement covers the difference between the CI runner and a developer machine and
# nothing more: wider, and a third of the tests could go with the gate still green. The
# rates are read off the line-rate and branch-rate attributes of the merged Cobertura
# report, not off the TextSummary, which rounds separately and reads a tenth lower.
MIN_LINE_RATE=75.5
MIN_BRANCH_RATE=59.2

REPORTGENERATOR_VERSION=5.3.11

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
# shellcheck source=/dev/null
. "$REPO_ROOT/scripts/lib/common.sh"
RESULTS="${1:-$REPO_ROOT/TestResults}"

if [ -z "$(find "$RESULTS" -name coverage.cobertura.xml -print -quit 2>/dev/null)" ]; then
  echo "ERROR: no coverage report under $RESULTS." >&2
  echo 'Collect it first: dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults' >&2
  exit 1
fi

# One run, all of it. A GUID directory is a test PROJECT, not a run: one `dotnet test` of
# the solution drops one per project, and keeping only the newest measures one project and
# reports it as the solution. What must not be merged is two runs, told apart by span: the
# reports of one run land within its duration (a build job may not exceed forty minutes),
# so a spread of an hour means older reports lie here and the answer would be about neither.
TIMES="$(find "$RESULTS" -name coverage.cobertura.xml -exec stat -c %Y {} + 2>/dev/null | sort -n)"
OLDEST="$(printf '%s\n' "$TIMES" | head -1)"
LATEST="$(printf '%s\n' "$TIMES" | tail -1)"
if [ -n "$OLDEST" ] && [ -n "$LATEST" ] && [ "$((LATEST - OLDEST))" -gt 3600 ]; then
  echo "ERROR: the reports under $RESULTS span more than an hour, so they come from more than one run." >&2
  echo 'Merging them measures no version in particular. Collect once into an empty directory:' >&2
  echo '  rm -rf TestResults && dotnet test --collect:"XPlat Code Coverage" --results-directory TestResults' >&2
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

# The tests are out of the denominator: DM.Testing and the *.Tests assemblies. So is what a
# generator wrote (the Riok.Mapperly mappers, all under obj/): a test of a mapper body
# nobody typed proves the generator works. The file filter also silences the fifty lines of
# "does not exist (any more)" that cleaning obj/ left in the report.
"$TOOLS/reportgenerator" \
  "-reports:$(host "$RESULTS")/**/coverage.cobertura.xml" \
  "-targetdir:$(host "$MERGED")" \
  "-reporttypes:Cobertura;TextSummary" \
  "-assemblyfilters:-*.Tests;-DM.Testing" \
  "-filefilters:-*.g.cs" \
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
