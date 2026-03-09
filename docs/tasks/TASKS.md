# Задачи DM3

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
| Invite links | Многоразовые ссылки-приглашения для игр/блогов (как в Discord: код, опционально лимит/срок, статистика) |

### Интеграции

| Задача | Описание |
|--------|----------|
| Discord бот | Уведомления (реализовать senders) |
| Telegram бот | Уведомления (реализовать senders) |

### Frontend

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

## Рассматривается

| Задача | Описание |
|--------|----------|
| k3s + Helm + Terraform | Kubernetes (если масштаб вырастет) |
| Bun | Замена Node.js |
| Google OAuth | Вход через Google |
| Система менторства | Автоприветствие новичкам от менторов (удалён MentorGreetingsMessage — реализовать заново при планировании модерации) |

---

## Технический долг

| Задача | Файл/Область |
|--------|--------------|
| Рефакторинг BbParserWrapper | `src/DM.Infrastructure.Core/Parsing/BbParserWrapper.cs` |
| Удалить console.log | Frontend |
| ConfigureAwait(false) | Все async методы |
| Разобраться с dm.am | Почему не грузится в России? |

---

## Ссылки

- [Миграция со старого сайта](./MIGRATION.md)
- [Стандарты кода](../standards/CODE.md)
