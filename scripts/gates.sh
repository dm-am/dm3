#!/bin/sh
#
# Runs the same gates as the pre-push hook, in the same order, ahead of the push: a red
# gate is cheaper to learn about while the change is hot, and only the failed step can be
# repeated. The hook stays the last barrier.
#
# The list lives in two files because the hook must work in a clone without this script
# and this script must work on an untouched tree. DM.Architecture.Tests compares their
# commands with each other, with all steps of both workflows, and checks which gates run on
# a branch; a step missing here needs a record with a reason in the named register.
#
#   bash scripts/gates.sh                   the steps this change needs
#   bash scripts/gates.sh Фронтенд          one step, by substring of its name (case sensitive)
#   DM_GATES_FULL=1 bash scripts/gates.sh   the full set, e2e included
#
# A green step prints one line. A red one prints its whole output after that line and stops
# the run with its own exit status.
#
# Without an argument only the steps the change needs run: scripts/ci-paths.json maps the
# working tree against the common base with origin/dev (untracked files included, since this
# runs before the commit) to jobs, with the same script as the hook and the changes job of
# the workflow. A step named by an argument always runs. The branch set is the lock file, the
# code comparison test, the client gates in src/gates, lint, types, format and build of the
# solution, and the architecture tests; client unit tests, solution tests, coverage, client
# build, vulnerable packages, e2e and the docker steps are CI's on a PR.
#
# The check scripts under scripts/ and scripts/deployment are one command each, called by the
# workflow, this script and the hook, so a gate has one version of itself everywhere.
#
# A full run takes the whole machine: a second heavy process beside it (a parallel dotnet
# test, a running workflow, a capture) fails unit tests by timeout and integration tests on
# the shared database, and the failure looks real.

set -e

ROOT=$(git rev-parse --show-toplevel)
CLIENT="$ROOT/src/DM.Web.Client"
ONLY=${1:-}

say() { printf '\n\033[36m==> %s\033[0m\n' "$1"; }

# A step's output waits here and is shown only when the step fails.
GATES_TMP=$(mktemp -d)
trap 'rm -rf "$GATES_TMP"' EXIT
# An interrupted step still shows what it printed so far.
trap 'cat "$GATES_TMP/step.log" 2>/dev/null; exit 130' INT TERM

quietly() {
  name=$1
  shift
  printf '\033[36m==> %s\033[0m' "$name"
  if "$@" >"$GATES_TMP/step.log" 2>&1; then
    printf ' ok\n'
  else
    code=$?
    printf ' упал, код %s\n' "$code"
    cat "$GATES_TMP/step.log"
    exit "$code"
  fi
}

# The map's verdict: a "<job>=true|false" line per job, kept apart from the report on stderr.
choose() { node "$ROOT/scripts/ci-paths.mjs" --worktree origin/dev >"$GATES_TMP/selection"; }
SELECTION=""
if [ -z "$ONLY" ]; then
  quietly "Пути: какие гейты нужны изменениям" choose
  SELECTION=$(cat "$GATES_TMP/selection")
fi

# Whether any job of the list is wanted; "always" is a step outside the map.
wanted() {
  for job in $1; do
    [ "$job" = always ] && return 0
    printf '%s\n' "$SELECTION" | grep -qx "$job=true" && return 0
  done
  return 1
}

# A step runs when the filter is part of its name, and without a filter when the map calls
# one of its jobs.
step() {
  jobs=$1
  name=$2
  shift 2
  if [ -n "$ONLY" ]; then
    case "$name" in
      *"$ONLY"*) ;;
      *) return 0 ;;
    esac
  elif ! wanted "$jobs"; then
    return 0
  fi
  quietly "$name" "$@"
}

# A function a step calls chains its commands: quietly runs it as the condition of an if,
# where set -e is off.
hook_tests() {
  node "$ROOT/.claude/hooks/block-dangerous-git.test.js" &&
    node "$ROOT/.claude/hooks/block-new-migrations.test.js" &&
    node "$ROOT/.claude/hooks/lint-edited-file.test.js" &&
    node "$ROOT/.claude/hooks/block-in-subagents.test.js" &&
    node "$ROOT/.claude/hooks/lint-agent-files.test.js" &&
    node "$ROOT/.claude/hooks/lint-size-hint.test.js"
}

in_client() { (cd "$CLIENT" && "$@"); }
in_root() { (cd "$ROOT" && "$@"); }

# Solution tests and coverage in one run, as CI gets them: the cobertura reports go to
# --results-directory and the next step reads them. The directory is emptied first, since a
# report merged from several states of the code describes none of them.
backend_tests() {
  rm -rf "$ROOT/TestResults" &&
    dotnet test "$ROOT/DM.sln" --no-build -c Release --nologo --results-directory "$ROOT/TestResults" --collect:"XPlat Code Coverage"
}

# Gates that take tens of minutes or need docker and a network run only on request, as in the
# hook: CI runs them on a PR and is the merge gate, and an e2e run would face a stand rebuilt
# for every branch. A request is a non-empty DM_GATES_FULL or a step name argument.
full_requested() { [ -n "${DM_GATES_FULL:-}" ] || [ -n "$ONLY" ]; }

step always "Хуки: собственные тесты" hook_tests
# The stand for e2e is checked first and the suite runs last, to learn that the suite has
# nowhere to run before twenty minutes of the other gates. The script starts nothing and
# names the command that prepares the stand.
if full_requested; then
  step e2e "e2e: стенд" bash "$ROOT/scripts/check-e2e-stand.sh"
fi
# --dry-run installs nothing: the gates below run on the node_modules in place, and a
# package-lock.json out of sync with package.json is exactly what they do not see.
step frontend "Фронтенд: синхронность lock-файла" in_client npm ci --dry-run
# Code comparison test: both sides of the comment comparison and the command itself, on
# copies of real files. A broken lexer would say "same code" about any change. The lexers
# come from the client's node_modules, hence after the lock file.
step frontend "Стенд кадров: тест сравнения кода" node "$ROOT/scripts/visual/compare-code.test.mjs"
# Gates in src/gates are tree rules that read sources as text. Dead client code is knip's;
# the format is checked by lint (prettier/prettier and --max-warnings 0).
step "frontend docs" "Фронтенд: гейты в src/gates" in_client npm run test:conventions
step frontend "Фронтенд: мертвый код (knip)" in_client npm run knip
step frontend "Фронтенд: линт и формат" in_client npm run lint:ci
step frontend "Фронтенд: типы" in_client npm run type-check
# The TypeScript of the frame stand, on the client's tools and lint config: the client run
# does not reach outside its folder, so these run from the root.
step frontend "Стенд кадров: типы" in_root src/DM.Web.Client/node_modules/.bin/vue-tsc --noEmit -p scripts/visual/tsconfig.json
step frontend "Стенд кадров: линт и формат" in_root src/DM.Web.Client/node_modules/.bin/eslint --config src/DM.Web.Client/eslint.config.js --max-warnings 0 "scripts/visual/**/*.ts"
# The docs job: architecture tests alone. The map calls it only when build is not needed,
# and build runs the same tests below.
step docs "Документы: архитектурные тесты Release" dotnet test "$ROOT/test/DM.Architecture.Tests" -c Release --nologo
step build "Бэкенд: форматирование" dotnet format "$ROOT/DM.sln" whitespace --verify-no-changes
step build "Бэкенд: сборка Release" dotnet build "$ROOT/DM.sln" -c Release --nologo -v q
step build "Бэкенд: архитектурные тесты Release" dotnet test "$ROOT/test/DM.Architecture.Tests" --no-build -c Release --nologo
# The whole compose-topology job: it needs only docker and takes twenty-two seconds, so it
# is not split.
if full_requested; then
  step compose-topology "Скрипты оболочки: shellcheck" bash "$ROOT/scripts/check-shell-scripts.sh"
  step compose-topology "Развертывание: топология compose" bash "$ROOT/scripts/deployment/check-compose-topology.sh"
  step compose-topology "Развертывание: конфигурации nginx" bash "$ROOT/scripts/deployment/check-nginx-configuration.sh"
  step compose-topology "Развертывание: установщик сервера" bash "$ROOT/scripts/deployment/check-server-installer.sh"
  step compose-topology "Развертывание: скрипт релиза" bash "$ROOT/scripts/deployment/check-release-entry.sh"
  step compose-topology "Развертывание: мониторинг" bash "$ROOT/scripts/deployment/check-monitoring.sh"
  step compose-topology "Развертывание: команда выпуска" bash "$ROOT/scripts/release/check-release.sh"
  step "frontend docs" "Фронтенд: юнит-тесты с покрытием" in_client npm run test:coverage
  step frontend "Фронтенд: сборка" in_client npm run build
  step dependency-scan "Зависимости: пакеты npm" in_client npm audit --omit=dev --audit-level=high
  step dependency-scan "Зависимости: пакеты .NET" bash "$ROOT/scripts/check-vulnerable-packages.sh"
  step build "Бэкенд: тесты Release" backend_tests
  step build "Бэкенд: покрытие" bash "$ROOT/scripts/check-coverage.sh"
  # Opening the HTML report is forbidden: after a failure Playwright opens it when it sees a
  # terminal, and the run would sit on the report server until Ctrl+C. CI=1 and the global
  # timeout are the settings of the e2e job, as in the hook.
  step e2e "e2e: набор на одном профиле" in_client env CI=1 PLAYWRIGHT_HTML_OPEN=never npm run test:e2e:fast -- --global-timeout=1800000
  say "Все нужные гейты зеленые"
else
  say "Гейты ветки зеленые. Юнит-тесты клиента, тесты решения, покрытие, e2e и docker гонит CI на PR, здесь они идут с DM_GATES_FULL=1 или по имени шага"
fi
