# Установка и запуск DM3

> **Обновлено:** 2026-01-27

---

## Требования

- **Docker Desktop** (с WSL2 на Windows)
- **Node.js 20+** и **Yarn** (для frontend)
- **Git**

Проверка:
```bash
docker --version
node --version
yarn --version
```

---

## Быстрый старт (5 минут)

### 1. Запуск инфраструктуры

```bash
cd docker
docker-compose up -d
```

Запустится: PostgreSQL, MongoDB, RabbitMQ, MinIO, OpenSearch, MailHog, Jaeger, Backend API.

Проверка: `docker ps` — должно быть ~10 контейнеров.

### 2. Настройка MinIO

1. Откройте http://localhost:9001
2. Войдите: `minio` / `miniokey`
3. Создайте bucket `dm-uploads`
4. Access Policy → `Public`

### 3. Запуск Frontend

```bash
cd frontend/DM.Web.Modern
yarn install
yarn dev
```

Frontend: http://localhost:5173

### 4. Создание тестовых аккаунтов

1. Откройте http://localhost:5173/dev/accounts
2. Нажмите "Создать/обновить тестовые аккаунты"

---

## Порты сервисов

| Сервис | URL | Credentials |
|--------|-----|-------------|
| **Frontend** | http://localhost:5173 | — |
| **API** | http://localhost:5051 | — |
| **Swagger** | http://localhost:5051/swagger | — |
| **PostgreSQL** | localhost:5432 | postgres/admin |
| **MongoDB** | localhost:27017 | — |
| **RabbitMQ** | http://localhost:15672 | guest/guest |
| **MinIO** | http://localhost:9001 | minio/miniokey |
| **MailHog** | http://localhost:5025 | — |
| **Grafana** | http://localhost:3000 | admin/admin |

---

## Тестовые аккаунты

| Логин | Пароль | Роль |
|-------|--------|------|
| Rayzen | `Test123!` | SeniorModerator |
| Akkarin | `Test123!` | Moderator |
| Alice | `Test123!` | RegularUser |
| Bob | `Test123!` | RegularUser |

---

## Команды

### Docker

```bash
docker ps                           # Статус
docker logs dm-api --tail 50 -f     # Логи API
docker restart dm-api               # Перезапуск
cd docker && docker-compose down    # Остановка
```

### Frontend

```bash
cd frontend/DM.Web.Modern
yarn dev           # Dev server
yarn build         # Production
yarn test:unit     # Тесты
yarn lint          # Линтинг
```

### Backend (локальная разработка)

```bash
docker stop dm-api
dotnet run --project src/DM.Web.API --urls "http://localhost:5051"
```

### Миграции

```bash
dotnet ef migrations add YYYYMMDD_Name -p src/DM.Services.DataAccess
dotnet ef database update -p src/DM.Services.DataAccess
```

---

## Конфигурация

### Frontend (.env.local)

```bash
VITE_API_HOST=http://localhost:5051
```

### Backend

Connection strings настроены для Docker по умолчанию. Переопределение:
- `appsettings.Development.json`
- Environment: `ConnectionStrings__Rdb=...`

---

## Частые проблемы

### Порт занят

```bash
# Windows
taskkill //F //IM node.exe

# Linux/Mac
pkill -f node
```

### Frontend не видит API

Проверьте `.env.local`: `VITE_API_HOST=http://localhost:5051`

### Изображения не загружаются

Проверьте MinIO bucket `dm-uploads` и Access Policy = `Public`.

---

## Preview режим (для показа)

```bash
cd docker
docker-compose -f docker-compose.yml -f docker-compose.preview.yml up -d --build
```

URL: http://localhost:80
Basic Auth: `preview` / `dm2026preview`
