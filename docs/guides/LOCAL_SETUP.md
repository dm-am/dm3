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
- **MinIO:** http://localhost:9001 (credentials из `docker/.env`)  — создать bucket `dm-uploads` с Access Policy = `Public`

---

## Порты сервисов

**Источник истины:** [`docker/docker-compose.yml`](../../docker/docker-compose.yml)

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

**Credentials:** Все пароли в `docker/.env` (создать из `docker/.env.example`).

**Все порты:** `docker ps --format "table {{.Names}}\t{{.Ports}}"`

---

## Тестовые данные

### Справочные данные (автоматически)

При запуске API EF Core применяет миграции, которые создают:
- **Доски форума** — 11 разделов
- **Теги игр** — 57 тегов в 3 группах

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
| Admin | `TestAdmin` | admin@test.local |
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
| `Тест Ёлки` | Cyrillic + space + Ё |

**Специальные состояния:**

| Логин | Особенность |
|-------|-------------|
| `TestHonorary` | Honorary goblin (почетный гоблин) |

**Pending Registrations (для тестирования активации):**

| Email | Назначение |
|-------|------------|
| inactive@test.local | Тестирование flow "регистрация не завершена" |

Все пользователи начинают с 0 постов (статус "новичок").

### Ручная регистрация

1. http://localhost:5173 → Регистрация
2. MailHog: http://localhost:8025 (письмо активации)
3. Смена роли: http://localhost:5173/dev/accounts

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

```bash
dotnet ef migrations add YYYYMMDD_Name -p src/DM.Infrastructure.Persistence -s src/DM.Web.API
dotnet ef database update -p src/DM.Infrastructure.Persistence -s src/DM.Web.API
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
| `CdnConfiguration` | MinIO/S3 для загрузки файлов | — |
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
```

---

## Частые проблемы

| Проблема | Решение |
|----------|---------|
| Порт занят | `taskkill //F //IM node.exe` (Windows) |
| Frontend не видит API | Проверить `.env.local`: `VITE_API_HOST=http://localhost:5000` |
| Изображения не загружаются | MinIO bucket `dm-uploads` с Access Policy = `Public` |
| Seed: письма не доходят | Используй `node scripts/seed.js` без `--with-email` |
| Seed: "API not available" | Запусти API: `dotnet run --project src/DM.Web.API` |
| Seed: "PostgreSQL not available" | Запусти: `docker compose up -d dm-pg` |
| Пользователи не в "Активных" | Seed обновляет `LastActivityUtc`, перезапусти seed |

---

## Preview режим

```bash
cd docker
docker compose -f docker-compose.yml -f docker-compose.preview.yml up -d --build
```

URL: http://localhost:80 (Basic Auth: `preview` / `dm2026preview`)

---

## Ссылки

- [Архитектура](../architecture/SYSTEM.md)
- [Тестирование](./TESTING.md)
- [Деплоймент](./DEPLOYMENT.md)
- [Конфигурация](../references/CONFIGURATION.md)

