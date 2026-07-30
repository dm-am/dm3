# Конфигурация DM3

> **User Story:** "Где найти настройки? Какой файл редактировать?"

---

## Источники истины (SSOT)

| Что | Где смотреть |
|-----|-------------|
| **Порты сервисов** | `docker/docker-compose.yml` |
| **Env vars** | `docker/docker-compose.yml` → секция `x-workload-env` |
| **API конфиг** | `src/DM.Web.API/appsettings.json` |
| **Docker секреты** | `docker/.env` (создать из `.env.example`) |

---

## Файлы конфигурации

### Backend

| Файл | Назначение |
|------|------------|
| `docker/.env` | Секреты Docker (пароли БД, Mongo, MinIO, RabbitMQ, ключ шифрования) |
| `src/DM.Web.API/appsettings.json` | Главный конфиг API |
| `src/DM.Workers.*/appsettings.json` | Конфиги workers |

**Что где лежит.** `appsettings.json` отслеживается гитом и содержит только
значения по умолчанию для localhost, чтобы свежий клон собирался и запускался.
Настоящие учетные данные приходят переменными `DM_*`, и у них нет дефолта в
репозитории: отсутствие валит старт, а не подставляет значение, которое может
прочитать кто угодно. Полное правило — в [SECURITY.md](../conventions/SECURITY.md).

**Обязательные переменные без дефолта:**

| Переменная | Как получить |
|------------|--------------|
| `DM_CryptoConfiguration__KeyBase64` | `openssl rand -base64 32`. Одно значение на все зеркала. `dm.ps1` генерирует локальный ключ сам |
| `MONGO_ROOT_PASSWORD`, `MONGO_PASSWORD` | Задать в `docker/.env`. Меняются только вместе с пересозданием тома Mongo |

### Frontend

| Файл | Назначение |
|------|------------|
| `src/DM.Web.Client/.env.local` | API URL |

---

## Быстрые ссылки

| Нужно | Документ |
|-------|----------|
| Запустить локально | [LOCAL_SETUP.md](../guides/LOCAL_SETUP.md) |
| Настроить зеркало | [MIRRORING.md](../guides/MIRRORING.md) |
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
| `ImageProxyConfiguration` | imgproxy endpoint + URL signing | [UPLOADS.md](../architecture/UPLOADS.md) |
| `MirrorConfiguration` | Зеркала | [MIRRORING.md](../guides/MIRRORING.md) |

---

## Health endpoints

| Endpoint | Назначение |
|----------|-----------|
| `/_health` | Liveness (Docker health check) |
| `/_ready` | Readiness (PostgreSQL + MongoDB) |
| `/_health/detail` | Детальная информация |

---

## Команды

```bash
# Посмотреть все порты
docker ps --format "table {{.Names}}\t{{.Ports}}"

# Проверить env vars контейнера
docker inspect dm-api | jq '.[0].Config.Env'
```
