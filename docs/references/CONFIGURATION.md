# Конфигурация DM3

Где задается каждая настройка и какие переменные нужны развертыванию. Тест, который держит правило, назван у правила, остальное держит ревью.

## Где задаются значения

| Где | Что |
|-----|-----|
| `src/<хост>/appsettings.json` | Значения по умолчанию для localhost, без учетных данных |
| `src/<хост>/appsettings.Development.json` | Учетные данные localhost, в образ не попадает |
| `secrets/appsettings*.json` в каталоге хоста | Учетные данные файлом, в репозитории и в compose их нет |
| Переменные `DM_<Секция>__<Ключ>` | Перекрывают файлы, элемент массива `__0`. Порядок источников в `WebHostBuilderExtensions` |
| `docker/docker-compose.yml`, якорь `x-workload-env` | Переменные контейнеров приложения, порты сервисов |
| `docker/.env` | Пароли и ключи стека, создает `docker/scripts/init-env.sh` из `docker/.env.example` |
| `src/DM.Web.Client/.env.local` | `VITE_API_HOST` для клиента из исходников. В образе пуст: сайт и API на одном origin |

Правило о секретах и его довод в [SECURITY.md](../conventions/SECURITY.md#конвенция-конфигурации-и-секретов).

Учетные данные, без которых хост не стартует (`ConnectionStrings:Rdb`,
`CdnConfiguration:AccessKey` и `SecretKey`), лежат в `appsettings.Development.json`,
и Production этот файл не читает. Что учетных данных нет в `appsettings.json`,
держит [RequiredConfigurationShould](../../test/DM.Architecture.Tests/RequiredConfigurationShould.cs).
Запуск API из исходников описан в [LOCAL_SETUP.md](../guides/LOCAL_SETUP.md#backend-из-исходников).

Переменная из `docker/.env` доходит до контейнера, только если compose называет
ее в окружении сервиса: у приложения это якорь `x-workload-env` и оверлеи. Имя в
`docker/.env` часто другое (`SESSION_COOKIE_DOMAIN` для
`DM_SessionCookieConfiguration__Domain`). Что контейнер получил, показывает
`docker inspect dm-api`.

## Секции

Секция называется как ее класс, ключи и значения по умолчанию описаны в нем.
Одиночные ключи внизу таблицы код читает по имени.

| Секция или ключ | Что задает | Где правило |
|-----------------|------------|-------------|
| `ConnectionStrings` | `Rdb` PostgreSQL, `Logs` Loki, `TracingEndpoint` трассы | [MONITORING.md](../guides/MONITORING.md) |
| `CryptoConfiguration` | Ключ шифрования, его версия и прежние ключи на время смены | [SECURITY.md](../conventions/SECURITY.md#требования-к-токенам) |
| `AuthenticationConfiguration` | Сроки сессий, учет активности, задержка и блокировка входа | [AUTHENTICATION.md](../architecture/AUTHENTICATION.md#сессии) |
| `TwoFactorConfiguration` | Второй фактор: имя в приложении, окна, попытки, коды восстановления, отсрочка отключения | [AUTHENTICATION.md](../architecture/AUTHENTICATION.md#двухфакторная-аутентификация) |
| `TokenConfiguration` | Сроки жизни ссылок из писем | [SECURITY.md](../conventions/SECURITY.md#требования-к-токенам) |
| `PasswordPolicyConfiguration` | Требования к паролям | [SECURITY.md](../conventions/SECURITY.md#требования-к-паролям) |
| `SessionCookieConfiguration` | Область куки сессии | [Ниже](#область-куки-сессии) |
| `ReverseProxyConfiguration` | Доверенные края и их число для адреса посетителя | [POINT_OF_PRESENCE.md](../guides/POINT_OF_PRESENCE.md#адрес-посетителя-через-два-края) |
| `SiteAddressConfiguration` | `PublicUrl` для ссылок в письмах, `AdditionalOrigins` для dev-сервера клиента | [SYSTEM.md](../architecture/SYSTEM.md#адреса-сайта) |
| `CdnConfiguration` | Объектное хранилище S3 для загрузок | [DATA_STORAGE.md](../conventions/DATA_STORAGE.md#объектное-хранилище) |
| `GeoLocationConfiguration` | Таблица стран для письма о входе | [Ниже](#таблица-стран) |
| `BotConfiguration` | Боты уведомлений | [Ниже](#боты-уведомлений) |
| `EmailConfiguration` | Relay писем, вход на него, STARTTLS и отправитель | [MONITORING.md](../guides/MONITORING.md#alerting) |
| `MessagingConfiguration` | Срок правки своего сообщения | [AUTHORIZATION.md](../architecture/AUTHORIZATION.md#сроки-правки) |
| `RateLimiting:Enabled` | Выключатель лимита запросов | [API_DESIGN.md](../conventions/API_DESIGN.md#rate-limiting) |
| `Observability:MinimumLevel` | Уровень логов, в `docker/.env` это `LOG_MINIMUM_LEVEL` | [MONITORING.md](../guides/MONITORING.md#уровень) |
| `MigrateOnStart` | API накатывает миграцию и выходит (контейнер `migration`) | [DEPLOYMENT.md](../guides/DEPLOYMENT.md#обновление-стенда) |
| `SeedEpochUtc`, `SeedRandomSeed` | Время и зерно сида для повторимых кадров | [scripts/visual](../../scripts/visual/README.md) |

## Переменные развертывания

### Без значения в репозитории

Compose не поднимает стек без `DM_CryptoConfiguration__KeyBase64`
(`openssl rand -base64 32`). Его генерирует `init-env.sh`, его же вызывает
`scripts/dm.ps1`, это держит [EnvironmentFileShould](../../test/DM.Architecture.Tests/EnvironmentFileShould.cs).

### Область куки сессии

`DM_SessionCookieConfiguration__Domain`, пусто по умолчанию: кука остается на
выдавшем ее хосте. Регистрируемый домен делает вход общим для всех его хостов
([POINT_OF_PRESENCE.md](../guides/POINT_OF_PRESENCE.md#общий-вход)), и куку
получает любое приложение на любом из них. Поэтому заполнять, только когда все
хосты домена принадлежат этому приложению.

### Таблица стран

Письмо о входе с незнакомого устройства называет страну по таблице, которую API
скачивает сам, когда файла нет, и дальше раз в неделю.
`DM_GeoLocationConfiguration__CountryDatabasePath` это путь внутри контейнера
API, пусто выключает страну в письме. Каталог должен быть доступен API на запись
и переживать пересоздание контейнера (том `geodata` в
`docker/docker-compose.preview.yml`). `DM_GeoLocationConfiguration__CountryDatabaseUrl`
в compose не пробрасывается, по умолчанию это `user-country.csv` из
[sapics/ip-location-db](https://github.com/sapics/ip-location-db) под лицензией
PDDL 1.0 без требования ссылки на источник. Адрес только https, пусто выключает
загрузку, и файл читается как есть.

### Боты уведомлений

Переменные `DM_BotConfiguration__*` задаются в `docker/.env`, пустое значение
закрывает вебхук (`404`) и выключает отправку.

Вебхук Telegram регистрируется вместе с секретом:
`setWebhook?url=https://<сайт>/v1/webhooks/telegram&secret_token=<TelegramWebhookSecret>`.

Адрес `https://<сайт>/v1/webhooks/discord` вписывается в поле Interactions
Endpoint URL приложения Discord, `DiscordPublicKey` это Public Key (hex) со
страницы General Information. При сохранении Discord присылает PING и запросы с
испорченной подписью и ждет `200` и `401` соответственно. Значение, которое не
является ключом Ed25519, останавливает запуск API. Подпись принимается, пока ее
метка времени близка к часам сервера: при разошедшихся часах Discord получает
`401`, а в журнале остается строка о метке вне окна. Команда `/connect`
регистрируется один раз на приложение:
`PUT https://discord.com/api/v10/applications/{appId}/commands` со списком из
одной команды `connect` с обязательной строковой опцией `code` (type 3).

## Проверки здоровья

`/_health` отвечает проверке здоровья Docker и зависимостей не проверяет. `/_ready`
проверяет PostgreSQL, `/_health/detail` все зависимости, включая RabbitMQ и подписку
API на живые уведомления (`realtime-push`). `/metrics` отдает метрики Prometheus.
