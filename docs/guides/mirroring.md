# Зеркалирование DM3

## Обзор архитектуры

Зеркала используют **общую инфраструктуру** (БД, очереди, хранилище) с основным сервером, но предоставляют **отдельные точки входа** для пользователей.

```
                         ┌─────────────────────────────────────────────────────────────┐
                         │                    Main Server (db.dm.am)                   │
                         │                                                             │
Users ─────────┬────────>│  nginx → dm-api ─────┬───> PostgreSQL (dm3)               │
(dm.am)        │         │                      ├───> MongoDB (dm3)                   │
               │         │                      ├───> RabbitMQ                        │
               │         │                      └───> MinIO (dm-uploads)              │
               │         │                                                             │
               │         │  Consumers: mail, search, notifications                     │
               │         └─────────────────────────────────────────────────────────────┘
               │                                    ▲
               │                                    │ SSL connections
               │                                    │
               │         ┌──────────────────────────┴──────────────────────────────────┐
               │         │                    Mirror Server (dm.ru)                    │
               └────────>│                                                             │
Users ────────────────────>│  nginx → dm-api ──────────────────────────────────────────┤
(dm.ru)                  │                    (connects to Main Server DBs)            │
                         │                                                             │
                         │  Watchtower (auto-updates from GHCR)                        │
                         └─────────────────────────────────────────────────────────────┘
```

## Преимущества

1. **Единая база данных** — все данные синхронизированы
2. **Географическая близость** — меньше задержка для пользователей
3. **Автоматическое обновление** — Watchtower следит за новыми образами
4. **Session transfer** — пользователи могут переходить между зеркалами

## Требования

### Основной сервер
- Открытые порты для зеркал (через firewall):
  - `5432` (PostgreSQL)
  - `27017` (MongoDB)
  - `5672` (RabbitMQ)
  - `9000` (MinIO)
- SSL сертификаты для БД (или VPN между серверами)

### Зеркало
- VPS с Docker
- SSL сертификат (Let's Encrypt)
- Те же криптографические ключи что на основном сервере

## Быстрая настройка зеркала

```bash
# 1. Клонировать репозиторий
git clone https://github.com/dm-am/dm3.git && cd dm3/docker

# 2. Запустить интерактивный мастер
./scripts/setup-mirror.sh

# 3. Настроить nginx (SSL)
certbot --nginx -d your-mirror-domain.ru

# 4. Запустить
docker compose --env-file .env.mirror --profile mirror up -d
```

## Ручная настройка

### 1. Создать `.env.mirror`

```bash
cp .env.mirror.example .env.mirror
# Заполнить:
# - POSTGRES_PASSWORD (тот же что на main)
# - RABBITMQ_DEFAULT_PASS (тот же)
# - MINIO_ROOT_PASSWORD (тот же)
# - DM_CryptoConfiguration__KeyBase64 (КРИТИЧНО: тот же!)
# - MIRROR_ID (уникальный для этого зеркала)
# - DB_HOST, MONGO_HOST, etc. (IP/домен основного сервера)
```

### 2. Настроить firewall на основном сервере

```bash
# UFW (Ubuntu)
ufw allow from <mirror-ip> to any port 5432  # PostgreSQL
ufw allow from <mirror-ip> to any port 27017 # MongoDB
ufw allow from <mirror-ip> to any port 5672  # RabbitMQ
ufw allow from <mirror-ip> to any port 9000  # MinIO
```

### 3. SSL для подключения к БД

В `.env.mirror`:
```bash
DB_SSL_MODE=;SSL Mode=Require
MONGO_TLS=&tls=true
```

### 4. Запуск

```bash
docker compose --env-file .env.mirror --profile mirror up -d
```

## Session Transfer

Пользователи могут авторизоваться на любом зеркале и их сессия будет работать везде.

**Критически важно:** Все зеркала должны использовать **одинаковый криптографический ключ** (`DM_CryptoConfiguration__KeyBase64`).

Генерация ключа:
```bash
openssl rand -base64 32
```

## Автоматическое обновление

Watchtower автоматически обновляет контейнеры когда появляются новые образы:

1. CI/CD публикует образ в GitHub Container Registry
2. Watchtower (каждые 5 мин) проверяет наличие обновлений
3. При обнаружении нового образа — автоматический pull и restart

### Добавление нового зеркала

1. Настроить зеркало по инструкции выше
2. Добавить URL зеркала в `appsettings.json` → `IntegrationSettings.MirrorUrls`
3. Push в main → CI/CD обновит образ → Watchtower обновит все сервера

## Мониторинг

Каждое зеркало имеет health-эндпоинты:

- `/_health` — liveness (Docker health check)
- `/_ready` — readiness (проверяет подключение к БД)
- `/_health/detail` — детальная информация

## Troubleshooting

| Проблема | Решение |
|----------|---------|
| "Connection refused" к БД | Проверить firewall, SSL настройки |
| Session не работает между зеркалами | Криптографические ключи должны совпадать |
| Watchtower не обновляет | Проверить label `com.centurylinklabs.watchtower.enable=true` |
| Высокая задержка | Использовать VPN или сервер ближе к main |

## Ссылки

- [Деплоймент](./deployment.md) — общая информация

