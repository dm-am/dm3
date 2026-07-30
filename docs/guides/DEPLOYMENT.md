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
              Consumer Services (Email, Notifications)
```

---

## CI/CD (GitHub Actions)

**Файлы:** `.github/workflows/`

| Workflow | Файл | Триггеры | Действия |
|----------|------|----------|----------|
| Build & Test | `dotnet.yml` | push/PR в main, dev | Сборка, тесты, проверки качества и публикация образов; перечень job и их зависимости — в самом файле |
| Security | `security.yml` | push/PR + weekly | OWASP ZAP scan (full docker compose) |

**Образы публикуются в:** GHCR, под префиксом `IMAGE_PREFIX` из `dotnet.yml`. Имя каждого публикуемого образа обязано совпадать с тем, что тянет `docker/docker-compose.yml`, иначе CI публикует артефакт, который никто не потребляет.

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

Результат: http://<IP> за Basic Auth. Учетные данные лежат в [`docker/nginx/.htpasswd`](../../docker/nginx/.htpasswd) и в документации не публикуются — задать свои командой из [Смена пароля](#preview-окружение).

### Ручная установка

```bash
git clone https://github.com/dm-am/dm3.git && cd dm3/docker
docker compose up -d --build                    # Dev режим
docker compose -f docker-compose.yml -f docker-compose.preview.yml up -d  # Preview
```

### Preview окружение

**Правило:** сайт целиком живет в оверлее, а не в базовом compose. Базовый дает
инфраструктуру и API, оверлей добавляет nginx и контейнер SPA. Поэтому **любая**
команда, поднимающая или гасящая боевой стенд, называет оба файла — и инсталлятор,
и юнит systemd. Расхождение между ними означает, что перезапуск подменяет сайт
голым API, и его ловит отдельный гейт в CI.

**Образы, а не сборка на сервере.** И API, и фронтенд по умолчанию тянутся из
реестра; сборка на месте включается только переменной `API_IMAGE` / `FRONT_IMAGE`
с локальным тегом. Образ, который CI публикует, но никто не тянет, — это то же
самое, что отсутствие доставки.

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
| PostgreSQL | `gunzip -c /var/backups/postgresql/<файл>.sql.gz \| docker exec -i dm-pg psql -U postgres dm3` |
| MongoDB | `docker exec -i dm-mongo mongorestore --archive --gzip --drop < /var/backups/mongodb/<файл>.archive.gz` |
| MinIO | `docker run --rm --network host -v /var/backups/minio/<каталог>:/backup --entrypoint sh minio/mc -c 'mc alias set dst "$MINIO_ENDPOINT" "$MINIO_ROOT_USER" "$MINIO_ROOT_PASSWORD" && mc mirror /backup dst/dm-uploads'` |
| Git | `git revert HEAD && git push` |

Команды восстановления соответствуют тому, что кладут скрипты бэкапа: PostgreSQL
и MongoDB сжаты gzip-ом, а MinIO — каталог объектов, а не архив. Прежние команды
в этой таблице выполниться не могли: psql получал gzip вместо SQL, а mongorestore
искал архив по пути внутри контейнера, куда он не смонтирован, и без `--gzip`.

---

## Масштабирование

**Вертикальное:** `deploy.resources.limits` (cpus, memory) — настроено для всех сервисов.

**Горизонтальное — API в одном экземпляре, и это ограничение, а не недоделка.**
Внутри процесса API живут периодические задания (чистки, дайджесты, напоминания),
и выборов лидера между экземплярами нет: второй экземпляр выполнит ту же работу
второй раз. Часть заданий идемпотентна, часть нет, и разбирать это по одному
дешевле не станет. Поэтому `container_name` фиксирует единственность на уровне
compose — поднять реплики нельзя даже случайно.

Снимается это одним из двух способов, и оба — отдельная работа:
вынести задания в собственный процесс (как уже сделано с почтой и уведомлениями),
либо завести распределенную блокировку и брать ее перед каждым запуском.
Пока ни того ни другого нет, `deploy.replicas` для API — заявка, которую нечем
обеспечить.

**БД:** Connection pooling; размер пула задается строкой подключения в `x-workload-env` (`docker/docker-compose.yml`) и здесь не дублируется.

---

## Мониторинг

**Prometheus:** http://localhost:9090 — метрики всех сервисов (API + consumers + PostgreSQL + Node)

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
| `/_ready` | Readiness (PostgreSQL + MongoDB) |
| `/_health/detail` | Детальная информация обо всех проверках |

---

## Ссылки

- [Установка](./LOCAL_SETUP.md) — локальная разработка
- [Тестирование](./TESTING.md) — тесты и CI
- [Конфигурация](../references/CONFIGURATION.md) — все настройки

