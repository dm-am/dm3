# Установка и запуск DM3

Как поднять DM3 на своей машине.

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
powershell -File .\scripts\dm.ps1 help     # остальные команды

# Linux/Mac: те же команды через ./scripts/dm.sh

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

Справочные данные (доски форума, теги игр и прочее из `HasData` модели, каталоги в `InitialData`) приходят с миграцией: ее применяет разовый контейнер `migration` при подъеме стека.

Тестовых пользователей и контент кладет `seed`. Сид это консольный инструмент, а не эндпоинт: он пишет прямо в Postgres и объектное хранилище, поэтому по сети недоступен. Скрипт запускает его разовым контейнером под compose-профилем `tools`, который обычный `docker compose up` не поднимает. API при этом должен работать: после сида скрипт перезапускает API, чтобы тот пересчитал стартовые проекции (популярность, лучший пост недели) уже по данным.

Тестовые аккаунты по ролям, их общий пароль и аккаунты граничных случаев (имена по [USERNAME_POLICY.md](../conventions/USERNAME_POLICY.md), рейтинги, незавершенная регистрация) перечислены с назначением в `src/DM.Tools.Seeder/Seeding/DataSeeder.Users.cs`, удаленный аккаунт в `src/DM.Tools.Seeder/Seeding/DataSeeder.RemovedAccount.cs`.

Статус новичка после сида есть не у всех: рейтинг пересчитывается в конце прогона по фактическому числу постов. Порог описан в [AUTHORIZATION.md](../architecture/AUTHORIZATION.md).

## Команды

`reset` стирает тома стека (базы и хранилище) и поднимает его заново, `status` и `logs dm-api` показывают сервисы и лог. Что делает каждая команда, печатает `help`. Перечень всех скриптов репозитория, кто их зовет и где они описаны: [scripts/README.md](../../scripts/README.md).

`e2e` пересобирает и пересоздает только API с выключенным лимитом запросов, как оверлей джобы e2e в CI. Под лимитом одна загрузка страницы тратит больше десятка запросов из сотни в минуту, и набор падает на ответах 429, неотличимых от устаревших локаторов. Лимит возвращает следующий `start`. Когда хук перед пушем гонит e2e, сказано в [TESTING.md](./TESTING.md#хук-перед-пушем).

### Backend из исходников

```bash
docker stop dm-api
export $(grep '^DM_CryptoConfiguration__KeyBase64=' docker/.env)
dotnet run --project src/DM.Web.API --urls "http://localhost:5000" --environment Development
```

Окружение называется явно: учетные данные localhost лежат в `appsettings.Development.json`, а Production его не читает. Ключа шифрования нет ни в одном файле репозитория ([SECURITY.md](../conventions/SECURITY.md#требования-к-токенам)), вторая строка берет его из `docker/.env`, тот же, что у контейнера: с другим ключом сессии, выданные стендом, не расшифруются.

### Миграции

Миграция одна, `InitialCreate`, правило в [DATA_STORAGE.md](../conventions/DATA_STORAGE.md). После изменения сущностей ее пересоздают:

```bash
dotnet ef migrations remove -p src/DM.Infrastructure.Persistence -s src/DM.Web.API
dotnet ef migrations add InitialCreate -p src/DM.Infrastructure.Persistence -s src/DM.Web.API
```

`migrations add` в EF 10 падает с "Sequence contains no elements": `Boards.LastTopicId` и `Topics.BoardId` в сид-данных ссылаются друг на друга ([dotnet/efcore#36682](https://github.com/dotnet/efcore/issues/36682)). Обход: временно поставить `LastTopicId = null` в сиде досок в `src/DM.Infrastructure.Persistence/InitialData/BoardCatalog.cs`, сгенерировать миграцию, вернуть значение в сид и в три сгенерированных файла (InsertData доски "Общий" в миграции, `LastTopicId` в блоке HasData снапшота и дизайнера). Что три файла сошлись с моделью, проверяют MigrationShould и SnapshotAgreementShould.

## Конфигурация

Файлы настроек, секции `appsettings.json`, переменные `docker/.env` и `.env.local` клиента описаны в [CONFIGURATION.md](../references/CONFIGURATION.md).

## Частые проблемы

| Проблема | Решение |
|----------|---------|
| Frontend не видит API | Адрес API берется из `VITE_API_HOST` в `src/DM.Web.Client/.env.local`, без файла `http://localhost:5000`. API должен отвечать на http://localhost:5000/_health |
| Изображения не загружаются | API стартует и без хранилища, отказывают только загрузки и картинки. `dm-seaweedfs` в `docker ps` должен быть healthy, причину смотреть в `docker logs dm-seaweedfs` |
| Seed: "API not available" | Сначала `start` |

## Стенд preview

Стенд как на сервере, за nginx и Basic Auth:

```bash
cd docker
bash scripts/init-htpasswd.sh
docker compose -f docker-compose.yml -f docker-compose.preview.yml up -d --build
```

Адрес http://localhost, логин `preview`, пароль спрашивает первая команда. Без `docker/nginx/.htpasswd` docker создает на месте файла каталог, и nginx не поднимается. Подробности в [DEPLOYMENT.md](./DEPLOYMENT.md#стенд-preview).
