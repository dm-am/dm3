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
# Без аргумента идут только шаги, нужные изменениям: карта scripts/ci-paths.json
# раскладывает их по группам путей тем же скриптом, что и хук, и джоба changes
# воркфлоу. Изменения тут шире, чем у хука: рабочее дерево против общей базы с
# origin/dev, вместе с неотслеженными файлами, потому что скрипт зовут до
# коммита. Шаг, названный аргументом, идет всегда: его попросили по имени.
# Набор e2e, как и в хуке, идет только по своей воле: на ветке его гонит CI на
# PR. Полный набор по своей воле, e2e в том числе:
#   DM_GATES_FULL=1 bash scripts/gates.sh
#
# Прогон занимает машину целиком. Второй тяжелый процесс рядом — параллельный
# dotnet test, идущий воркфлоу, запущенная съемка — роняет юнит-тесты по
# таймауту и интеграционные по общей базе, и отказ выглядит как настоящий.

set -e

ROOT=$(git rev-parse --show-toplevel)
CLIENT="$ROOT/src/DM.Web.Client"
ONLY=${1:-}

say() { printf '\n\033[36m==> %s\033[0m\n' "$1"; }

# Решение карты: строка "<джоба>=true|false" на каждую джобу, отчет в stderr.
SELECTION=""
if [ -z "$ONLY" ]; then
  say "Пути: какие гейты нужны изменениям"
  SELECTION=$(node "$ROOT/scripts/ci-paths.mjs" --worktree origin/dev)
fi

# Нужна ли хоть одна из джоб списка. "always" значит шаг вне карты.
wanted() {
  for job in $1; do
    [ "$job" = always ] && return 0
    printf '%s\n' "$SELECTION" | grep -qx "$job=true" && return 0
  done
  return 1
}

# Шаг выполняется, когда фильтр входит в его имя, а без фильтра, когда карта
# зовет одну из его джоб.
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

# Набор e2e и проверка стенда под него идут только по своей воле, как в хуке и
# по той же причине: на ветке набор гонит джоба e2e на PR, а локальный шел бы
# против стенда, который ради каждой ветки пришлось бы пересобирать. Воля тут
# двух видов: непустая DM_GATES_FULL или имя шага аргументом, и тогда шаги
# выбирает фильтр по имени.
e2e_requested() { [ -n "${DM_GATES_FULL:-}" ] || [ -n "$ONLY" ]; }

step always "Хуки: собственные тесты" hook_tests
# Стенд для e2e проверяется в начале, а набор идет последним: узнать, что
# набору не на чем бежать, надо до двадцати минут остальных гейтов. Скрипт
# ничего не поднимает, при отказе он называет команду, которая готовит стенд.
if e2e_requested; then
  step e2e "e2e: стенд" bash "$ROOT/scripts/check-e2e-stand.sh"
elif wanted e2e; then
  say "e2e: на ветке его гонит CI на PR, здесь только с DM_GATES_FULL=1 или по имени шага"
fi
# Джоба compose-topology целиком: разбор скриптов и четыре проверки
# развертывания. Ей нужен только docker, а идет она двадцать две секунды при
# сорока минутах всего прогона, поэтому делить ее на дешевое сюда и дорогое в
# CI нечего: половина, оставленная там, все равно красила бы пуш на любой
# правке файлов развертывания. Ровно так до сих пор и прятался разбор скриптов.
# (Слово shellcheck в начале строки комментария этот же инструмент
# читает как свою директиву и падает на ней, поэтому оно тут не первое.)
step compose-topology "Скрипты оболочки: shellcheck" bash "$ROOT/scripts/check-shell-scripts.sh"
step compose-topology "Развертывание: топология compose" bash "$ROOT/scripts/deployment/check-compose-topology.sh"
step compose-topology "Развертывание: конфигурации nginx" bash "$ROOT/scripts/deployment/check-nginx-configuration.sh"
step compose-topology "Развертывание: установщик сервера" bash "$ROOT/scripts/deployment/check-server-installer.sh"
step compose-topology "Развертывание: мониторинг" bash "$ROOT/scripts/deployment/check-monitoring.sh"
# --dry-run у npm ci не ставит ничего: гейты ниже гоняются по уже разложенному
# node_modules, и рассинхрон package-lock.json с package.json — ровно то, чего
# они не видят.
step frontend "Фронтенд: синхронность lock-файла" in_client npm ci --dry-run
step frontend "Фронтенд: линт" in_client npm run lint:ci
step frontend "Фронтенд: типы" in_client npm run type-check
step "frontend docs" "Фронтенд: юнит-тесты с покрытием" in_client npm run test:coverage
step frontend "Фронтенд: сборка" in_client npm run build
step dependency-scan "Зависимости: пакеты npm" in_client npm audit --omit=dev --audit-level=high
step dependency-scan "Зависимости: пакеты .NET" bash "$ROOT/scripts/check-vulnerable-packages.sh"
# Джоба docs: архитектурные тесты без остального решения. Карта зовет ее только
# тогда, когда build не нужен, а build гоняет эти же тесты в составе решения.
step docs "Документы: архитектурные тесты Release" dotnet test "$ROOT/test/DM.Architecture.Tests" -c Release --nologo
step build "Бэкенд: форматирование" dotnet format "$ROOT/DM.sln" whitespace --verify-no-changes
step build "Бэкенд: сборка Release" dotnet build "$ROOT/DM.sln" -c Release --nologo -v q
step build "Бэкенд: тесты Release" backend_tests
step build "Бэкенд: покрытие" bash "$ROOT/scripts/check-coverage.sh"
# Открывать отчет HTML запрещено явно: после падения Playwright открывает его,
# когда видит терминал, а этот скрипт запускают из терминала, и прогон стоял
# бы на сервере отчета до Ctrl+C. CI=1 и общий таймаут - как в хуке и по той же
# причине: настройки джобы e2e и конец прогона, даже если тест повис.
if e2e_requested; then
  step e2e "e2e: набор на одном профиле" in_client env CI=1 PLAYWRIGHT_HTML_OPEN=never npm run test:e2e:fast -- --global-timeout=1800000
fi

say "Все нужные гейты зеленые"
