#!/bin/sh
#
# Прогоняет ровно те гейты, что и хук pre-push, в том же порядке.
#
# Зачем отдельно от хука: пуш с непройденным гейтом стоит полного прогона —
# около двадцати минут, — и узнать о падении в его конце значит потратить их
# впустую. Хук остается последней преградой, а эта команда дает тот же ответ
# заранее и на своих условиях: ее можно запустить, пока правка еще горячая,
# и повторить только упавший шаг.
#
# Почему один и тот же список живет в двух файлах: хук обязан быть
# самодостаточным (клон без этого скрипта все равно защищен), а скрипт обязан
# работать на нетронутом дереве. Расхождение ловит gatesMirrorTheHook в
# DM.Architecture.Tests: он сверяет команды обоих файлов.
#
# Запуск целиком:
#   bash scripts/gates.sh
# Отдельный шаг (подстрока имени):
#   bash scripts/gates.sh фронтенд
#
# Прогон занимает машину целиком. Второй тяжелый процесс рядом — параллельный
# dotnet test, идущий воркфлоу, запущенная съемка — роняет юнит-тесты по
# таймауту и интеграционные по общей базе, и отказ выглядит как настоящий.

set -e

ROOT=$(git rev-parse --show-toplevel)
CLIENT="$ROOT/src/DM.Web.Client"
ONLY=${1:-}

say() { printf '\n\033[36m==> %s\033[0m\n' "$1"; }

# Шаг выполняется, когда фильтр пуст или входит в его имя.
step() {
  name=$1
  shift
  case "$name" in
    *"$ONLY"*) ;;
    *) return 0 ;;
  esac
  say "$name"
  "$@"
}

hook_tests() {
  node "$ROOT/.claude/hooks/block-dangerous-git.test.js"
  node "$ROOT/.claude/hooks/block-new-migrations.test.js"
  node "$ROOT/.claude/hooks/lint-edited-file.test.js"
}

in_client() { (cd "$CLIENT" && "$@"); }

step "Хуки: собственные тесты" hook_tests
step "Фронтенд: линт" in_client npm run lint:ci
step "Фронтенд: типы" in_client npm run type-check
step "Фронтенд: юнит-тесты с покрытием" in_client npm run test:coverage
step "Фронтенд: сборка" in_client npm run build
step "Зависимости: пакеты npm" in_client npm audit --omit=dev --audit-level=high
step "Зависимости: пакеты .NET" bash "$ROOT/scripts/check-vulnerable-packages.sh"
step "Бэкенд: форматирование" dotnet format "$ROOT/DM.sln" whitespace --verify-no-changes
step "Бэкенд: сборка Release" dotnet build "$ROOT/DM.sln" -c Release --nologo -v q
step "Бэкенд: тесты Release" dotnet test "$ROOT/DM.sln" --no-build -c Release --nologo

say "Все гейты зеленые"
