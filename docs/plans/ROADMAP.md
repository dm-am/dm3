# Дорожная карта DM3

> **User Story:** "Что планируется? Что рассматривается? Какой техдолг?"

**Что уже готово:** [PROGRESS.md](../PROGRESS.md).

## Стек

- **Backend:** .NET 8, PostgreSQL, MongoDB, RabbitMQ
- **Frontend:** Vue 3, TypeScript, Vite
- **Инфраструктура:** Docker, Nginx, SignalR, Prometheus, Grafana, Jaeger

---

## Планируется

### Инфраструктура

| Задача | Описание |
|--------|----------|
| Traefik | Заменить Nginx: auto SSL, labels config, hot reload |
| Zero-downtime deploys | 2 реплики API/frontend + rolling updates |
| Auto-deploy | Webhook + GitHub Actions (push → deploy) |
| Offsite backups | Restic → Backblaze B2 (шифрованные) |
| External monitoring | Better Stack → Telegram алерты |
| Umami | Self-hosted аналитика |
| Карта старых адресов | Редиректы со старых адресов на новые маршруты |

### Функциональность

| Задача | Описание |
|--------|----------|
| Invite links | Многоразовые ссылки-приглашения для игр/блогов (как в Discord) |

### Интеграции

| Задача | Описание |
|--------|----------|
| Модераторский канал | Отправка событий модерации в канал Discord или чат Telegram |

### Тесты

| Задача | Описание |
|--------|----------|
| Расширить E2E | Playwright для всех страниц |

### Кэширование

| Задача | Описание |
|--------|----------|
| User profiles | Redis/Memory cache |
| Forum moderators | Кэш списка модераторов |

---

## Технический долг

| Задача | Файл/Область |
|--------|--------------|
| Рефакторинг BbParserWrapper | Infrastructure.Core, парсинг BBCode |

---

## Рассматривается

| Задача | Описание |
|--------|----------|
| k3s + Helm + Terraform | Kubernetes (если масштаб вырастет) |
| Bun | Замена Node.js |
| Google OAuth | Вход через Google |
| Автоприветствие наставника | Приветственное сообщение новичку от назначенного наставника |

---

## Ссылки

- [Миграция со старого сайта](./DM2_MIGRATION.md)
- [Стандарты разработки](../conventions/CODE_STYLE.md)
- [Требования безопасности](../conventions/SECURITY.md) — реализованное и планируемое по NIST SP 800-63-4
