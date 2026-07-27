# Развертывание DM3

## Архитектура

```
Internet → Nginx → Frontend (Vue.js)
                 → API (.NET 8)
                 → MinIO (S3-compat, source-файлы)
                 → imgproxy (on-the-fly resize + AVIF/WebP)
                       ↓
              PostgreSQL / MongoDB / RabbitMQ
                       ↓
              Consumer Services (Email, Search, Notifications)
```

---

## CI/CD (GitHub Actions)

**Файлы:** `.github/workflows/`

| Workflow | Файл | Триггеры | Действия |
|----------|------|----------|----------|
| Build & Test | `dotnet.yml` | push/PR в main, dev | 4 jobs: Build+Test, Frontend CI (type-check + build), Dependency Scanning (dotnet+npm audit), Publish (matrix: 4 Docker images: dm-api, consumer-mail, consumer-search, consumer-notification) |
| Security | `security.yml` | push/PR + weekly | OWASP ZAP scan (full docker compose) |

**Образы публикуются в:** `ghcr.io/<username>/dm3` (4 образа: dm-api, consumer-mail, consumer-search, consumer-notification)

**Теги:** `sha-<commit>`, `main`, `dev`, `latest` (только main)

---

## Docker Build

**Файлы:**
- Backend: [`docker/app.Dockerfile`](../../docker/app.Dockerfile)
- Frontend: [`src/DM.Web.Client/Dockerfile`](../../src/DM.Web.Client/Dockerfile)

**Принцип:** Multi-stage build (SDK → Runtime), non-root user `dmuser`

**Оптимизация:** BuildKit NuGet cache mount, .csproj-first restore для кэширования слоев

---

## Развертывание на VPS

### Автоматическая установка

```bash
curl -sSL https://raw.githubusercontent.com/dm-am/dm3/dev/docker/setup-server.sh | bash
```

Результат: http://<IP> (Basic Auth: `preview` / `dm2026preview`)

### Ручная установка

```bash
git clone https://github.com/dm-am/dm3.git && cd dm3/docker
docker compose up -d --build                    # Dev режим
docker compose -f docker-compose.yml -f docker-compose.preview.yml up -d  # Preview
```

### Preview окружение

**Файлы:**
- [`docker/docker-compose.preview.yml`](../../docker/docker-compose.preview.yml)
- [`docker/nginx/nginx.conf`](../../docker/nginx/nginx.conf)

**Смена пароля:**
```bash
docker run --rm httpd htpasswd -nb preview НОВЫЙ_ПАРОЛЬ > docker/nginx/.htpasswd
docker compose restart nginx
```

**SSL:** Раскомментировать HTTPS блок в nginx.conf + `certbot --nginx -d yourdomain.com`

### Зеркало

**Архитектура:** Один `docker-compose.yml` + разные `.env` файлы. Зеркало подключается к БД основного сервера.

```
Main сервер                    Mirror сервер
┌──────────────┐              ┌──────────────┐
│ docker-compose.yml          │ docker-compose.yml (тот же)
│ + .env                      │ + .env.mirror
│                             │
│ PostgreSQL ◄────────────────┤ dmapi
│ MongoDB    ◄────────────────┤ nginx
│ RabbitMQ   ◄────────────────┤
│ MinIO      ◄────────────────┤
└──────────────┘              └──────────────┘
```

**Первоначальная настройка зеркала (один раз):**
```bash
cd docker
cp .env.example .env.mirror
# Заполнить: секреты и крипто-ключ с main, MIRROR_ID, хосты main-сервера,
# публичные URL зеркала — полный список переменных в MIRRORING.md
docker compose --env-file .env.mirror --profile mirror up -d
```

**Обновление — автоматически!**

Watchtower каждые 5 минут проверяет новые образы и обновляет контейнеры.

**Добавление нового зеркала:**
1. Обновить `appsettings.json` — добавить URL зеркала
2. Merge в main → CI/CD публикует образ
3. Watchtower обновит все сервера автоматически

**Nginx:** Использовать `nginx/nginx.conf`, убрать `auth_basic`, раскомментировать HTTPS.

---

## Переменные окружения

**Источник истины:** [`docker/docker-compose.yml`](../../docker/docker-compose.yml) (секция `x-workload-env`)

| Категория | Переменные |
|-----------|------------|
| Базы данных | `DM_ConnectionStrings__Rdb`, `DM_ConnectionStrings__Mongo` |
| RabbitMQ | `DM_RabbitMqConfiguration__*` |
| MinIO (source storage) | `DM_CdnConfiguration__*` |
| imgproxy (transform layer) | `DM_ImageProxyConfiguration__Endpoint/Key/Salt/SourceUrlPrefix` |
| Email | `DM_EmailConfiguration__*` |

**Production:** `docker/.env` (создать из `docker/.env.example`). Secrets: `POSTGRES_PASSWORD`, `RABBITMQ_DEFAULT_PASS`, `MINIO_ROOT_PASSWORD`, `IMGPROXY_KEY`, `IMGPROXY_SALT`, `GF_SECURITY_ADMIN_PASSWORD`.

---

## Откат

| Метод | Команда |
|-------|---------|
| Docker образ | `docker compose down && docker compose up -d` с другим тегом |
| PostgreSQL | `docker exec -i dm-pg psql -U postgres dm3 < backup.sql` |
| MongoDB | `docker exec dm-mongo mongorestore --archive=/backup.archive --drop` |
| Git | `git revert HEAD && git push` |

---

## Масштабирование

**Горизонтальное:** `deploy.replicas: 3` в docker-compose + nginx `upstream` с `least_conn`

**Вертикальное:** `deploy.resources.limits` (cpus, memory) — уже настроено для всех сервисов

**БД:** Connection pooling (`MaxPoolSize=200`), read replicas

---

## Мониторинг

**Prometheus:** http://localhost:9090 — метрики всех сервисов (API + 3 consumers + PostgreSQL + Node)

**Grafana:** http://localhost:3000 — 3 dashboard'а (API Overview, Infrastructure, Consumers) настроены автоматически

**Jaeger:** http://localhost:16686 — distributed tracing (OTLP gRPC)

**Alerting:** `docker/prometheus/alerts.yml` — 7 правил (ApiDown, HighErrorRate, HighLatency, ConsumerDown, PostgresDown, HighMemoryUsage, DiskSpaceLow)

---

## Бэкапы

**Скрипты:** `docker/scripts/backup-postgres.sh`, `backup-mongodb.sh`, `backup-minio.sh`

**Проверка:** `docker/scripts/verify-backup.sh` — возраст, размер, целостность gzip

**Retention:** 30 дней для всех типов бэкапов

---

## Health Checks

| Endpoint | Назначение |
|----------|-----------|
| `/_health` | Liveness (Docker health check) |
| `/_ready` | Readiness (PostgreSQL + MongoDB + RabbitMQ) |
| `/_health/detail` | Детальная информация обо всех проверках |

---

## Ссылки

- [Установка](./LOCAL_SETUP.md) — локальная разработка
- [Тестирование](./TESTING.md) — тесты и CI
- [Конфигурация](../references/CONFIGURATION.md) — все настройки

