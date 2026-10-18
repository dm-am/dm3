# Конфигурация DM3

Где задается каждая настройка и какие переменные нужны развертыванию. Содержание с кодом не сверяется, знаки и пути держат `DocumentPunctuationShould` и `DocumentedPathsShould`.

## Источники истины (SSOT)

| Что | Где |
|-----|-----|
| Порты сервисов | `docker/docker-compose.yml` |
| Переменные окружения контейнеров | `docker/docker-compose.yml`, якорь `x-workload-env` |
| Конфиг API | `src/DM.Web.API/appsettings.json` |
| Секреты Docker | `docker/.env`, его создает `docker/scripts/init-env.sh` из `docker/.env.example` |

## Файлы конфигурации

### Backend

| Файл | Назначение |
|------|------------|
| `docker/.env` | Пароли и ключи стека |
| `src/DM.Web.API/appsettings.json` | Главный конфиг API |
| `src/DM.Workers.*/appsettings.json` | Конфиги воркеров |
| `src/*/appsettings.Development.json` | Учетные данные localhost для запуска из исходников |

В `appsettings.json` только значения по умолчанию для localhost, настоящие
учетные данные приходят переменными `DM_*` без дефолта в репозитории. Правило и
довод в [SECURITY.md](../conventions/SECURITY.md#конвенция-конфигурации-и-секретов).

Ключи, без которых хост не стартует (`ConnectionStrings:Rdb`,
`CdnConfiguration:AccessKey` и `SecretKey`), лежат в
`appsettings.Development.json`, а не в `appsettings.json`. В Production хост этот
файл не читает и в образ он не попадает, поэтому проверка на старте срабатывает
там, где нужна. Запуск из исходников с явным окружением:
`dotnet run --project src/DM.Web.API --environment Development`.

Переменная из `docker/.env` доходит до контейнера, только если названа в
`x-workload-env`: якорь перечисляет переменные явно. Имя в `docker/.env` часто
другое (`SESSION_COOKIE_DOMAIN` для `DM_SessionCookieConfiguration__Domain`),
соответствие видно в самом якоре.

### Обязательные переменные без дефолта

Без них compose не поднимает стек. Все три генерирует `init-env.sh`, его же
вызывает `scripts/dm.ps1`.

| Переменная | Как получить |
|------------|--------------|
| `DM_CryptoConfiguration__KeyBase64` | `openssl rand -base64 32`. Живет только на экземпляре приложения, довод в [SECURITY.md](../conventions/SECURITY.md#требования-к-токенам) |
| `IMGPROXY_KEY`, `IMGPROXY_SALT` | `openssl rand -hex 32`. Секция `ImageProxyConfiguration` (адрес imgproxy и подпись URL, [UPLOADS.md](../architecture/UPLOADS.md)) приходит в API только переменными `DM_ImageProxyConfiguration__*` из `docker-compose.yml`, в `appsettings.json` ее нет |

### Настройки развертывания

| Переменная | Что задает |
|------------|-----------|
| `DM_SessionCookieConfiguration__Domain` | Область куки сессии. Пусто (дефолт): кука остается на выдавшем ее хосте. Регистрируемый домен делает вход общим для всех его хостов, поэтому заполнять только когда все они принадлежат этому приложению ([POINT_OF_PRESENCE.md](../guides/POINT_OF_PRESENCE.md#общий-вход)) |
| `DM_GeoLocationConfiguration__CountryDatabasePath` | Путь внутри контейнера API к таблице стран, по которой письмо о входе с незнакомого устройства называет страну. Пусто: страны в письме нет и ничего не скачивается |
| `DM_GeoLocationConfiguration__CountryDatabaseUrl` | Откуда API скачивает таблицу, только https. В compose не пробрасывается, действует дефолт: `user-country.csv` из [sapics/ip-location-db](https://github.com/sapics/ip-location-db), релиз latest, лицензия PDDL 1.0 без требования ссылки на источник. Пусто: API ничего не скачивает и читает файл по пути как есть |

Таблицу стран руками никто не кладет: API скачивает ее сам, когда файла нет, и
дальше раз в неделю от даты файла, без перезапуска. Неудачная загрузка оставляет
прежнюю таблицу и повторяется через час. Пока таблицы нет, письмо выходит без
строки "Страна", старт не падает. Адрес читателя ищется только в локальной копии.
Каталог должен быть доступен API на запись и переживать пересоздание контейнера:
`docker/docker-compose.preview.yml` кладет файл на именованный том `geodata`.
Проверка копии и расписание описаны в `CountryTableRefresher`.

### Боты уведомлений (секция `BotConfiguration`)

Задаются в `docker/.env`, пустое значение оставляет функцию выключенной.

| Переменная | Что задает |
|------------|-----------|
| `DM_BotConfiguration__TelegramBotToken` | Токен бота от @BotFather, им отправляются уведомления |
| `DM_BotConfiguration__TelegramWebhookSecret` | Секрет входящего вебхука. Регистрируется вместе с адресом: `setWebhook?url=https://<сайт>/v1/webhooks/telegram&secret_token=<секрет>`, Telegram присылает его в заголовке каждого вызова. Не задан: вебхук отвечает `404` |
| `DM_BotConfiguration__DiscordBotToken` | Токен бота из Discord Developer Portal, им отправляются уведомления |
| `DM_BotConfiguration__DiscordPublicKey` | Public Key приложения (hex) со страницы General Information в Discord Developer Portal, им проверяется подпись Ed25519 каждой интеракции. Не задан: вебхук отвечает `404` |

Входящая точка Discord `https://<сайт>/v1/webhooks/discord` вписывается в поле
Interactions Endpoint URL приложения. При сохранении Discord ее проверяет:
присылает PING и запросы с испорченной подписью и ждет `200` и `401`
соответственно. Slash-команда `/connect` регистрируется один раз на приложение:
`PUT https://discord.com/api/v10/applications/{appId}/commands` со списком из
одной команды `connect` и обязательной строковой опцией `code` (type 3).

### Frontend

| Файл | Назначение |
|------|------------|
| `src/DM.Web.Client/.env.local` | `VITE_API_HOST`, адрес API при запуске из исходников. В развернутом виде пуст: сайт и API отвечают с одного origin |

## Ключевые секции appsettings.json

| Секция | Что настраивает | Документация |
|--------|-----------------|--------------|
| `AuthenticationConfiguration` | Сроки сессий, учет активности и попыток входа | [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| `PasswordPolicyConfiguration` | Требования к паролям | [SECURITY.md](../conventions/SECURITY.md#требования-к-паролям) |
| `TokenConfiguration` | Сроки жизни ссылок из писем | |
| `CdnConfiguration` | Объектное хранилище S3 для загрузок | [UPLOADS.md](../architecture/UPLOADS.md) |

## Health endpoints

| Endpoint | Назначение |
|----------|-----------|
| `/_health` | Liveness для health check Docker, зависимости не проверяет |
| `/_ready` | Readiness, PostgreSQL |
| `/_health/detail` | Все проверки: PostgreSQL, RabbitMQ и подписка API на очередь живых уведомлений (`realtime-push`, Degraded, пока этот процесс ее ни разу не открыл) |
| `/metrics` | Метрики в формате Prometheus |

## Команды

```bash
# Посмотреть все порты
docker ps --format "table {{.Names}}\t{{.Ports}}"

# Проверить env vars контейнера
docker inspect dm-api | jq '.[0].Config.Env'
```

## Ссылки

- [LOCAL_SETUP.md](../guides/LOCAL_SETUP.md): запуск локально
- [POINT_OF_PRESENCE.md](../guides/POINT_OF_PRESENCE.md): точка присутствия
- [DEPLOYMENT.md](../guides/DEPLOYMENT.md): деплой на VPS
