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
# работать на нетронутом дереве. Расхождение ловит DM.Architecture.Tests, и
# сверок там две: одна сличает команды этих двух файлов между собой, вторая —
# со ВСЕМИ шагами обоих воркфлоу. Шаг, которого тут нет, обязан стоять в
# именованном реестре сверки с причиной; молчание — это то, чем оба падения
# уже оплачены: порог покрытия бэкенда стоял только в воркфлоу, а shellcheck
# прятался в джобе compose-topology, которую сверка вообще не смотрела.
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

# Тесты решения и сбор покрытия одним прогоном — так их получает CI: шаг Test
# в .github/workflows/dotnet.yml пишет по отчету cobertura на тестовый проект в
# --results-directory, а следующий шаг читает оттуда же. Порог поэтому идет
# сразу за тестами и отдельным прогоном ничего не собирает.
#
# Каталог отдается пустым. У CI чекаут чистый, а на машине разработчика
# TestResults копится прогонами, и отчет, слитый из нескольких состояний кода,
# не описывает ни одно из них: удаленный вчера файл все еще приносит свои
# строки. Это же требование записано в .gitignore рядом с самим каталогом.
backend_tests() {
  rm -rf "$ROOT/TestResults"
  dotnet test "$ROOT/DM.sln" --no-build -c Release --nologo --results-directory "$ROOT/TestResults" --collect:"XPlat Code Coverage"
}

step "Хуки: собственные тесты" hook_tests
# Разбор скриптов — единственный шаг джобы compose-topology, которому нужен
# только docker, и до сих пор он был виден лишь из CI. (Слово shellcheck в
# начале строки комментария этот же инструмент читает как свою директиву и
# падает на ней, поэтому оно тут не первое.) --dry-run у npm ci не ставит
# ничего: гейты ниже гоняются по уже разложенному node_modules, и рассинхрон
# package-lock.json с package.json — ровно то, чего они не видят.
step "Скрипты оболочки: shellcheck" bash "$ROOT/scripts/check-shell-scripts.sh"
step "Фронтенд: синхронность lock-файла" in_client npm ci --dry-run
step "Фронтенд: линт" in_client npm run lint:ci
step "Фронтенд: типы" in_client npm run type-check
step "Фронтенд: юнит-тесты с покрытием" in_client npm run test:coverage
step "Фронтенд: сборка" in_client npm run build
step "Зависимости: пакеты npm" in_client npm audit --omit=dev --audit-level=high
step "Зависимости: пакеты .NET" bash "$ROOT/scripts/check-vulnerable-packages.sh"
step "Бэкенд: форматирование" dotnet format "$ROOT/DM.sln" whitespace --verify-no-changes
step "Бэкенд: сборка Release" dotnet build "$ROOT/DM.sln" -c Release --nologo -v q
step "Бэкенд: тесты Release" backend_tests
step "Бэкенд: покрытие" bash "$ROOT/scripts/check-coverage.sh"

say "Все гейты зеленые"
