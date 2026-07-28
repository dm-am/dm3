# Установка и запуск DM3

## Требования

- **Docker Desktop** (с WSL2 на Windows)
- **Node.js 20+** и **npm**
- **Git**

---

## Быстрый старт

```bash
# Windows (PowerShell)
.\scripts\dm.ps1 start                        # Запуск всех сервисов
.\scripts\dm.ps1 seed                         # Тестовые данные (после запуска API)

# Linux/Mac
./scripts/dm.sh start
./scripts/dm.sh seed

# Frontend (отдельно)
cd src/DM.Web.Client && npm install && npm run dev
```

### Альтернатива (ручной запуск)

```bash
cd docker
cp .env.example .env                          # Создать файл с секретами
docker compose up -d                          # Инфраструктура

cd src/DM.Web.Client && npm install && npm run dev  # Frontend
```

- **Frontend:** http://localhost:5173
- **API/Swagger:** http://localhost:5000
- **Health:** http://localhost:5000/_health (liveness), http://localhost:5000/_ready (readiness)
- **MinIO:** http://localhost:9001 (credentials из `docker/.env`) — bucket `dm-uploads` создается автоматически при первом старте API
- **imgproxy:** http://localhost:8080 (on-the-fly resize + AVIF/WebP negotiation)

---

## Порты сервисов

**Источник истины:** [`docker/docker-compose.yml`](../../docker/docker-compose.yml)

| Сервис | Порт | Credentials |
|--------|------|-------------|
| Frontend (Vite) | 5173 | — |
| API (Swagger) | 5000 | — |
| Search Consumer | 5001 (gRPC), 5101 (health, метрики) | — |
| Notification Consumer | 5002 | — |
| Email Consumer | 5003 | — |
| PostgreSQL | 5432 | из `docker/.env` |
| MongoDB | 27017 | — |
| RabbitMQ | 5672, 15672 | из `docker/.env` |
| MinIO | 9000, 9001 | из `docker/.env` |
| imgproxy | 8080 | HMAC key/salt из `docker/.env` |
| MailHog | 1025, 8025 | — |
| OpenSearch | 9200, 5601 | — |
| Jaeger | 16686 | — |
| Prometheus | 9090 | — |
| Grafana | 3000 | из `docker/.env` |

**Credentials:** Все пароли в `docker/.env` (создать из `docker/.env.example`).

**Все порты:** `docker ps --format "table {{.Names}}\t{{.Ports}}"`

---

## Тестовые данные

### Справочные данные (автоматически)

При запуске API EF Core применяет миграции, которые создают:
- **Доски форума** — 11 разделов
- **Теги игр** — 65 тегов в 8 группах

### Тестовые пользователи

```bash
.\scripts\dm.ps1 seed   # Windows
./scripts/dm.sh seed    # Linux/Mac
```

**Требования:** API запущен (порт 5000).

Скрипт вызывает `POST /v1/moderation/seed` напрямую через curl (доступен только в Development).

**Тестовые аккаунты (пароль: `Test123!`):**

**Роли:**

| Роль | Логин | Email |
|------|-------|-------|
| Admin | `SolohinLex` | admin@test.local |
| SeniorModerator | `TestSeniorMod` | seniormod@test.local |
| Moderator | `TestModerator` | mod@test.local |
| Mentor | `TestMentor` | mentor@test.local |
| RegularUser | `TestUser` | user@test.local |

**Граничные случаи имен (см. [USERNAME_POLICY.md](../conventions/USERNAME_POLICY.md)):**

| Логин | Особенность |
|-------|-------------|
| `Ян` | Min length (2), cyrillic |
| `LongestLoginPossible` | Max length (20) |
| `Player_One` | Underscore |
| `Player-Two` | Hyphen |
| `Player.Three` | Dot |
| `Player Four` | Space |
| `Игрок` | Cyrillic only |
| `Игрок_Один` | Cyrillic + underscore |
| `Тест Елки` | Cyrillic + space + Е |

**Специальные состояния:**

| Логин | Особенность |
|-------|-------------|
| `TestHonorary` | Обладатель награды "Почетный гоблин" |

**Pending Registrations (для тестирования активации):**

| Email | Назначение |
|-------|------------|
| inactive@test.local | Тестирование flow "регистрация не завершена" |

Все пользователи начинают с 0 постов (статус "новичок").

### Ручная регистрация

1. http://localhost:5173 → Регистрация
2. MailHog: http://localhost:8025 (письмо активации)

---

## Команды

### CLI-скрипты (рекомендуется)

```bash
# Windows (PowerShell)
.\scripts\dm.ps1 start    # Запуск всех сервисов
.\scripts\dm.ps1 stop     # Остановка
.\scripts\dm.ps1 reset    # Сброс БД и перезапуск
.\scripts\dm.ps1 seed     # Тестовые данные
.\scripts\dm.ps1 status   # Статус сервисов
.\scripts\dm.ps1 logs     # Логи (или logs dm-api)

# Linux/Mac
./scripts/dm.sh start
./scripts/dm.sh stop
./scripts/dm.sh reset
./scripts/dm.sh seed
./scripts/dm.sh status
./scripts/dm.sh logs
```

### Docker (ручные команды)

```bash
docker compose up -d --build          # Запуск с пересборкой
docker compose down                   # Остановка
docker logs dm-api --tail 50 -f       # Логи API
docker exec -it dm-api /bin/sh        # Shell в контейнере
```

### Frontend

```bash
cd src/DM.Web.Client
npm run dev           # Dev server
npm run build         # Production
npm run test:unit     # Тесты
```

### Backend (локальная разработка)

```bash
docker stop dm-api
dotnet run --project src/DM.Web.API --urls "http://localhost:5000"
```

### Миграции

Только одна миграция `InitialCreate` (см. [DATA_STORAGE.md](../conventions/DATA_STORAGE.md)). После изменения Entity — пересоздать ее, а не добавлять новую:

```bash
dotnet ef migrations remove -p src/DM.Infrastructure.Persistence -s src/DM.Web.API
dotnet ef migrations add InitialCreate -p src/DM.Infrastructure.Persistence -s src/DM.Web.API
```

---

## Конфигурация

### Файлы конфигурации

| Файл | Назначение |
|------|------------|
| [`docker/.env`](../../docker/.env.example) | Секреты Docker (пароли БД, MinIO, RabbitMQ) |
| [`src/DM.Web.API/appsettings.json`](../../src/DM.Web.API/appsettings.json) | Главный конфиг API (сессии, пароли, токены, CDN) |
| [`src/DM.Web.Client/.env.local`](../../src/DM.Web.Client/) | Frontend (API URL) |

### Workers (наследуют от API через docker-compose)

| Файл | Назначение |
|------|------------|
| [`src/DM.Workers.Mail/appsettings.json`](../../src/DM.Workers.Mail/appsettings.json) | Email: SMTP настройки |
| [`src/DM.Workers.SearchIndexer/appsettings.json`](../../src/DM.Workers.SearchIndexer/appsettings.json) | Search: OpenSearch подключение |
| [`src/DM.Workers.NotificationDispatcher/appsettings.json`](../../src/DM.Workers.NotificationDispatcher/appsettings.json) | Notifications: MongoDB, RabbitMQ |

### Основные секции appsettings.json

| Секция | Что настраивает | См. документацию |
|--------|-----------------|------------------|
| `AuthenticationConfiguration` | Сессии (1 год), throttling, lockout | [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| `PasswordPolicyConfiguration` | Требования к паролям (8+ символов) | [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| `TokenConfiguration` | Срок жизни токенов (активация, сброс пароля) | [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| `CdnConfiguration` | MinIO/S3 для source-файлов | [UPLOADS.md](../architecture/UPLOADS.md) |
| `ImageProxyConfiguration` | imgproxy endpoint + HMAC key/salt для signed transform URL's | [UPLOADS.md](../architecture/UPLOADS.md) |
| `MirrorConfiguration` | Зеркала (dm.am, ru.l.dm.am) | [MIRRORING.md](./MIRRORING.md) |

### Frontend

```bash
# src/DM.Web.Client/.env.local
VITE_API_HOST=http://localhost:5000
```

### Docker

Все секреты — в `docker/.env` (создать из `.env.example`):
```bash
POSTGRES_PASSWORD=...
RABBITMQ_DEFAULT_PASS=...
MINIO_ROOT_PASSWORD=...
IMGPROXY_KEY=...   # 64 hex chars (32 bytes), HMAC-SHA256 key
IMGPROXY_SALT=...  # 64 hex chars (32 bytes), HMAC-SHA256 salt
```

---

## Частые проблемы

| Проблема | Решение |
|----------|---------|
| Порт занят | `taskkill //F //IM node.exe` (Windows) |
| Frontend не видит API | Проверить `.env.local`: `VITE_API_HOST=http://localhost:5000` |
| Изображения не загружаются | API создает bucket автоматически на старте. Проверь `docker logs dm-api 2>&1 \| grep -i bucket` |
| Thumbnails не отдаются (404 на imgproxy) | `docker ps \| grep imgproxy`. Проверить `IMGPROXY_KEY`/`IMGPROXY_SALT` в `docker/.env` (64 hex chars each) |
| Seed: "API not available" | Запусти API: `dotnet run --project src/DM.Web.API` |
| Seed: "PostgreSQL not available" | Запусти: `docker compose up -d dm-pg` |
| Пользователи не в "Активных" | Seed обновляет `LastActivityUtc`, перезапусти seed |

---

## Preview режим

```bash
cd docker
docker compose -f docker-compose.yml -f docker-compose.preview.yml up -d --build
```

URL: http://localhost:80 за Basic Auth. Пароль в документации не публикуется: он лежит в `docker/nginx/.htpasswd`, задать свой — [DEPLOYMENT.md](./DEPLOYMENT.md#preview-окружение).

---

## Ссылки

- [Архитектура](../architecture/SYSTEM.md)
- [Тестирование](./TESTING.md)
- [Деплоймент](./DEPLOYMENT.md)
- [Конфигурация](../references/CONFIGURATION.md)

