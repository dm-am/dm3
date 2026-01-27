# Развертывание DM3

**Создано:** 2026-01-27
**Обновлено:** 2026-01-27

## Содержание

- [Обзор](#обзор)
- [GitHub Actions CI/CD](#github-actions-cicd)
  - [Workflow: Build and Test (.NET)](#workflow-build-and-test-net)
  - [Workflow: Security Scanning](#workflow-security-scanning)
- [Docker Build Process](#docker-build-process)
  - [Backend (API и Consumers)](#backend-api-и-consumers)
  - [Frontend (Vue.js SPA)](#frontend-vuejs-spa)
- [Container Registry](#container-registry)
- [Развертывание на VPS](#развертывание-на-vps)
  - [Автоматическая установка](#автоматическая-установка)
  - [Ручная установка](#ручная-установка)
  - [Preview окружение с Basic Auth](#preview-окружение-с-basic-auth)
- [Переменные окружения](#переменные-окружения)
- [Стратегия отката](#стратегия-отката)
- [Масштабирование](#масштабирование)
- [Связанная документация](#связанная-документация)

## Обзор

DM3 использует современный подход к развертыванию на базе Docker-контейнеров и GitHub Actions для CI/CD. Приложение состоит из:

- **Backend API** (.NET 8.0) - основной REST API
- **3 Consumer-сервиса** (.NET 8.0) - обработка фоновых задач
- **Frontend** (Vue.js + Vite) - SPA приложение
- **Инфраструктура** (PostgreSQL, MongoDB, OpenSearch, RabbitMQ, MinIO, Jaeger, Prometheus, Grafana)

Архитектура развертывания:

```
┌─────────────────────────────────────────────────────────────┐
│                          Internet                           │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      ▼
┌─────────────────────────────────────────────────────────────┐
│                    Nginx (Reverse Proxy)                    │
│              - Basic Auth (preview only)                    │
│              - SSL/TLS termination                          │
│              - Static file serving                          │
└──────┬──────────────┬──────────────────┬───────────────────┘
       │              │                  │
       ▼              ▼                  ▼
┌───────────┐  ┌─────────────┐   ┌────────────┐
│  Frontend │  │   DM API    │   │   MinIO    │
│  (Vue.js) │  │  (Backend)  │   │   (CDN)    │
└───────────┘  └──────┬──────┘   └────────────┘
                      │
        ┌─────────────┼─────────────┐
        ▼             ▼             ▼
┌──────────────┐ ┌─────────┐ ┌────────────┐
│ PostgreSQL   │ │ MongoDB │ │ RabbitMQ   │
│   (Main DB)  │ │ (NoSQL) │ │ (Queue)    │
└──────────────┘ └─────────┘ └────────────┘
        │             │             │
        └─────────────┼─────────────┘
                      ▼
           ┌──────────────────────┐
           │  Consumer Services   │
           │  - Email Sender      │
           │  - Search Engine     │
           │  - Notifications     │
           └──────────────────────┘
```

## GitHub Actions CI/CD

### Workflow: Build and Test (.NET)

**Файл:** `.github/workflows/dotnet.yml`

**Триггеры:**
- Push в ветки `main` и `dev`
- Pull Request в ветки `main` и `dev`

**Job 1: Build & Test**

```yaml
build:
  runs-on: ubuntu-latest
  steps:
    - Checkout кода
    - Setup .NET 8.0.x
    - Restore dependencies
    - Build проекта
    - Запуск тестов с генерацией TRX отчетов
    - Загрузка результатов тестов как артефактов
```

**Job 2: Publish (только для main/dev)**

```yaml
publish:
  needs: build
  runs-on: ubuntu-latest
  if: github.event_name == 'push' && (github.ref == 'refs/heads/main' || github.ref == 'refs/heads/dev')
  steps:
    - Login в GitHub Container Registry (ghcr.io)
    - Извлечение метаданных Docker (теги, labels)
    - Build и push Docker образа API
```

**Генерируемые теги:**
- `sha-<commit>` - для всех коммитов
- `main` или `dev` - для соответствующих веток
- `latest` - только для ветки `main`

**Пример из workflow:**

```yaml
- name: Extract metadata for Docker
  id: meta
  uses: docker/metadata-action@v5
  with:
    images: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}
    tags: |
      type=sha,prefix=
      type=ref,event=branch
      type=raw,value=latest,enable=${{ github.ref == 'refs/heads/main' }}

- name: Build and push API image
  uses: docker/build-push-action@v5
  with:
    context: .
    file: ./docker/app.Dockerfile
    push: true
    tags: ${{ steps.meta.outputs.tags }}
    labels: ${{ steps.meta.outputs.labels }}
    build-args: |
      PROJECT_NAME=DM.Web.API
```

### Workflow: Security Scanning

**Файл:** `.github/workflows/security.yml`

**Триггеры:**
- Push в ветки `main` и `dev`
- Pull Request в ветки `main` и `dev`
- Расписание: каждое воскресенье в 03:00 UTC

**Процесс:**

1. **Подготовка окружения:**
   - Запуск PostgreSQL контейнера
   - Build Docker образа API
   - Запуск API контейнера с подключением к БД
   - Ожидание готовности API (health check на `/_health`)

2. **OWASP ZAP сканирование:**
   - Baseline scan API endpoints
   - Проверка уязвимостей безопасности
   - Генерация HTML отчета
   - Загрузка отчета как артефакта

**Пример из workflow:**

```yaml
- name: Start PostgreSQL
  run: |
    docker run -d --name dm-pg -p 5432:5432 \
      -e POSTGRES_USER=postgres \
      -e POSTGRES_PASSWORD=admin \
      -e POSTGRES_DB=dm3.5 \
      postgres:16

- name: Build and start API
  run: |
    docker build -t dm-api:test -f docker/app.Dockerfile .
    docker run -d --name dm-api -p 5051:5050 \
      -e ConnectionStrings__Rdb="Host=host.docker.internal;Database=dm3.5;Username=postgres;Password=admin" \
      dm-api:test

- name: OWASP ZAP Baseline Scan
  uses: zaproxy/action-baseline@v0.12.0
  with:
    target: 'http://localhost:5051'
    rules_file_name: '.zap/rules.tsv'
```

## Docker Build Process

### Backend (API и Consumers)

**Dockerfile:** `docker/app.Dockerfile`

Используется multi-stage build для оптимизации размера образа:

**Stage 1: Build**

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build

ARG PROJECT_NAME

WORKDIR /app

COPY src ./
COPY DM.sln ./
COPY Directory.Build.props ./
COPY Directory.Packages.props ./
RUN dotnet publish ${PROJECT_NAME} -c Release -o out
```

- Базовый образ: `mcr.microsoft.com/dotnet/sdk:8.0`
- Аргумент `PROJECT_NAME` для универсальности (используется для API и всех consumers)
- Publish в режиме Release

**Stage 2: Runtime**

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

ARG PROJECT_NAME

WORKDIR /app
COPY --from=build /app/out ./

RUN adduser --disabled-password dmuser
USER dmuser

ENV RUNTIME_PROJECT ${PROJECT_NAME}.dll
CMD dotnet ${RUNTIME_PROJECT}
```

- Базовый образ: `mcr.microsoft.com/dotnet/aspnet:8.0` (меньше размером)
- Создание непривилегированного пользователя `dmuser` (безопасность)
- Динамическая точка входа через переменную `RUNTIME_PROJECT`

**Используется для:**
- `DM.Web.API` - основной API
- `DM.Services.Mail.Sender.Consumer` - отправка email
- `DM.Services.Search.Consumer` - индексация поиска
- `DM.Services.Notifications.Consumer` - уведомления

### Frontend (Vue.js SPA)

**Dockerfile:** `frontend/DM.Web.Modern/Dockerfile`

**Stage 1: Build**

```dockerfile
FROM node:20-alpine as build

WORKDIR /app

# Copy package files
COPY package.json package-lock.json ./

# Install dependencies
RUN npm ci

# Copy source
COPY . .

# Build with empty API host (will use relative URLs through nginx)
ENV VITE_API_HOST=""
RUN npm run build
```

- Node.js 20 Alpine для минимального размера
- `npm ci` для reproducible builds
- `VITE_API_HOST=""` - пустой хост для работы через nginx proxy

**Stage 2: Production**

```dockerfile
FROM nginx:alpine

# Copy built files
COPY --from=build /app/dist /usr/share/nginx/html

# Copy nginx config
COPY nginx.conf /etc/nginx/conf.d/default.conf

EXPOSE 80

CMD ["nginx", "-g", "daemon off;"]
```

- Nginx Alpine для продакшена
- Статические файлы из `/dist`
- Кастомная конфигурация nginx

## Container Registry

**Registry:** GitHub Container Registry (ghcr.io)

**URL:** `ghcr.io/<username>/dm3`

**Аутентификация:**
- GitHub Actions использует `GITHUB_TOKEN` (автоматически)
- Для локального pull:
  ```bash
  echo $GITHUB_TOKEN | docker login ghcr.io -u <username> --password-stdin
  ```

**Публикуемые образы:**
- `ghcr.io/<username>/dm3:latest` - последняя стабильная версия (main)
- `ghcr.io/<username>/dm3:main` - ветка main
- `ghcr.io/<username>/dm3:dev` - ветка dev
- `ghcr.io/<username>/dm3:sha-<commit>` - конкретный коммит

## Развертывание на VPS

### Автоматическая установка

**Скрипт:** `docker/setup-server.sh`

Для быстрого развертывания на чистом Ubuntu сервере:

```bash
curl -sSL https://raw.githubusercontent.com/dm-am/dm3/dev/docker/setup-server.sh | bash
```

**Что делает скрипт:**

1. Устанавливает Docker и Docker Compose
2. Добавляет текущего пользователя в группу `docker`
3. Клонирует репозиторий DM3
4. Открывает порты 80 и 443 в firewall (iptables)
5. Запускает приложение через docker-compose (preview режим)
6. Выводит URL приложения и учетные данные

**Результат:**

```
=== Готово! ===
Приложение доступно по адресу: http://<IP>
Логин: preview
Пароль: dm2026preview
```

### Ручная установка

**Требования:**
- Ubuntu 20.04+ / Debian 11+
- Docker 20.10+
- Docker Compose v2+
- Минимум 2GB RAM, 20GB disk
- Открытые порты: 80, 443

**Шаги:**

1. **Клонирование репозитория:**

```bash
git clone https://github.com/dm-am/dm3.git
cd dm3
```

2. **Базовое развертывание (без nginx):**

```bash
cd docker
docker-compose up -d --build
```

Доступ:
- Frontend: `http://localhost:5050`
- API: `http://localhost:5051`
- Swagger: `http://localhost:5051/swagger`

3. **Production развертывание (с nginx):**

```bash
cd docker
docker-compose -f docker-compose.yml -f docker-compose.preview.yml up -d --build
```

Доступ: `http://localhost:80` (с Basic Auth)

### Preview окружение с Basic Auth

**Compose файл:** `docker/docker-compose.preview.yml`

**Особенности:**
- Nginx как reverse proxy для frontend и API
- HTTP Basic Authentication для защиты preview
- Единая точка входа на порту 80

**Генерация пароля:**

```bash
docker run --rm httpd htpasswd -nb preview yourpassword > docker/nginx/.htpasswd
```

**Файл:** `docker/nginx/.htpasswd`

```
preview:$apr1$xxxxxxxxxxxxxxxxxxx
```

**Смена пароля:**

```bash
docker run --rm httpd htpasswd -nb preview НОВЫЙ_ПАРОЛЬ > docker/nginx/.htpasswd
cd docker && docker-compose restart nginx
```

**Nginx конфигурация:** `docker/nginx/nginx.conf`

```nginx
# Basic auth for all requests
auth_basic "DM Preview - Restricted Access";
auth_basic_user_file /etc/nginx/.htpasswd;

# API proxy
location /v1/ {
    proxy_pass http://api;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
}

# SignalR/WebSocket
location /notifications {
    proxy_pass http://api;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_read_timeout 86400;
}

# MinIO uploads (без auth)
location /dm-uploads/ {
    auth_basic off;
    proxy_pass http://minio:9000/dm-uploads/;
}

# Frontend
location / {
    proxy_pass http://frontend;
}
```

**Security headers:**

```nginx
add_header X-Frame-Options "SAMEORIGIN" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-XSS-Protection "1; mode=block" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
add_header Permissions-Policy "geolocation=(), microphone=(), camera=()" always;
```

**SSL/TLS (для production):**

Раскомментировать HTTPS блок в `nginx.conf` и установить сертификаты:

```bash
# Let's Encrypt (рекомендуется)
certbot --nginx -d yourdomain.com

# Или self-signed (для тестирования)
openssl req -x509 -nodes -days 365 -newkey rsa:2048 \
  -keyout /etc/nginx/ssl/privkey.pem \
  -out /etc/nginx/ssl/fullchain.pem \
  -subj "/CN=localhost"
```

## Переменные окружения

**Источник:** `docker/docker-compose.yml` (секция `x-workload-env`)

### Базы данных

```bash
# PostgreSQL
DM_ConnectionStrings__Rdb=Host=dm-pg;Port=5432;User ID=postgres;Password=admin;Database=dm3.5;Pooling=true;MinPoolSize=0;MaxPoolSize=100;Connection Idle Lifetime=60;

# MongoDB
DM_ConnectionStrings__Mongo=mongodb://dm-mongo:27017/dm3-5?maxPoolSize=1000

# OpenSearch (логи и поиск)
DM_ConnectionStrings__Logs=http://dm-es:9200

# Jaeger (трейсинг)
DM_ConnectionStrings__TracingEndpoint=http://dm-jaeger:14268/api/traces
```

### RabbitMQ (очередь сообщений)

```bash
DM_RabbitMqConfiguration__Endpoint=amqp://dm-rmq:5672
DM_RabbitMqConfiguration__VirtualHost=/
DM_RabbitMqConfiguration__Username=guest
DM_RabbitMqConfiguration__Password=guest
```

### MinIO (CDN/хранилище файлов)

```bash
DM_CdnConfiguration__Url=http://dm-minio:9000
DM_CdnConfiguration__PublicUrl=http://localhost:9000
DM_CdnConfiguration__Region=us-east-1
DM_CdnConfiguration__BucketName=dm-uploads
DM_CdnConfiguration__AccessKey=minio
DM_CdnConfiguration__SecretKey=miniokey
DM_CdnConfiguration__Provider=Minio
```

### Email (MailHog для разработки)

```bash
DM_EmailConfiguration__ServerHost=dm-mailhog
DM_EmailConfiguration__ServerPort=1025
DM_EmailConfiguration__FromAddress=info@dm.am
DM_EmailConfiguration__FromDisplayName=DM.am
DM_EmailConfiguration__ReplyToAddress=test@dm.am
```

### Интеграция

```bash
DM_IntegrationSettings__WebUrl=http://localhost:5050
DM_IntegrationSettings__ApiUrl=http://localhost:5051
DM_IntegrationSettings__CorsUrls__0=http://localhost:5050
DM_IntegrationSettings__CorsUrls__1=http://localhost:8080
DM_IntegrationSettings__CorsUrls__2=http://localhost:5173

PORT=5050
```

### Специальные флаги

```bash
# Автоматическая миграция БД при запуске (для migration контейнера)
DM_MigrateOnStart=true
```

**Переопределение в production:**

Создать `.env` файл или использовать Docker secrets:

```bash
# .env
DM_ConnectionStrings__Rdb=Host=prod-db;Database=dm_prod;Username=dmuser;Password=${DB_PASSWORD}
DM_EmailConfiguration__ServerHost=smtp.gmail.com
DM_EmailConfiguration__ServerPort=587
DM_EmailConfiguration__Username=${SMTP_USERNAME}
DM_EmailConfiguration__Password=${SMTP_PASSWORD}
```

## Стратегия отката

### 1. Откат Docker образов

**Просмотр доступных версий:**

```bash
docker images ghcr.io/<username>/dm3
```

**Откат на предыдущую версию:**

```bash
# Остановить текущие контейнеры
docker-compose down

# Указать нужную версию в docker-compose.yml
# services:
#   dmapi:
#     image: ghcr.io/<username>/dm3:sha-<previous-commit>

# Или через переменную окружения
export DM_API_VERSION=sha-abc123
docker-compose up -d
```

### 2. Откат базы данных

**PostgreSQL:**

```bash
# Восстановление из бэкапа
docker exec -i dm-pg psql -U postgres dm3.5 < backup.sql

# Или использовать скрипт
cd docker/scripts
./restore-postgres.sh backup.sql
```

**MongoDB:**

```bash
# Восстановление из бэкапа
docker exec dm-mongo mongorestore --archive=/backup/dm3-5.archive --drop

# Или через скрипт
cd docker/scripts
./restore-mongodb.sh backup.archive
```

См. [backup scripts](../../docker/scripts/README.md) для автоматизации.

### 3. Blue-Green Deployment

**Идея:** Запустить две версии приложения параллельно.

```bash
# Запустить новую версию на других портах
docker-compose -f docker-compose.yml \
  -f docker-compose.preview.yml \
  -p dm-green up -d

# Переключить nginx на новую версию
# (изменить upstream в nginx.conf)

# Если проблемы - переключить обратно
# Удалить новую версию
docker-compose -p dm-green down
```

### 4. Git откат

```bash
# Откат последнего коммита
git revert HEAD
git push origin main

# GitHub Actions автоматически задеплоит предыдущую версию
```

### 5. Health Checks

Перед полным переключением проверить:

```bash
# API health
curl http://localhost:5051/_health

# Metrics
curl http://localhost:5051/metrics

# Database connectivity
docker exec dm-api dotnet DM.Web.API.dll --health-check
```

## Масштабирование

### Горизонтальное масштабирование

**API Replicas:**

```yaml
services:
  dmapi:
    deploy:
      replicas: 3
      restart_policy:
        condition: on-failure
```

**Nginx load balancing:**

```nginx
upstream api {
    least_conn;
    server dmapi-1:5050;
    server dmapi-2:5050;
    server dmapi-3:5050;
}
```

### Вертикальное масштабирование

**Ресурсы контейнеров:**

```yaml
services:
  dmapi:
    deploy:
      resources:
        limits:
          cpus: '2.0'
          memory: 2G
        reservations:
          cpus: '1.0'
          memory: 1G
```

### База данных

**PostgreSQL connection pooling:**

```bash
DM_ConnectionStrings__Rdb=...;Pooling=true;MinPoolSize=10;MaxPoolSize=200
```

**Read replicas:**

Настроить PostgreSQL streaming replication для read-only запросов.

### Кеширование

**Redis для кеша:**

```yaml
services:
  redis:
    image: redis:alpine
    ports:
      - "6379:6379"
```

Добавить в переменные окружения:

```bash
DM_CacheConfiguration__Provider=Redis
DM_CacheConfiguration__ConnectionString=redis:6379
```

## Связанная документация

- [Скрипты резервного копирования](../../docker/scripts/README.md) - backup стратегия
- [Стандарты проекта](../standards/CODE.md) - code style и архитектура
- [Установка и запуск](./SETUP.md) - локальная разработка
- [Тестирование](./TESTING.md) - тесты и CI
