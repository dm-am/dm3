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
| Аутентификация | 5 req/min на IP | `/v1/account/login`, `/v1/account` |

**HTTP Status:** `429 Too Many Requests` при превышении лимита.

**Заголовки:** `X-RateLimit-Limit`, `X-RateLimit-Remaining`, `X-RateLimit-Reset`, `Retry-After` (при 429).

---

## Форматы ответов

### Envelope<T>

```json
{
  "resource": { /* объект */ },
  "metadata": { /* опционально */ }
}
```

### ListEnvelope<T>

```json
{
  "resources": [ /* массив */ ],
  "paging": {
    "number": 1,
    "size": 10,
    "totalPages": 5,
    "totalEntities": 50
  }
}
```

### CursorEnvelope<T>

```json
{
  "resources": [ /* массив */ ],
  "cursor": {
    "hasPrev": false,
    "hasNext": true,
    "prevCursor": null,
    "nextCursor": "base64..."
  }
}
```

### Формат ошибки

```json
{
  "message": "Описание ошибки",
  "errors": {
    "fieldName": ["Ошибка валидации"]
  }
}
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

См. [AUTHENTICATION.md](../architecture/AUTHENTICATION.md)

---

## Структура API

### Swagger Groups (8 групп)

| Группа | Теги | Описание |
|--------|------|----------|
| Account | Login, Registration, Profile, Security, Mirror | Аутентификация и управление аккаунтом |
| Blog | Blogs, Publications, Blog Invitations, Blog Notepads | Блоги и публикации |
| Community | Users, UserReviews, GameReviews, PostReviews, PlatformReviews, Polls, Statistics | Сообщество: профили, отзывы, опросы, статистика |
| Forum | Forum, Boards, Topics, Comments | Форумы и обсуждения |
| Game | Games, Rooms, Characters, Posts, Comments, Invitations, AttributeSchemas, Game Notepads | Игры и всё связанное |
| Messaging | Conversations, Messages, GlobalChat | Личные сообщения и чат |
| Common | Search, Uploads, Notepad Entries, Subscriptions, Notifications | Общие утилиты |
| Moderation | Moderation | Инструменты модерации |

### Принципы тегирования

- **Тег = первичный ресурс в URL** (`/v1/users/*` → Users)
- **Связанные действия = один тег** (stats, leaderboards, reports → Statistics)
- **Вложенные ресурсы = отдельный тег** (`/v1/users/{login}/reviews` → UserReviews)

---

## Бизнес-правила

### Регистрация

- Поле `website` в форме регистрации - honeypot для защиты от ботов (должно быть пустым)
- После регистрации требуется активация по email

### Роли и права

См. [RBAC.md](../architecture/RBAC.md)

### Привязка аватара

1. Загрузить изображение через `POST /v1/uploads/presign`
2. Передать `avatarUploadId` в `PATCH /v1/account`

---

## Ссылки

- [README](../README.md) — Обзор документации
- [Архитектура](../architecture/OVERVIEW.md) — Как устроено
- [Аутентификация](../architecture/AUTHENTICATION.md) — BFF и сессии
- [Глоссарий](../reference/GLOSSARY.md) — Термины и определения
- [Установка](../guides/SETUP.md) — Запуск проекта

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
