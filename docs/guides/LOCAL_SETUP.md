# Установка и запуск DM3

Как поднять DM3 на своей машине. То, что каждая команда отсюда существует, держит DocumentedCommandsShould, согласие трех файлов миграции после пересоздания держат MigrationShould и SnapshotAgreementShould, остальное не проверяется.

## Требования

- Docker Desktop, на Windows с WSL2.
- Git. На Windows из Git for Windows берется и bash: `dm.ps1` зовет им `docker/scripts/init-env.sh`.
- Node.js и npm ровно тех версий, что в `engines` файла [package.json](../../src/DM.Web.Client/package.json) клиента. Версия точная, а не нижняя граница: lock-файл пишет npm, приехавшая с этой Node, и `npm ci` другой версии его отвергает. Ту же версию называют Dockerfile клиента и CI, равенство держит FrontendToolchainShould.
- .NET SDK по [global.json](../../global.json) и глобальный инструмент `dotnet-ef`. Нужны только для запуска API из исходников и для миграций.

## Быстрый старт

```bash
# Windows
powershell -File .\scripts\dm.ps1 start
powershell -File .\scripts\dm.ps1 seed

# Linux/Mac
./scripts/dm.sh start
./scripts/dm.sh seed

# Frontend, отдельно
cd src/DM.Web.Client && npm install && npm run dev
```

`start` сам создает `docker/.env` скриптом `docker/scripts/init-env.sh local`. Копия `docker/.env.example` руками не годится: ключ шифрования в ней пуст, и compose останавливается на интерполяции до старта контейнеров.

- Frontend: http://localhost:5173. Vite занимает строго этот порт и при занятом не стартует.
- API и Swagger: http://localhost:5000
- Mailpit с письмами активации: http://localhost:8025
- Объектное хранилище (S3): http://localhost:9000, учетки в `docker/.env`. Веб-консоли у хранилища нет.
- Остальные порты: в [docker-compose.yml](../../docker/docker-compose.yml) или `dm.ps1 status`. Grafana, Jaeger и Prometheus описаны в [MONITORING.md](./MONITORING.md).

Все пароли в `docker/.env`.

## Тестовые данные

Справочные данные (доски форума, теги игр и прочее из `HasData` в `DmDbContext`) приходят с миграцией: ее применяет разовый контейнер `migration` при подъеме стека.

Тестовых пользователей и контент кладет `seed`. Сид это консольный инструмент, а не эндпоинт: он пишет прямо в Postgres и объектное хранилище, поэтому по сети недоступен. Скрипт запускает его разовым контейнером под compose-профилем `tools`, который обычный `docker compose up` не поднимает. API при этом должен работать: после сида скрипт перезапускает API, чтобы тот пересчитал стартовые проекции (популярность, лучший пост недели) уже по данным.

Пароль всех тестовых аккаунтов `Test123!`.

| Роль | Логин |
|------|-------|
| Admin | `SolohinLex` |
| SeniorModerator | `TestSeniorMod` |
| Moderator | `TestModerator` |
| Mentor | `TestMentor` |
| RegularUser | `TestUser` |

Остальные аккаунты (граничные случаи имен по [USERNAME_POLICY.md](../conventions/USERNAME_POLICY.md), рейтинги, удаленный аккаунт, незавершенная регистрация `inactive@test.local`) перечислены с назначением в `src/DM.Tools.Seeder/Seeding/DataSeeder.Users.cs` и `src/DM.Tools.Seeder/Seeding/DataSeeder.RemovedAccount.cs`.

Статус новичка после сида есть не у всех: рейтинг пересчитывается в конце прогона по фактическому числу постов. Порог описан в [AUTHORIZATION.md](../architecture/AUTHORIZATION.md).

## Команды

```bash
# Windows
powershell -File .\scripts\dm.ps1 start
powershell -File .\scripts\dm.ps1 stop
powershell -File .\scripts\dm.ps1 reset
powershell -File .\scripts\dm.ps1 seed
powershell -File .\scripts\dm.ps1 e2e
powershell -File .\scripts\dm.ps1 status
powershell -File .\scripts\dm.ps1 logs dm-api

# Linux/Mac: те же команды
./scripts/dm.sh help
```

`reset` стирает тома стека (базы и хранилище) и поднимает его заново. Что делает каждая команда, печатает `help`.

`e2e` пересобирает и пересоздает только API с выключенным лимитом запросов, как оверлей джобы e2e в CI. Под лимитом одна загрузка страницы тратит больше десятка запросов из сотни в минуту, и набор падает на ответах 429, неотличимых от устаревших локаторов. Лимит возвращает следующий `start`. Хук перед пушем гонит набор только с `DM_GATES_FULL=1` и тогда сначала проверяет стенд: без выключателя отказывает и называет эту команду.

### Backend из исходников

```bash
docker stop dm-api
dotnet run --project src/DM.Web.API --urls "http://localhost:5000" --environment Development
```

Без явного окружения хост не стартует, причина в [CONFIGURATION.md](../references/CONFIGURATION.md).

### Миграции

Миграция одна, `InitialCreate`, правило в [DATA_STORAGE.md](../conventions/DATA_STORAGE.md). После изменения сущностей ее пересоздают:

```bash
dotnet ef migrations remove -p src/DM.Infrastructure.Persistence -s src/DM.Web.API
dotnet ef migrations add InitialCreate -p src/DM.Infrastructure.Persistence -s src/DM.Web.API
```

`migrations add` в EF 10 падает с "Sequence contains no elements". Это замаскированное обнаружение цикла в сид-данных: `Boards.LastTopicId` и `Topics.BoardId` ссылаются друг на друга, EF 8 разрывал nullable-ребро сам, EF 10 отказывается ([dotnet/efcore#36682](https://github.com/dotnet/efcore/issues/36682)). Обход: временно поставить `LastTopicId = null` в сиде досок в `DmDbContext`, сгенерировать миграцию, вернуть значение в сид и в три сгенерированных файла (InsertData доски "Общий" в миграции, `LastTopicId` в блоке HasData снапшота и дизайнера). Что три файла сошлись с моделью, проверяют MigrationShould и SnapshotAgreementShould.

## Конфигурация

Файлы настроек, секции `appsettings.json`, переменные `docker/.env` и `.env.local` клиента описаны в [CONFIGURATION.md](../references/CONFIGURATION.md).

## Частые проблемы

| Проблема | Решение |
|----------|---------|
| Frontend не видит API | Адрес API берется из `VITE_API_HOST` в `src/DM.Web.Client/.env.local`, без файла `http://localhost:5000`. API должен отвечать на http://localhost:5000/_health |
| Изображения не загружаются | API стартует и без хранилища, отказывают только загрузки и картинки. `dm-seaweedfs` в `docker ps` должен быть healthy, причину смотреть в `docker logs dm-seaweedfs` |
| Превью картинок 404 от imgproxy | Жив ли `dm-imgproxy` в `docker ps`, заданы ли `IMGPROXY_KEY` и `IMGPROXY_SALT` в `docker/.env` (64 hex-символа каждый) |
| Seed: "API not available" | Сначала `start` |

## Preview режим

Стенд как на сервере, за nginx и Basic Auth:

```bash
cd docker
bash scripts/init-htpasswd.sh
docker compose -f docker-compose.yml -f docker-compose.preview.yml up -d --build
```

Адрес http://localhost, логин `preview`, пароль спрашивает первая команда. Без `docker/nginx/.htpasswd` docker создает на месте файла каталог, и nginx не поднимается. Подробности в [DEPLOYMENT.md](./DEPLOYMENT.md#preview-окружение).
