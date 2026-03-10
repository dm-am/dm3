# Дорожная карта DM3

## Стек

- **Backend:** .NET 8, PostgreSQL, MongoDB, RabbitMQ
- **Frontend:** Vue 3, TypeScript, Vite
- **Инфраструктура:** Docker, Nginx, SignalR, Prometheus, Grafana, Jaeger

---

## Текущий релиз (MVP)

**Готово:**
- Аутентификация (BFF Pattern, HttpOnly cookies)
- Форум (доски, топики, комментарии, лайки)
- Игры (создание, персонажи, комнаты, посты)
- Сообщения (личные переписки, глобальные чат-события)
- Уведомления (SignalR)
- Поиск (OpenSearch)
- Блоги (создание, публикации, комментарии, лайки)
- Infrastructure hardening (Docker health checks, resource limits, secrets, CI/CD matrix builds)
- Мониторинг (Prometheus + Grafana dashboards + alerting rules)
- Database resilience (EF Core retry, MongoDB retry)
- Бэкап-скрипты (PostgreSQL, MongoDB, MinIO + verify)
- E2E тесты (Playwright, cookie auth)
- Remember me (опция "Запомнить меня")

---

## В работе

- Frontend страницы

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
| Зеркало в России | Подготовлено (ожидает домен) |

### Функциональность

| Задача | Описание |
|--------|----------|
| Invite links | Многоразовые ссылки-приглашения для игр/блогов (как в Discord) |
| Session transfer | Перенос сессий между зеркалами |

### Интеграции

| Задача | Описание |
|--------|----------|
| Discord бот | Уведомления (реализовать senders) |
| Telegram бот | Уведомления (реализовать senders) |

### Frontend страницы

| Страница | Описание |
|----------|----------|
| `/game/:id/characters` | Персонажи игры |
| `/notifications` | Страница уведомлений |
| `/blogs` | Блоги |
| `/moderation` | Модерация |
| `/support` | Поддержка |
| `/complaint` | Жалобы |

### Тесты

| Задача | Описание |
|--------|----------|
| Mail.Sender тесты | Unit тесты с моками SMTP |
| Search тесты | Unit тесты + OpenSearch mock |
| Расширить E2E | Playwright для всех страниц |

**Недостающие тест-проекты:**
- `DM.Workers.Mail.Tests`
- `DM.Workers.SearchIndexer.Tests`

### Кэширование

| Задача | Описание |
|--------|----------|
| User profiles | Redis/Memory cache |
| Forum moderators | Кэш списка модераторов |

---

## Безопасность

### Реализовано (NIST SP 800-63-4)

| Задача | Описание |
|--------|----------|
| HIBP проверка паролей | Сервер проверяет пароли через HIBP k-anonymity API |
| Предупреждение коротких паролей | Рекомендация 15+ символов на фронтенде |
| Device info в сессиях | IP, User-Agent, время создания сессии |
| Таймауты сессий | 24ч обычная / 30д "запомнить меня" (по NIST) |
| Argon2id хеширование | Все пароли хешируются Argon2id (19 MiB, 2 iter) |
| Security audit log | Логирование security-событий в MongoDB + API просмотра |

### AAL2 (для модераторов/админов)

| Задача | Описание |
|--------|----------|
| TOTP 2FA | Authenticator app для второго фактора |
| Обязательный 2FA для ролей | Принудительно для Admin/SeniorModerator |
| Backup codes | Резервные коды при потере устройства |
| 2FA recovery | Восстановление через email + поддержку |

### Будущие улучшения

| Задача | Описание |
|--------|----------|
| Password history | Запрет повторного использования паролей |

---

## Технический долг

| Задача | Файл/Область |
|--------|--------------|
| Рефакторинг BbParserWrapper | `src/DM.Infrastructure.Core/Parsing/BbParserWrapper.cs` |
| Удалить NotImplementedException | `SchemaFactory.cs` в Game модуле |
| Удалить console.log | Frontend |
| ConfigureAwait(false) | Все async методы |
| Разобраться с dm.am | Почему не грузится в России? |

---

## Рассматривается

| Задача | Описание |
|--------|----------|
| k3s + Helm + Terraform | Kubernetes (если масштаб вырастет) |
| Bun | Замена Node.js |
| Google OAuth | Вход через Google |
| Система менторства | Автоприветствие новичкам от менторов |

---

## Ссылки

- [Миграция со старого сайта](./DM2_MIGRATION.md)
- [Стандарты разработки](../conventions/CODE_STYLE.md)
