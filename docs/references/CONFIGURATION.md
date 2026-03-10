# Конфигурация DM3

> **User Story:** "Какие настройки? Где найти env vars? Какие порты?"

---

## Файлы конфигурации

| Файл | Назначение |
|------|------------|
| `docker/.env` | Секреты Docker (пароли БД, MinIO, RabbitMQ) |
| `src/DM.Web.API/appsettings.json` | Главный конфиг API |
| `src/DM.Web.Client/.env.local` | Frontend (API URL) |

### Workers

| Файл | Назначение |
|------|------------|
| `src/DM.Workers.Mail/appsettings.json` | SMTP настройки |
| `src/DM.Workers.SearchIndexer/appsettings.json` | OpenSearch подключение |
| `src/DM.Workers.NotificationDispatcher/appsettings.json` | MongoDB, RabbitMQ |

---

## Порты сервисов

**Источник истины:** `docker/docker-compose.yml`

| Сервис | Порт | Credentials |
|--------|------|-------------|
| Frontend (Vite) | 5173 | — |
| API (Swagger) | 5000 | — |
| Search Consumer | 5001 | — |
| Notification Consumer | 5002 | — |
| Email Consumer | 5003 | — |
| PostgreSQL | 5432 | из `docker/.env` |
| MongoDB | 27017 | — |
| RabbitMQ | 5672, 15672 | из `docker/.env` |
| MinIO | 9000, 9001 | из `docker/.env` |
| MailHog | 1025, 8025 | — |
| OpenSearch | 9200, 5601 | — |
| Jaeger | 16686 | — |
| Prometheus | 9090 | — |
| Grafana | 3000 | из `docker/.env` |

**Команда для просмотра:** `docker ps --format "table {{.Names}}\t{{.Ports}}"`

---

## Переменные окружения

**Источник истины:** `docker/docker-compose.yml` (секция `x-workload-env`)

### Базы данных

| Переменная | Описание |
|------------|----------|
| `DM_ConnectionStrings__Rdb` | PostgreSQL connection string |
| `DM_ConnectionStrings__Mongo` | MongoDB connection string |

### RabbitMQ

| Переменная | Описание |
|------------|----------|
| `DM_RabbitMqConfiguration__Host` | Хост RabbitMQ |
| `DM_RabbitMqConfiguration__Port` | Порт RabbitMQ |
| `DM_RabbitMqConfiguration__Username` | Пользователь |
| `DM_RabbitMqConfiguration__Password` | Пароль |

### MinIO / S3

| Переменная | Описание |
|------------|----------|
| `DM_CdnConfiguration__Endpoint` | MinIO endpoint |
| `DM_CdnConfiguration__AccessKey` | Access key |
| `DM_CdnConfiguration__SecretKey` | Secret key |
| `DM_CdnConfiguration__BucketName` | Название bucket |

### Email

| Переменная | Описание |
|------------|----------|
| `DM_EmailConfiguration__Host` | SMTP хост |
| `DM_EmailConfiguration__Port` | SMTP порт |
| `DM_EmailConfiguration__Username` | Пользователь |
| `DM_EmailConfiguration__Password` | Пароль |

### Криптография

| Переменная | Описание |
|------------|----------|
| `DM_CryptoConfiguration__KeyBase64` | AES-256 ключ (критично для зеркал!) |

---

## Секции appsettings.json

### AuthenticationConfiguration

| Параметр | Значение | Описание |
|----------|----------|----------|
| `SessionDurationHours` | 8760 | Сессия (remember me) |
| `ShortSessionDurationHours` | 24 | Сессия (без remember me) |
| `ThrottlingDelayMilliseconds` | 100 | Задержка brute-force |
| `LockoutThreshold` | 15 | Попыток до блокировки |
| `LockoutDurationMinutes` | 30 | Длительность блокировки |

### PasswordPolicyConfiguration

| Параметр | Значение |
|----------|----------|
| `MinimumLength` | 8 |
| `MaximumLength` | 128 |
| `RequireUppercase` | false |
| `RequireLowercase` | false |
| `RequireDigit` | false |
| `RequireSpecialCharacter` | false |

### TokenConfiguration

| Параметр | Значение | Описание |
|----------|----------|----------|
| `ActivationTokenLifetimeHours` | 48 | Срок активации |
| `PasswordResetTokenLifetimeHours` | 24 | Срок сброса пароля |
| `InvitationTokenLifetimeDays` | 7 | Срок приглашения |

### CdnConfiguration

| Параметр | Описание |
|----------|----------|
| `Endpoint` | URL MinIO/S3 |
| `AccessKey` | Ключ доступа |
| `SecretKey` | Секретный ключ |
| `BucketName` | dm-uploads |
| `PublicUrl` | Публичный URL для файлов |

### MirrorConfiguration

| Параметр | Описание |
|----------|----------|
| `Mirrors` | Массив зеркал |
| `Mirrors[].Id` | Уникальный ID |
| `Mirrors[].Url` | URL зеркала |
| `Mirrors[].Name` | Название для UI |

---

## Конфигурация зеркала

### .env.mirror

```bash
# Те же что на main сервере
POSTGRES_PASSWORD=...
RABBITMQ_DEFAULT_PASS=...
MINIO_ROOT_PASSWORD=...
DM_CryptoConfiguration__KeyBase64=...  # КРИТИЧНО: тот же!

# Специфичные для зеркала
MIRROR_ID=ru
DB_HOST=main-server-ip
MONGO_HOST=main-server-ip
RABBITMQ_HOST=main-server-ip
MINIO_HOST=main-server-ip

# SSL (опционально)
DB_SSL_MODE=;SSL Mode=Require
MONGO_TLS=&tls=true
```

### Firewall на main сервере

```bash
ufw allow from <mirror-ip> to any port 5432  # PostgreSQL
ufw allow from <mirror-ip> to any port 27017 # MongoDB
ufw allow from <mirror-ip> to any port 5672  # RabbitMQ
ufw allow from <mirror-ip> to any port 9000  # MinIO
```

---

## Frontend

### .env.local

```bash
VITE_API_HOST=http://localhost:5000
```

### Production

```bash
VITE_API_HOST=https://api.dm.am
```

---

## Docker secrets

Создать из `.env.example`:

```bash
cd docker
cp .env.example .env
# Заполнить:
POSTGRES_PASSWORD=...
RABBITMQ_DEFAULT_PASS=...
MINIO_ROOT_PASSWORD=...
GF_SECURITY_ADMIN_PASSWORD=...
```

---

## Health endpoints

| Endpoint | Назначение |
|----------|-----------|
| `/_health` | Liveness (Docker health check) |
| `/_ready` | Readiness (PostgreSQL + MongoDB + RabbitMQ) |
| `/_health/detail` | Детальная информация |

---

## Ссылки

- [LOCAL_SETUP.md](../guides/LOCAL_SETUP.md) — локальный запуск
- [DEPLOYMENT.md](../guides/DEPLOYMENT.md) — деплоймент
- [MIRRORING.md](../guides/MIRRORING.md) — настройка зеркал
