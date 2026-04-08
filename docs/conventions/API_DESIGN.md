# API Design — DM3

> **User Story:** "Какие принципы API? Какие форматы ответов?"

> **SSOT:** Полный список endpoint'ов — в Swagger: http://localhost:5000/

---

## Базовая информация

| Параметр | Значение |
|----------|----------|
| Base URL | `https://api.dm.am` (prod), `http://localhost:5000` (dev) |
| API Version | v1 |
| Формат | JSON, UTF-8 |
| Аутентификация | HttpOnly Cookie (BFF Pattern) |

---

## Философия

> **Подход:** Pragmatic REST (Stripe, GitHub)

1. **Понятность > Догма** — читаемые URL важнее REST-пуризма
2. **Консистентность** — одинаковые паттерны везде
3. **Предсказуемость** — ожидаемое поведение
4. **Простота** — минимум сложности

---

## Три типа endpoints

| Тип | HTTP | Глаголы в URL | Пример |
|-----|------|---------------|--------|
| **Resource** | GET/POST/PATCH/DELETE | Нет | `/users/{id}` |
| **Query** | GET | Допустимы | `/account/check-email` |
| **Action** | POST | Допустимы | `/account/login`, `/games/{id}/join` |

---

## Форматы ответов

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

| Код | Когда использовать |
|-----|-------------------|
| 200 | Успешный запрос |
| 201 | Ресурс создан (POST) |
| 204 | Успешно, без тела (DELETE, actions) |
| 400 | Невалидные параметры |
| 401 | Требуется аутентификация |
| 403 | Недостаточно прав |
| 404 | Ресурс не найден |
| 409 | Конфликт состояния |
| 410 | Токен/приглашение истекло |
| 429 | Rate limit превышен |

---

## Pagination, Filtering & Sorting

### Принцип: Один endpoint — много сценариев

> **Правило:** Вместо специализированных endpoints (`/popular`, `/active`, `/owned`)
> используй **один list endpoint** с параметрами фильтрации и сортировки.

**Преимущества:**
- Меньше endpoints = проще API
- Гибкость для клиента
- Единая логика авторизации
- Кэширование по URL

### Pagination

| Тип | Параметры | Использование |
|-----|-----------|---------------|
| Offset | `?skip=20&take=10` | Списки с известным total |
| Cursor | `?cursor=abc&limit=50` | Бесконечный скролл |

### Filtering

```
GET /v1/games?statuses=Active,Draft&authorUsernames=ivan&requiredTags=42
GET /v1/posts?roomId=abc&authorId=xyz
GET /v1/users?role=moderator&isOnline=true
```

**Паттерны:**
- Enum фильтры: `?statuses=Active,Draft` (массив через запятую или повтор параметра)
- Boolean: `?isOpen=true`
- ID reference: `?authorId=abc`
- Числовые: `?minRating=5&maxPlayers=10`

### Sorting

```
GET /v1/games?sort=subscribersCount:desc     # Popular = по подписчикам
GET /v1/games?sort=lastPostUtc:desc          # Active = по последнему посту
GET /v1/games?sort=createdUtc:desc           # Newest
GET /v1/posts?sort=rating:desc&take=1        # Best post
```

**Формат:** `?sort=field:asc|desc` (default: `desc`)

### Примеры замены специальных endpoints

| Было (отдельный endpoint) | Стало (query parameters) |
|---------------------------|-------------------------|
| `GET /games/popular` | `GET /games?sortBy=popularity&take=10` |
| `GET /games/owned` | `GET /games?participating=true` |
| `GET /blogs/popular` | `GET /blogs?sortBy=popularity&take=10` |
| `GET /blogs/owned` | `GET /blogs?participating=true` |
| `GET /posts/best` | `GET /posts?sortBy=rating&take=1` |

> **`me` keyword:** Для текущего пользователя можно использовать `authorId=me`
> или отдельный флаг `?participating=true`.

### Когда отдельный endpoint оправдан

1. **Actions** (изменяют состояние): `/games/{id}/join`, `/posts/{id}/like`
2. **Агрегации** (сложные вычисления): `/statistics/overview`
3. **Специфичные форматы**: `/export/csv`

---

## Rate Limiting

| Эндпоинты | Лимит |
|-----------|-------|
| Глобальный | 100 req/min на IP |
| Аутентификация | 5 req/min на IP |
| Username check | 20 req/min на IP |
| Email check | 10 req/min на IP |

**При превышении:** `429 Too Many Requests` + заголовки `X-RateLimit-*`, `Retry-After`

---

## Health Endpoints

| Endpoint | Назначение |
|----------|-----------|
| `GET /_health` | Liveness (Docker) |
| `GET /_ready` | Readiness (DB checks) |
| `GET /_health/detail` | Детальная информация |
| `GET /metrics` | Prometheus metrics |

---

## Realtime (SignalR)

| Параметр | Значение |
|----------|----------|
| Endpoint | `/whatsup` |
| Протокол | WebSocket |
| Аутентификация | Query parameter `access_token` |

**Особенности:**
- Receive-only (клиент не вызывает методы)
- Connection tracking — in-memory
- Адресация по connection ID (без групп)

```javascript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("/whatsup?access_token=" + token)
  .build();

connection.on("Send", (notification) => {
  console.log(notification.eventType, notification.payload);
});
```

---

## Swagger Groups

API организован в 9 групп по доменам:

| Группа | Описание |
|--------|----------|
| Account | Регистрация, вход, восстановление |
| Personal | Профиль и настройки текущего пользователя |
| Messaging | Личные сообщения и чат |
| Community | Профили, отзывы, опросы |
| Game | Игры и все связанное |
| Blog | Блоги и публикации |
| Forum | Форумы и обсуждения |
| Moderation | Инструменты модерации |
| General | Зеркала, поиск, загрузки |

**Детали:** см. Swagger UI

---

## Ссылки

- [CODE_STYLE.md](./CODE_STYLE.md) — стандарты кода
- [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) — как работает вход
- [AUTHORIZATION.md](../architecture/AUTHORIZATION.md) — роли и права
