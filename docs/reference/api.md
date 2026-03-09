# API Reference - DM3

> Детальная документация API доступна в Swagger: http://localhost:5000/

## Базовая информация

- **Base URL:** `https://api.dm.am` (production), `http://localhost:5000` (development)
- **API Version:** v1
- **Формат данных:** JSON
- **Кодировка:** UTF-8
- **Аутентификация:** HttpOnly Cookie (BFF Pattern)

---

## Rate Limiting

| Эндпоинты | Лимит | Описание |
|-----------|-------|----------|
| Глобальный | 100 req/min на IP | Все эндпоинты |
| Аутентификация | 5 req/min на IP | `/v1/account/login`, `/v1/account/password`, `/v1/account/recovery` |
| Username check | 20 req/min на IP | `/v1/account/check-username` |
| Login check | 10 req/min на IP | `/v1/account/check-email` |

**HTTP Status:** `429 Too Many Requests` при превышении лимита.

**Заголовки:** `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`, `Retry-After` (при 429).

---

## Форматы ответов

> Подробнее: [standards.md](./standards.md#response-format)

| Тип ответа | Формат |
|------------|--------|
| Одиночный ресурс | JSON напрямую |
| Коллекция | `ListEnvelope<T>` с `paging` |
| Курсорная пагинация | `CursorEnvelope<T>` с `cursor` |
| Ошибка | `ErrorEnvelope` |

```json
// Одиночный ресурс — напрямую
{ "id": "...", "username": "john" }

// Коллекция — ListEnvelope
{ "resources": [...], "paging": { "skip": 0, "take": 20, "total": 150 } }

// Ошибка — ErrorEnvelope
{ "errors": [{ "code": "not_found", "message": "User not found" }] }
```

---

## Коды ответов

| Код | Описание |
|-----|----------|
| 200 | Успешный запрос |
| 201 | Ресурс создан |
| 204 | Успешно, без содержимого (DELETE, действия) |
| 400 | Невалидные параметры |
| 401 | Требуется аутентификация |
| 403 | Недостаточно прав |
| 404 | Ресурс не найден |
| 409 | Конфликт состояния |
| 410 | Токен/приглашение истекло (только временные ресурсы) |
| 429 | Превышен rate limit |

---

## Health Endpoints

| Endpoint | Назначение | Пример ответа |
|----------|-----------|---------------|
| `GET /_health` | Liveness (Docker) | `Healthy` |
| `GET /_ready` | Readiness (PostgreSQL + MongoDB + RabbitMQ) | JSON с деталями |
| `GET /_health/detail` | Все проверки | JSON с деталями |
| `GET /metrics` | Prometheus metrics | text/plain |

---

## Аутентификация

См. [security.md](../architecture/security.md)

---

## Структура API

### Swagger Groups (9 групп)

| Группа | Теги | Описание |
|--------|------|----------|
| Account | Availability, Registration, Authentication, Credentials, Recovery, Deactivation, Security | Регистрация, вход, восстановление доступа |
| Personal | Profile, Preferences, Blacklist, Notifications (Bots), Invitations, Subscriptions, Notepads, Notes | Профиль и настройки текущего пользователя |
| Messaging | Global Chat, Chats, Messages | Личные сообщения и чат |
| Community | Users, Subscribers, User Reviews, Platform Reviews, Polls, Statistics | Сообщество: профили, отзывы, опросы |
| Game | Games, Game Reviews, Blacklist, Invitations, Notepads, Comments, Attribute Schemas, Characters, Rooms, Posts, Post Reviews | Игры и все связанное |
| Blog | Blogs, Blacklist, Invitations, Notepads, Rubrics, Publications, Comments | Блоги и публикации |
| Forum | Boards, Comments, Forum, Topics | Форумы и обсуждения |
| Moderation | Moderation, Users, Notes, Warnings, Bans, Tickets, Ticket Responses, Credentials | Инструменты модерации |
| General | Mirrors, Search, Uploads | Общие утилиты |

### Account endpoints

| Тег | Эндпоинты |
|-----|-----------|
| Availability | `GET /check-email`, `GET /check-username` |
| Registration | `POST /register`, `GET /activation/{token}`, `POST /activation/{token}` |
| Authentication | `POST /login`, `DELETE /login`, `GET /sessions`, `DELETE /sessions/{id}`, `DELETE /sessions/others` |
| Credentials | `POST /password`, `POST /email-change`, `POST /email-change/{token}` |
| Recovery | `POST /recovery`, `GET /password-reset/{token}`, `POST /password-reset/{token}` |
| Deactivation | `POST /deactivate` |
| Security | `GET /logs?type=...&limit=...` |

### Personal endpoints (`/v1/users/me`)

| Тег | Эндпоинты |
|-----|-----------|
| Profile | `GET /profile`, `PATCH /profile` |
| Preferences | `GET /preferences`, `PATCH /preferences` |
| Blacklist | `GET /blacklist`, `POST /blacklist`, `DELETE /blacklist/{username}`, `GET /blacklist/settings`, `PATCH /blacklist/settings` |
| Notifications | `POST /bots/telegram`, `POST /bots/discord`, `DELETE /bots/telegram`, `DELETE /bots/discord` |

### Messaging endpoints (`/v1/chats`)

| Тег | Эндпоинты |
|-----|-----------|
| Chats | `GET /chats`, `GET /chats/{id}`, `POST /chats`, `PATCH /chats/{id}`, `POST /chats/direct/{username}`, `GET /chats/can-start/{username}` |
| Messages | `GET /chats/{id}/messages`, `POST /chats/{id}/messages`, `GET /messages/{id}`, `PATCH /messages/{id}`, `DELETE /messages/{id}` |

### Community endpoints (`/v1/users`)

| Тег | Эндпоинты |
|-----|-----------|
| Users | `GET /users`, `GET /users/{username}` |
| Subscribers | `GET /users/{username}/subscribers`, `POST /users/{username}/subscribers`, `DELETE /users/{username}/subscribers`, `GET /users/{username}/subscribers/me` |

### Moderation endpoints (`/v1/moderation`)

> Доступ: Moderator, SeniorModerator, Admin

| Тег | Эндпоинты |
|-----|-----------|
| Users | `GET /users/{username}/profile`, `PATCH /users/{username}/profile`, `PATCH /users/{username}/role/{role}` |
| Notes | `GET /users/{username}/notes`, `GET /notes/{id}`, `POST /users/{username}/notes`, `PUT /notes/{id}`, `DELETE /notes/{id}` |
| Warnings | `GET /users/{username}/warnings`, `GET /warnings`, `POST /warnings`, `DELETE /warnings/{id}` |
| Bans | `GET /users/{username}/bans`, `GET /users/{username}/bans/active`, `GET /bans`, `POST /bans`, `DELETE /bans/{id}` |
| Tickets | `GET /tickets`, `GET /tickets/stats`, `GET /tickets/assigned`, `GET /tickets/mine`, `GET /tickets/{id}` |
| Ticket Actions | `POST /tickets`, `POST /tickets/{id}/assign`, `POST /tickets/{id}/resolve` |
| Ticket Responses | `GET /tickets/{id}/responses`, `POST /tickets/{id}/responses` |
| Credentials | `GET /username-change-requests`, `GET /username-change-requests/{id}`, `POST /username-change-requests/{id}/approve`, `POST /username-change-requests/{id}/reject`, `POST /username-change-requests/{id}/rollback` |

**Права по ролям:**
- **Moderator**: Просмотр профилей, предупреждения, временные баны (до 30 дней), тикеты
- **SeniorModerator**: + длительные баны, изменение ролей (до Moderator)
- **Admin**: + перманентные баны, все роли, полный доступ

### Blog Users endpoints (`/v1/blogs/{id}/users`)

| Тег | Эндпоинты | Описание |
|-----|-----------|----------|
| Users | `GET /?role=...` | Все пользователи блога (owner, assistants, readers). Фильтр по роли. |
| Users | `DELETE /{userId}` | Удалить пользователя (только assistants) |
| Assistants | `GET /assistants` | Список ассистентов |
| Assistants | `DELETE /assistants/{username}` | Удалить ассистента |
| Readers | `GET /readers` | Список читателей (подписчиков) |
| Readers | `POST /readers` | Подписаться на блог |
| Readers | `DELETE /readers` | Отписаться от блога |

**Примечания:**
- Readers хранятся в таблице Subscriptions (SubscriptionTargetType.Blog)
- Assistants хранятся в таблице BlogAssistants
- Readers не могут быть удалены владельцем — только заблокированы через blacklist
- Для черновых блогов с приватной видимостью (DraftVisibility.Private) требуется приглашение

### General endpoints

| Тег | Эндпоинты |
|-----|-----------|
| Mirrors | `GET /mirrors`, `GET /mirrors/transfer`, `POST /mirrors/transfer/accept` |
| Search | `GET /search` |
| Uploads | `POST /uploads/presign` |

### Принципы тегирования

- **Группа = домен** — Account для аутентификации, Profile для данных пользователя
- **Тег = функциональная область** (Availability, Registration, Authentication, etc.)
- **Связанные действия = один тег** (password, email-change, username-change → Credentials)

---

## Бизнес-правила

### Регистрация (Email-first flow)

1. `GET /check-email` — проверка доступности email
2. `POST /register` — создание pending регистрации (email + password)
3. Email с ссылкой на активацию
4. `GET /activation/{token}` — проверка статуса токена
5. `GET /check-username` — проверка доступности имени
6. `POST /activation/{token}` — завершение с выбором имени

**Примечания:**
- Поле `website` в форме — honeypot для защиты от ботов (должно быть пустым)
- Токен активации действителен 48 часов
- При истечении токена: `POST /recovery` отправит новую ссылку

### Восстановление доступа (Unified Recovery)

`POST /recovery` автоматически определяет нужное действие:
- **Активный аккаунт** → отправка ссылки на сброс пароля
- **Pending регистрация** → повторная отправка ссылки активации
- **Email не найден** → возврат `{ status: "NotFound" }` (без раскрытия информации)

### Роли и права

См. [security.md](../architecture/security.md)

### Привязка аватара

1. Загрузить изображение через `POST /v1/uploads/presign`
2. Передать `avatarUploadId` в `PATCH /v1/account`

---

## Ссылки

- [Стандарты](./standards.md) — Эталон и правила проектирования API
- [Архитектура](../architecture/overview.md) — Как устроено
- [Безопасность](../architecture/security.md) — Аутентификация и авторизация
- [Глоссарий](./glossary.md) — Термины и определения
- [Установка](../guides/setup.md) — Запуск проекта

