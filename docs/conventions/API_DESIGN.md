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

## Pagination

**Offset-based:** `?skip=20&take=10`
**Cursor-based:** `?cursor=abc&limit=50`

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
| Game | Игры и всё связанное |
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
