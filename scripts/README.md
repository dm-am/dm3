# Скрипты

Перечень всех скриптов репозитория: что делает, кто зовет, где описан подробнее. Покрывает `scripts/`, `docker/scripts/` и оболочечные файлы верхнего уровня `docker/`. Хуки Claude Code лежат в `.claude/hooks`, их зовет `.claude/settings.json`, здесь их нет.

Перечень держит гейт [scriptsReadme.spec.ts](../src/DM.Web.Client/src/gates/scriptsReadme.spec.ts): у каждого файла этих папок есть строка в таблицах ниже, у каждой строки есть файл. Новый скрипт приходит со строкой, переименованный или удаленный уносит свою. Что файл вообще кто-то зовет, отдельно держит [scriptCallers.spec.ts](../src/DM.Web.Client/src/gates/scriptCallers.spec.ts), и этот перечень вызовом для него не считается. Колонку "Кто зовет" гейт не сверяет: ее читают рядом с шапкой самого файла, которая первична.

## Как читать колонку "Кто зовет"

- CI: джоба workflow [dotnet.yml](../.github/workflows/dotnet.yml) или [security.yml](../.github/workflows/security.yml), названа по имени джобы.
- `gates.sh` и хук `pre-push`: те же гейты, что в CI, на машине разработчика ([TESTING.md](../docs/guides/TESTING.md#хук-перед-пушем)). Часть шагов идет только с `DM_GATES_FULL=1`.
- `dm.ps1` и `dm.sh`: управляющие скрипты локального стенда ([LOCAL_SETUP.md](../docs/guides/LOCAL_SETUP.md#команды)).
- compose: docker compose запускает файл или монтирует его в контейнер.
- сервер: установщик, cron или systemd на машине, где клон репозитория лежит в `/opt/dm3`. Поэтому серверные скрипты остаются в `docker/`, а не в `scripts/`.
- человек: запуск руками по документу или по шапке файла.
- имя другого скрипта: тот зовет или подключает его по ходу работы.

## Гейты и проверки

Корень `scripts/`, папки `deployment/`, `hooks/` и `lib/`. Все check-скрипты роняют проверку кодом возврата и называют причину.

| Файл | Что делает | Кто зовет | Подробнее |
|------|-----------|-----------|-----------|
| [gates.sh](gates.sh) | Гонит локально те же гейты, что хук `pre-push`, в том же порядке, до пуша. Без аргумента только шаги, которые нужны правке по карте путей, с аргументом шаги по части имени | человек перед пушем | [TESTING.md](../docs/guides/TESTING.md#хук-перед-пушем) |
| [hooks/pre-push](hooks/pre-push) | Хук git на пуше: отвергает прямой пуш в dev и main, на пуше ветки гонит дешевые и точные гейты, остальные только с `DM_GATES_FULL=1` | git на каждом пуше, включается командой `git config core.hooksPath scripts/hooks` | [TESTING.md](../docs/guides/TESTING.md#хук-перед-пушем), шапка файла |
| [ci-paths.json](ci-paths.json) | Карта путей: какие группы файлов какие джобы CI вызывают. Данные, не скрипт | `ci-paths.mjs`, тест AgentToolingShould | [TESTING.md](../docs/guides/TESTING.md#хук-перед-пушем) |
| [ci-paths.mjs](ci-paths.mjs) | По карте `ci-paths.json` решает, какие проверки нужны правке, и печатает по строке "джоба=true или false" на джобу | CI (джоба changes), `gates.sh`, `pre-push` | шапка файла |
| [lib/common.sh](lib/common.sh) | Общее для check-скриптов: путь в виде, в котором его понимает нативная программа под Git Bash. Подключается, не запускается | `check-coverage.sh`, `deployment/check-*.sh`, `verify-seed-reproducibility.sh` | шапка файла |
| [check-coverage.sh](check-coverage.sh) | Роняет сборку, когда слитое покрытие решения строк или ветвей ниже храповика. Отчеты тестовых проектов сливает ReportGenerator | CI (джоба build), `gates.sh`, `pre-push` с `DM_GATES_FULL=1` | [TESTING.md](../docs/guides/TESTING.md#что-требуется-от-контрибьютора) |
| [check-e2e-stand.sh](check-e2e-stand.sh) | Проверяет, что локальный стенд годится для e2e (API отвечает, лимит запросов выключен), ничего не поднимает, при отказе называет команду | `gates.sh` и `pre-push`, когда идет e2e | [TESTING.md](../docs/guides/TESTING.md#e2e) |
| [check-shell-scripts.sh](check-shell-scripts.sh) | Гонит shellcheck в контейнере по каждому `*.sh` из git и по хуку `pre-push` | CI (джоба compose-topology), `gates.sh`, `pre-push` с `DM_GATES_FULL=1` | шапка файла |
| [check-vulnerable-packages.sh](check-vulnerable-packages.sh) | Роняет сборку, когда `dotnet list package --vulnerable` показывает уязвимость, которой нет в [vulnerability-allowlist.txt](../.github/vulnerability-allowlist.txt). Сам делает restore | CI (джоба dependency-scan), `gates.sh`, `pre-push` с `DM_GATES_FULL=1` | шапка файла |
| [verify-seed-reproducibility.sh](verify-seed-reproducibility.sh) | Сеет дважды в одну пустую базу и сравнивает результат, а с `--compare` сравнивает два коммита. Сбрасывает базы локального стека | человек, когда правит сидер | [visual/README.md](visual/README.md), шапка файла |
| [deployment/check-compose-topology.sh](deployment/check-compose-topology.sh) | Роняет проверку, когда файлы развертывания перестали описывать один стенд: оверлей не разбирается, контекст сборки указывает на удаленный каталог, юнит systemd и установщик называют разные compose-файлы | CI (джоба compose-topology), `gates.sh`, `pre-push` с `DM_GATES_FULL=1` | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#стенд-preview) |
| [deployment/check-monitoring.sh](deployment/check-monitoring.sh) | Проверяет, что мониторинг сделает то, что в нем написано: конфигурация Prometheus читается, правило срабатывает на состоянии, под которое написано, алерт доходит до приемника, приемник стартует | CI (джоба compose-topology), `gates.sh`, `pre-push` с `DM_GATES_FULL=1` | [MONITORING.md](../docs/guides/MONITORING.md#alerting) |
| [deployment/check-nginx-configuration.sh](deployment/check-nginx-configuration.sh) | Проверяет, что конфигурации nginx развертывания разбираются тем образом, который развертывание запускает | CI (джоба compose-topology), `gates.sh`, `pre-push` с `DM_GATES_FULL=1` | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#https) |
| [deployment/check-server-installer.sh](deployment/check-server-installer.sh) | Проверяет два обещания установщика сервера: он отказывается ставить поверх непустого каталога и переносит новую переменную шаблона в уже существующий файл окружения. Без docker и сети | CI (джоба compose-topology), `gates.sh`, `pre-push` с `DM_GATES_FULL=1` | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#автоматическая-установка) |

## Управление стендом

`dm.ps1` для Windows и `dm.sh` для Linux и Mac дают один набор команд: `start`, `stop`, `reset`, `seed`, `e2e`, `status`, `logs`, `help`. Парность держит тест ManagementScriptsShould. Части `dm.ps1` лежат в `dm/`.

| Файл | Что делает | Кто зовет | Подробнее |
|------|-----------|-----------|-----------|
| [dm.ps1](dm.ps1) | Управляющий скрипт локального стека для Windows PowerShell. Подключает части из `dm/` | человек, стенд кадров (`visual/rebuild.mjs` при `--rebuild-stand`) | [LOCAL_SETUP.md](../docs/guides/LOCAL_SETUP.md#команды) |
| [dm.sh](dm.sh) | То же для Linux и Mac | человек, стенд кадров (`visual/rebuild.mjs` вне Windows) | [LOCAL_SETUP.md](../docs/guides/LOCAL_SETUP.md#команды) |
| [dm/EndToEnd.ps1](dm/EndToEnd.ps1) | Команда `e2e`: пересоздает только API из этого дерева с выключенным лимитом запросов, как оверлей джобы e2e в CI | `dm.ps1` | [TESTING.md](../docs/guides/TESTING.md#e2e) |
| [dm/Environment.ps1](dm/Environment.ps1) | Готовит `docker/.env` через `docker/scripts/init-env.sh` и проверяет его, находит Git Bash вместо `bash` из WSL | `dm.ps1` | [CONFIGURATION.md](../docs/references/CONFIGURATION.md) |
| [dm/Seed.ps1](dm/Seed.ps1) | Команда `seed`: запускает сид разовым контейнером и перезапускает API, чтобы тот пересчитал стартовые проекции | `dm.ps1` | [LOCAL_SETUP.md](../docs/guides/LOCAL_SETUP.md#тестовые-данные) |
| [dm/Services.ps1](dm/Services.ps1) | Команды `start`, `stop`, `reset`: поднимает инфраструктуру и ждет здоровья сервисов, затем API, останавливает, стирает тома | `dm.ps1` | [LOCAL_SETUP.md](../docs/guides/LOCAL_SETUP.md#команды) |
| [dm/Status.ps1](dm/Status.ps1) | Команды `status` и `logs`: сервисы docker и процессы стенда вне docker, в первую очередь два дев-сервера сразу | `dm.ps1` | [LOCAL_SETUP.md](../docs/guides/LOCAL_SETUP.md#команды) |
| [dm/Steps.ps1](dm/Steps.ps1) | Строки шагов (в консоли с анимацией и отметкой, в захваченном выводе "шаг: результат") и процессы за ними, общие для всех команд, чтение `dm/addresses.txt`. При сбое печатает stderr первым | `dm.ps1` | шапка файла |
| [dm/addresses.txt](dm/addresses.txt) | Адреса локальных сервисов, один список для `dm.ps1` и `dm.sh` | `dm.ps1`, `dm.sh` | шапка файла |

## Сводка состояния

`build-status.mjs` считает состояние сайта по дереву и пишет [STATUS.md](../docs/STATUS.md). Стенд и браузер он не запускает. Модули `lib/` читают компилятором TypeScript клиента.

| Файл | Что делает | Кто зовет | Подробнее |
|------|-----------|-----------|-----------|
| [docs/build-status.mjs](docs/build-status.mjs) | Считает маршруты клиента, состояния стенда кадров и эндпоинты сервера и пишет `docs/STATUS.md`. С `--check` ничего не пишет и при расхождении печатает дифф | человек, тест StatusDocumentShould (джобы build и docs) | [STATUS.md](../docs/STATUS.md) |
| [docs/lib/diff.mjs](docs/lib/diff.mjs) | Построчный дифф записанного документа и посчитанного, для `--check` | `build-status.mjs` | шапка файла |
| [docs/lib/endpoints.mjs](docs/lib/endpoints.mjs) | Читает эндпоинты сервера из слепка `artifacts/openapi-routes.json` | `build-status.mjs` | шапка файла |
| [docs/lib/frames.mjs](docs/lib/frames.mjs) | Читает состояния стенда кадров (`visual/states.ts`) и роли (`visual/roles.ts`) | `build-status.mjs` | шапка файла |
| [docs/lib/matcher.mjs](docs/lib/matcher.mjs) | По адресу находит маршрут, который его открывает: шаблон vue-router превращается в регулярное выражение, при споре выигрывает маршрут с большим числом статичных сегментов | `build-status.mjs` | шапка файла |
| [docs/lib/render.mjs](docs/lib/render.mjs) | Пишет сам документ: только факты, без даты, коммита и числа тестов | `build-status.mjs` | шапка файла |
| [docs/lib/repo.mjs](docs/lib/repo.mjs) | Где лежит дерево, как оно обходится и читается, откуда берется компилятор. Обходы отсортированы, пути с прямыми слэшами | `build-status.mjs` и модули `lib/` | шапка файла |
| [docs/lib/router.mjs](docs/lib/router.mjs) | Читает таблицу маршрутов клиента: записи, дочерние сведены в полные пути, meta в том виде, в каком страница ее получает | `build-status.mjs` | шапка файла |
| [docs/lib/sections.mjs](docs/lib/sections.mjs) | Таблица разделов сайта по первому сегменту пути, единственная написанная руками часть документа | `docs/lib/render.mjs` | шапка файла |
| [docs/lib/source.mjs](docs/lib/source.mjs) | Чтение исходников TypeScript: синтаксическое дерево от компилятора и разбор путей из шаблонов, констант и стрелочных функций | `docs/lib/frames.mjs`, `docs/lib/router.mjs`, `docs/lib/endpoints.mjs` | шапка файла |

## Стенд кадров и снимок ответов API

Кадры страниц и снимок GET-ответов API доказывают, что правка невидима. Точка входа `visual.mjs`, остальное ее части. В CI и в гейты стенд не входит, кроме теста сравнения кода `compare-code.test.mjs`. Условия, коды возврата и разбор расхождений описаны в [visual/README.md](visual/README.md).

| Файл | Что делает | Кто зовет | Подробнее |
|------|-----------|-----------|-----------|
| [visual/visual.mjs](visual/visual.mjs) | Точка входа: `stand`, `snap`, `diff`, `api-snap`, `api-diff`. По умолчанию мерит уже запущенный стенд и ничего в нем не меняет | человек и агенты по PROCESS.md и скиллу refactor-in-bounds | [visual/README.md](visual/README.md#команды) |
| [visual/tsconfig.json](visual/tsconfig.json) | Проверка типов файлов стенда кадров на TypeScript: узел, Playwright и пути клиента | хук перед пушем, `gates.sh` и CI, шаг типов и линта стенда кадров | эта строка |
| [visual/api.mjs](visual/api.mjs) | Снимок ответов API: каждый GET из слепка маршрутов от гостя, участника и модератора в виде нормализованного JSON | `visual.mjs` (`api-snap`, `api-diff`) | [visual/README.md](visual/README.md#что-снимается) |
| [visual/api-fixture.mjs](visual/api-fixture.mjs) | Подставляет в параметры пути GET-маршрутов идентификаторы, найденные через списки самого API | `visual/api.mjs` | [visual/README.md](visual/README.md#что-снимается) |
| [visual/api-volatile.mjs](visual/api-volatile.mjs) | Поля ответов API, которые различаются между двумя прогонами одного коммита, каждое с причиной. Значение заменяется именем поля | `visual/api.mjs`, `visual/frames.spec.ts` | [visual/README.md](visual/README.md#маски-и-изменчивые-поля) |
| [visual/api-volatile.d.mts](visual/api-volatile.d.mts) | Типы той части `api-volatile.mjs`, которую читает сторона TypeScript | TypeScript, модуль рядом | шапка файла |
| [visual/unordered.mjs](visual/unordered.mjs) | Списки, которые API отдает без заданного порядка. Оба инструмента упорядочивают их сами, запись уходит вместе с дефектом сервера | `visual/api.mjs`, `visual/frames.spec.ts` | [visual/README.md](visual/README.md#маски-и-изменчивые-поля) |
| [visual/unordered.d.mts](visual/unordered.d.mts) | Типы `unordered.mjs` для стороны TypeScript | TypeScript, модуль рядом | шапка файла |
| [visual/base.mjs](visual/base.mjs) | База снимка: `meta.json` рядом со снятым и проверки, отказывающие сравнивать несравнимое | `visual.mjs` | [visual/README.md](visual/README.md#отпечаток-данных-и-пересев) |
| [visual/intact.mjs](visual/intact.mjs) | Проверяет, что прогон мерил один стенд и одно дерево от начала до конца, иначе прогон недействителен | `visual.mjs` | [visual/README.md](visual/README.md#когда-что-то-не-сходится) |
| [visual/stand.mjs](visual/stand.mjs) | Работающий стенд, как его находит прогон, только на чтение: отвечает ли API, засеян ли, выключены ли лимиты, отпечаток данных | `visual.mjs`, `visual/intact.mjs` | [visual/README.md](visual/README.md#отпечаток-данных-и-пересев) |
| [visual/rebuild.mjs](visual/rebuild.mjs) | Строгий режим `--rebuild-stand`: общий стек сброшен, пересобран из дерева и засеян на закрепленных эпохе и зерне. Уничтожает данные стенда | `visual.mjs` | [visual/README.md](visual/README.md) |
| [visual/preview.mjs](visual/preview.mjs) | Клиент, с которого снимают кадры: собирается из дерева в каталог прогона и отдается через `vite preview` на 5174 | `visual.mjs` | [visual/README.md](visual/README.md#как-снять-базу) |
| [visual/tree.mjs](visual/tree.mjs) | Где работает прогон: репозиторий, клиент, каталог сборок и баз, дерево, из которого собирают | `visual.mjs`, `visual/preview.mjs`, `visual/rebuild.mjs` | шапка файла |
| [visual/refusal.mjs](visual/refusal.mjs) | Как останавливается стенд кадров: отказ говорит, что не так, и завершает прогон кодом 2 | `visual.mjs` и модули рядом | [visual/README.md](visual/README.md#команды) |
| [visual/played-game.mjs](visual/played-game.mjs) | Игра, в которой играет участник, ищется, а не называется: какие аккаунты в какой игре, решает генератор сида | `visual/api-fixture.mjs`, `visual/fixture.ts` | шапка файла |
| [visual/played-game.d.mts](visual/played-game.d.mts) | Типы `played-game.mjs` для стороны TypeScript | TypeScript, модуль рядом | шапка файла |
| [visual/frames.config.ts](visual/frames.config.ts) | Конфиг Playwright кадров: один воркер и порядок файла, место для кадров различий | `visual.mjs` | шапка файла |
| [visual/frames.spec.ts](visual/frames.spec.ts) | Сам прогон: открывает каждое состояние от каждой роли и снимает три канала (PNG, дерево DOM, вычисленный стиль) | `visual/frames.config.ts` | [visual/README.md](visual/README.md#что-снимается) |
| [visual/states.ts](visual/states.ts) | Состояния, на которые смотрит стенд, одним списком из разделов `visual/states/`: кто смотрит, где и что делается на странице до съемки | `visual/frames.spec.ts`, `docs/build-status.mjs` (читает для STATUS.md) | [STATUS.md](../docs/STATUS.md) |
| [visual/states/shared.ts](visual/states/shared.ts) | Общее для разделов: тип состояния, помощники (кнопка, отказ API, вход) и сидовые константы | разделы `visual/states/*.ts` | шапка файла |
| [visual/states/guest.ts](visual/states/guest.ts) | Состояния гостя: главная, список игр с фильтром, входы и письма | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/member.ts](visual/states/member.ts) | Состояния участника: комментарии, настройки, игры и аккаунты по роли | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/staff.ts](visual/states/staff.ts) | Состояния ролей персонала на главной и в профилях | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/shell.ts](visual/states/shell.ts) | Состояния оболочки сайта: боковая панель при отказе, выделение, фокус клавишей | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/games.ts](visual/states/games.ts) | Состояния раздела игр: список, страница игры, посты, настройки | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/characters.ts](visual/states/characters.ts) | Состояния раздела персонажей игры | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/blogsForum.ts](visual/states/blogsForum.ts) | Состояния блогов и форума | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/chat.ts](visual/states/chat.ts) | Состояния чата | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/account.ts](visual/states/account.ts) | Состояния страницы аккаунта и его настроек | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/profile.ts](visual/states/profile.ts) | Состояния страниц профиля | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/moderation.ts](visual/states/moderation.ts) | Состояния модерации | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/tickets.ts](visual/states/tickets.ts) | Состояния обращений в поддержку | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/editor.ts](visual/states/editor.ts) | Состояния редактора | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/otherPages.ts](visual/states/otherPages.ts) | Состояния остальных страниц: о проекте, правила, статистика, пожертвования | `visual/states.ts` | [STATUS.md](../docs/STATUS.md) |
| [visual/states/commonKeys.ts](visual/states/commonKeys.ts) | Узлы, у которых каждое состояние записывает вычисленный стиль | `visual/frames.spec.ts` через `visual/states.ts` | шапка файла |
| [visual/roles.ts](visual/roles.ts) | Аккаунты, под которыми стенд смотрит, по одному на роль. Сид создает ровно их | `visual/sessions.ts`, `visual/states.ts`, `visual/api-volatile.mjs` | шапка файла |
| [visual/sessions.ts](visual/sessions.ts) | Один раз за прогон входит каждой ролью и сохраняет сеанс так же, как глобальная настройка e2e | `visual/frames.config.ts` | шапка файла |
| [visual/fixture.ts](visual/fixture.ts) | Вещи, которые состояния берут из сида по GUID, ищутся один раз за прогон через API | `visual/frames.spec.ts`, `visual/sessions.ts`, `visual/states.ts` | шапка файла |
| [visual/env.ts](visual/env.ts) | Переменные, которые `visual.mjs` передает прогону. Пропавшая значит, что прогон запущен руками | `visual/frames.config.ts`, `visual/frames.spec.ts`, `visual/sessions.ts` | шапка файла |
| [visual/capture.ts](visual/capture.ts) | Что закреплено на странице до первого скрипта приложения: случайность, часы | `visual/frames.spec.ts` | [visual/README.md](visual/README.md#что-снимается) |
| [visual/masks.ts](visual/masks.ts) | Места страницы, которым разрешено различаться между двумя прогонами одного коммита, каждое с причиной | `visual/capture.ts`, `visual/frames.spec.ts` | [visual/README.md](visual/README.md#маски-и-изменчивые-поля) |
| [visual/reporter.ts](visual/reporter.ts) | Отчет прогона `diff`: какой из трех каналов сдвинулся и какие состояния не открылись. Текст для человека и JSON для скрипта | `visual/frames.config.ts` | [visual/README.md](visual/README.md#когда-что-то-не-сходится) |
| [visual/compare-code.mjs](visual/compare-code.mjs) | Доказывает, что правка тронула только комментарии: сравнивает файлы до и после как токены без комментариев | человек и агенты по PROCESS.md и скиллу refactor-in-bounds | [PROCESS.md](../docs/conventions/PROCESS.md#чем-доказывается-правка) |
| [visual/compare-code.test.mjs](visual/compare-code.test.mjs) | Тест `compare-code.mjs` в обе стороны: правка комментария равна, правка кода нет. Затем сама команда на копиях настоящих файлов | `gates.sh`, `pre-push` | [TESTING.md](../docs/guides/TESTING.md#хук-перед-пушем) |
| [visual/compare-css.mjs](visual/compare-css.mjs) | Гейт эквивалентности стилей: собирает клиент дважды и сравнивает таблицы стилей, которые получит браузер | человек и агенты по скиллу refactor-in-bounds | [PROCESS.md](../docs/conventions/PROCESS.md#чем-доказывается-правка) |
| [visual/compare/args.mjs](visual/compare/args.mjs) | Командная строка, общая для двух инструментов сравнения: две стороны (ревизия, рабочее дерево или каталог) и пути-фильтры | `visual/compare-code.mjs`, `visual/compare-css.mjs` | шапка файла |
| [visual/compare/build.mjs](visual/compare/build.mjs) | Собирает сторону сравнения так, как клиент уходит в поставку, в каталог со своим именем | `visual/compare-code.mjs`, `visual/compare-css.mjs` | шапка файла |
| [visual/compare/vite-build.mjs](visual/compare/vite-build.mjs) | Одна сборка материализованного дерева клиента в дочернем процессе, чтобы состояние прежней сборки не перешло в эту | `visual/compare/build.mjs` | шапка файла |
| [visual/compare/git.mjs](visual/compare/git.mjs) | Читает репозиторий на ревизии или как он лежит на диске, не трогая индекс, рабочее дерево и stash | `visual/compare-code.mjs`, `visual/compare-css.mjs` | шапка файла |
| [visual/compare/tokens.mjs](visual/compare/tokens.mjs) | Исходник как последовательность токенов без комментариев. Комментарии, которые читает инструмент (`#__PURE__`, директивы eslint и TypeScript), остаются кодом | `visual/compare-code.mjs` | шапка файла |
| [visual/compare/csharp.mjs](visual/compare/csharp.mjs) | C# как токены без комментариев. Лексер отказывается от символа, для которого нет правила, а не угадывает | `visual/compare/tokens.mjs`, `visual/compare-code.mjs` | шапка файла |
| [visual/compare/lexing.mjs](visual/compare/lexing.mjs) | Общее для лексеров: список токенов, ошибка остановки, сравнение двух списков | `visual/compare/tokens.mjs`, `visual/compare/csharp.mjs` | шапка файла |

## Серверные скрипты

Скрипты `docker/scripts/` едут на сервер вместе с клоном репозитория, установщик и cron зовут их по месту. Ночные задания бэкапа ставит `install-cron.sh`: 02:00 дамп базы, 04:00 копия хранилища, 05:00 проверка копий.

| Файл | Что делает | Кто зовет | Подробнее |
|------|-----------|-----------|-----------|
| [../docker/scripts/init-env.sh](../docker/scripts/init-env.sh) | Единственное место, где создается `docker/.env`: шаблон плюс сгенерированный ключ шифрования (`local`), а для `server` еще свежие пароли, окружение Production и закрепленный тег образа | `dm.ps1`, `dm.sh`, `setup-server.sh`, CI (джобы e2e, deployment-smoke, zap-scan), человек при ручной установке | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#ручная-установка) |
| [../docker/scripts/_env.sh](../docker/scripts/_env.sh) | Подгружает `docker/.env` для скрипта под cron, у которого почти нет окружения. Уже заданные значения побеждают. Подключается, не запускается | `backup-postgres.sh`, `backup-storage.sh`, `restore-storage.sh`, `verify-backup.sh`, `check-storage.sh` | шапка файла |
| [../docker/scripts/init-htpasswd.sh](../docker/scripts/init-htpasswd.sh) | Создает файл basic-auth, который стенд preview монтирует в nginx. В репозиторий файл не попадает, пароль спрашивает | `setup-server.sh`, CI (джоба deployment-smoke), оператор при смене пароля | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#стенд-preview) |
| [../docker/scripts/init-ssl.sh](../docker/scripts/init-ssl.sh) | Выпускает сертификат стенда через certbot по webroot и включает TLS на краю. Принимает домены аргументами | оператор, когда появился домен | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#https) |
| [../docker/scripts/require-empty-install-dir.sh](../docker/scripts/require-empty-install-dir.sh) | Установщик ставит и не обновляет: отказывается ставить в непустой каталог, пока не тронут docker и пакеты | `setup-server.sh` | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#автоматическая-установка) |
| [../docker/scripts/install-cron.sh](../docker/scripts/install-cron.sh) | Ставит в crontab задания бэкапа и создает журнал `/var/log/dm3-backup.log` | `setup-server.sh` | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#бэкапы) |
| [../docker/scripts/backup-postgres.sh](../docker/scripts/backup-postgres.sh) | Дамп PostgreSQL через контейнер в `/var/backups/postgresql`, таблица `OutboxEvents` без строк. Пишется во временный файл и ставится на место целым, старше `RETENTION_DAYS` (по умолчанию 30) удаляется | cron сервера в 02:00, CI (джоба deployment-smoke) | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#бэкапы) |
| [../docker/scripts/backup-storage.sh](../docker/scripts/backup-storage.sh) | Копия бакета объектного хранилища обычной папкой файлов ключом `dm-backup`, который только читает и перечисляет. Потом внешняя репликация, если заданы переменные AWS | cron сервера в 04:00, CI (джоба deployment-smoke) | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#бэкапы) |
| [../docker/scripts/verify-backup.sh](../docker/scripts/verify-backup.sh) | Проверяет копии: возраст, размер и целостность. Вердикт оставляет для node-exporter | cron сервера в 05:00 | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#бэкапы) |
| [../docker/scripts/restore-storage.sh](../docker/scripts/restore-storage.sh) | Возвращает копию `backup-storage.sh` в бакет ключом `dm-app`. Через `cp --recursive`, потому что у ключа нет права перечисления | оператор при восстановлении | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#откат) |
| [../docker/scripts/check-storage.sh](../docker/scripts/check-storage.sh) | Границы объектного хранилища на поднятом стеке: анонимное чтение только публичных префиксов, недоступность в обход прокси, лимит памяти сборщика, права каждой учетной записи | CI (джоба deployment-smoke), оператор | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#бэкапы) |
| [../docker/scripts/check-dump-excludes-outbox.sh](../docker/scripts/check-dump-excludes-outbox.sh) | Снимает настоящий дамп скриптом `backup-postgres.sh` и проверяет, что в нем есть данные другой таблицы и нет строк `OutboxEvents`, что `verify-backup.sh` его принимает и что восстановление дает пустую таблицу. Строку и восстановленную базу убирает за собой | CI (джоба deployment-smoke), оператор | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#бэкапы) |
| [../docker/scripts/check-database-privileges.sh](../docker/scripts/check-database-privileges.sh) | Читает с сервера, кто может подключиться к базе `dm3`: только `dm_app`, ни `PUBLIC`, ни экспортер | CI (джоба deployment-smoke), оператор после восстановления базы | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#откат) |
| [../docker/scripts/smoke-edge.sh](../docker/scripts/smoke-edge.sh) | Что должен отвечать край (nginx), в одном месте: те же проверки в конвейере и у оператора по публичному адресу | CI (джоба deployment-smoke), оператор после обновления контейнеров | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#стенд-preview) |

## Установщик и точки входа контейнеров

Оболочечные файлы верхнего уровня `docker/`: установщик сервера и скрипты, которые compose монтирует в контейнеры как есть.

| Файл | Что делает | Кто зовет | Подробнее |
|------|-----------|-----------|-----------|
| [../docker/setup-server.sh](../docker/setup-server.sh) | Установщик сервера на чистой Ubuntu: docker, ротация журналов, клон по `DM_REF`, `docker/.env`, порты, пароль preview, юнит systemd `dm3`, cron бэкапов, запуск стека | оператор по команде из DEPLOYMENT.md, `check-compose-topology.sh` и `check-server-installer.sh` его читают | [DEPLOYMENT.md](../docs/guides/DEPLOYMENT.md#автоматическая-установка) |
| [../docker/storage-start.sh](../docker/storage-start.sh) | Запуск объектного хранилища SeaweedFS: бакет из `-bucket`, учетные записи и анонимное чтение из файла идентичностей, который пишется на каждом старте из паролей окружения | compose (контейнер seaweedfs) | [SECURITY.md](../docs/conventions/SECURITY.md) |
| [../docker/postgres-init.sh](../docker/postgres-init.sh) | Создает роли приложения и экспортера и базу, которой владеет приложение, и отнимает у остальных ролей право подключаться к ней. Postgres запускает файл один раз, на пустом томе | compose (каталог initdb контейнера Postgres) | [SECURITY.md](../docs/conventions/SECURITY.md) |
| [../docker/alertmanager-init.sh](../docker/alertmanager-init.sh) | Точка входа контейнера alertmanager: подставляет переменные окружения в `prometheus/alertmanager.yml`, потому что сам он их не раскрывает. С `--render-only` рендерит и останавливается | compose (контейнер alertmanager), `deployment/check-monitoring.sh` | [MONITORING.md](../docs/guides/MONITORING.md#alerting) |
