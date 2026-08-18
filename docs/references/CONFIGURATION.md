# Конфигурация DM3

> **User Story:** "Где найти настройки? Какой файл редактировать?"

---

## Источники истины (SSOT)

| Что | Где смотреть |
|-----|-------------|
| **Порты сервисов** | `docker/docker-compose.yml` |
| **Env vars** | `docker/docker-compose.yml` → секция `x-workload-env` |
| **API конфиг** | `src/DM.Web.API/appsettings.json` |
| **Docker секреты** | `docker/.env` (создает `docker/scripts/init-env.sh`) |

---

## Файлы конфигурации

### Backend

| Файл | Назначение |
|------|------------|
| `docker/.env` | Секреты Docker (пароли БД, Mongo, MinIO, RabbitMQ, ключ шифрования) |
| `src/DM.Web.API/appsettings.json` | Главный конфиг API |
| `src/DM.Workers.*/appsettings.json` | Конфиги workers |
| `src/*/appsettings.Development.json` | Учетные данные localhost для запуска из исходников |

**Что где лежит.** `appsettings.json` отслеживается гитом и содержит только
значения по умолчанию для localhost. Настоящие учетные данные приходят
переменными `DM_*`, и у них нет дефолта в репозитории: отсутствие валит старт, а
не подставляет значение, которое может прочитать кто угодно. Полное правило — в
[SECURITY.md](../conventions/SECURITY.md).

Ключи, без которых хост отказывается стартовать (`ConnectionStrings:Rdb`,
`ConnectionStrings:Mongo`, `CdnConfiguration:AccessKey` и `SecretKey`), лежат не
в `appsettings.json`, а в `appsettings.Development.json`. Хост в Production этот
файл не читает, и в образ он не попадает, поэтому проверка на старте срабатывает
там, где она нужна. Запуск из исходников — с явным окружением:
`dotnet run --project src/DM.Web.API --environment Development`.

**Обязательные переменные без дефолта:**

| Переменная | Как получить |
|------------|--------------|
| `DM_CryptoConfiguration__KeyBase64` | `openssl rand -base64 32`. Одно значение на все экземпляры приложения: им шифруются одноразовые ссылки из писем, а письмо, выданное под одним адресом сайта, открывают по другому, и расшифровать токен обязана принимающая сторона. `dm.ps1` генерирует локальный ключ сам |
| `MONGO_ROOT_PASSWORD`, `MONGO_PASSWORD` | Задать в `docker/.env`. Меняются только вместе с пересозданием тома Mongo |
| `IMGPROXY_KEY`, `IMGPROXY_SALT` | `openssl rand -hex 32`, генерирует `init-env.sh` в `docker/.env`. Вся секция `ImageProxyConfiguration` (imgproxy endpoint + URL signing, [UPLOADS.md](../architecture/UPLOADS.md)) приходит в API только переменными `DM_ImageProxyConfiguration__*` из `docker-compose.yml`, в `appsettings.json` ее нет |

**Настройки развертывания, а не кода:**

| Переменная | Что задает |
|------------|-----------|
| `DM_SessionCookieConfiguration__Domain` | Область куки сессии. Пусто — кука остается на выдавшем ее хосте, и это дефолт для локальной разработки и стенда. Регистрируемый домен — вход становится общим для всех его хостов, поэтому заполнять его можно только когда все они принадлежат этому приложению ([POINT_OF_PRESENCE.md](../guides/POINT_OF_PRESENCE.md)) |

**Боты уведомлений (секция `BotConfiguration`):**

Задаются в `docker/.env` и доходят до контейнеров одноименными переменными:
якорь `x-workload-env` в `docker-compose.yml` пробрасывает явный список, и
переменная вне его до API не дойдет. Пустое значение оставляет функцию
выключенной.

| Переменная | Что задает |
|------------|-----------|
| `DM_BotConfiguration__TelegramBotToken` | Токен бота от @BotFather, им отправляются уведомления |
| `DM_BotConfiguration__TelegramWebhookSecret` | Секрет входящего вебхука. Регистрируется вместе с адресом: `setWebhook?url=https://<сайт>/v1/webhooks/telegram&secret_token=<секрет>`, Telegram присылает его в заголовке каждого вызова. Не задан — вебхук отвечает `404` |
| `DM_BotConfiguration__DiscordBotToken` | Токен бота из Discord Developer Portal, им отправляются уведомления |
| `DM_BotConfiguration__DiscordPublicKey` | Public Key приложения (hex) со страницы General Information в Discord Developer Portal. Им проверяется подпись Ed25519 каждой интеракции. Не задан — вебхук отвечает `404` |

Входящая точка Discord — `https://<сайт>/v1/webhooks/discord`. Этот адрес
вписывается в поле Interactions Endpoint URL приложения в Discord Developer
Portal, и при сохранении Discord проверяет его: присылает PING и запросы с
намеренно испорченной подписью, ожидая `200` и `401` соответственно.
Slash-команда `/connect` регистрируется один раз на приложение:
`PUT https://discord.com/api/v10/applications/{appId}/commands` со списком из
одной команды `connect` и обязательной строковой опцией `code` (type 3).

### Frontend

| Файл | Назначение |
|------|------------|
| `src/DM.Web.Client/.env.local` | Адрес API при запуске из исходников. В развернутом виде пуст: сайт и API отвечают с одного origin |

---

## Быстрые ссылки

| Нужно | Документ |
|-------|----------|
| Запустить локально | [LOCAL_SETUP.md](../guides/LOCAL_SETUP.md) |
| Поднять точку присутствия | [POINT_OF_PRESENCE.md](../guides/POINT_OF_PRESENCE.md) |
| Деплой на VPS | [DEPLOYMENT.md](../guides/DEPLOYMENT.md) |
| Параметры аутентификации | [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |

---

## Ключевые секции appsettings.json

| Секция | Что настраивает | Документация |
|--------|-----------------|--------------|
| `AuthenticationConfiguration` | Сессии, throttling, lockout | [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| `PasswordPolicyConfiguration` | Требования к паролям | [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| `TokenConfiguration` | Сроки жизни токенов | [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| `CdnConfiguration` | MinIO/S3 source storage | [UPLOADS.md](../architecture/UPLOADS.md) |

---

## Health endpoints

| Endpoint | Назначение |
|----------|-----------|
| `/_health` | Liveness (Docker health check) |
| `/_ready` | Readiness (PostgreSQL + MongoDB) |
| `/_health/detail` | Детальная информация |
| `/metrics` | Метрики в формате Prometheus |

---

## Команды

```bash
# Посмотреть все порты
docker ps --format "table {{.Names}}\t{{.Ports}}"

# Проверить env vars контейнера
docker inspect dm-api | jq '.[0].Config.Env'
```
