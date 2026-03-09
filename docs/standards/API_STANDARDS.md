# API Standards — DM3

> **Подход:** Pragmatic REST
> **Вдохновлено:** Stripe, GitHub, Twilio APIs

---

## Философия

### Принципы

1. **Понятность > Догма** — API должен быть интуитивным, а не "правильным по REST"
2. **Консистентность** — одинаковые паттерны везде
3. **Предсказуемость** — разработчик должен угадывать URL без документации
4. **Простота** — минимум сложности для решения задачи

### Три типа endpoints

| Тип | Назначение | HTTP | Глаголы в URL |
|-----|------------|------|---------------|
| **Resource** | CRUD операции над сущностями | GET/POST/PATCH/DELETE | Нет |
| **Query** | Проверки, поиск, фильтрация | GET | Допустимы |
| **Action** | Операции, не вписывающиеся в CRUD | POST | Допустимы |

---

## Типы endpoints

### 1. Resource Endpoints

Для CRUD операций над сущностями. Строгий REST.

```
GET    /v1/users              # Список
POST   /v1/users              # Создать
GET    /v1/users/{id}         # Получить
PATCH  /v1/users/{id}         # Обновить
DELETE /v1/users/{id}         # Удалить
```

**Правила:**
- Plural naming (`/users`, не `/user`)
- Без глаголов
- ID в path

**Вложенные ресурсы:**
```
GET  /v1/games/{id}/characters      # Персонажи игры
POST /v1/games/{id}/characters      # Создать персонажа
GET  /v1/characters/{id}            # Конкретный персонаж (top-level)
```

### 2. Query Endpoints

Для проверок и поиска. Глаголы допустимы.

```
GET /v1/account/check-email?email=user@example.com
GET /v1/account/check-username?username=john
GET /v1/search?q=keyword&type=users
GET /v1/games/{id}/can-join
```

**Правила:**
- Всегда GET (read-only)
- Параметры в query string
- Возвращают данные или boolean-результат

**Паттерны именования:**
| Паттерн | Пример | Когда использовать |
|---------|--------|-------------------|
| `check-{what}` | `check-email` | Проверка доступности/валидности |
| `can-{action}` | `can-join` | Проверка возможности действия |
| `search` | `search?q=...` | Полнотекстовый поиск |
| `lookup` | `lookup?ids=1,2,3` | Batch получение по ID |

### 3. Action Endpoints

Для операций, не вписывающихся в CRUD. Глаголы допустимы.

```
POST /v1/account/register
POST /v1/account/activate
POST /v1/account/resend-activation
POST /v1/games/{id}/join
POST /v1/games/{id}/leave
POST /v1/users/{id}/ban
POST /v1/posts/{id}/publish
```

**Правила:**
- Всегда POST (изменяет состояние)
- Глагол описывает действие
- Может быть на ресурсе (`/users/{id}/ban`) или standalone (`/account/register`)

**Паттерны именования:**
| Паттерн | Пример | Когда использовать |
|---------|--------|-------------------|
| `{action}` | `register`, `activate` | Standalone действие |
| `{resource}/{id}/{action}` | `users/{id}/ban` | Действие над ресурсом |
| `{action}-{what}` | `resend-activation` | Уточнение действия |

---

## URL Structure

### Базовые правила

| Правило | Пример | Неправильно |
|---------|--------|-------------|
| kebab-case | `/global-chat`, `/check-email` | `/globalChat`, `/checkEmail` |
| Lowercase | `/users` | `/Users` |
| Plural для коллекций | `/users`, `/games` | `/user`, `/game` |
| Singular для domains | `/account`, `/search` | `/accounts` (если не коллекция) |
| Без trailing slash | `/users` | `/users/` |
| Без /api prefix | `/v1/users` | `/api/v1/users` |

### Структура

```
https://api.dm.am/v1/{domain}/{resource}/{id}/{sub-resource|action}
```

**Примеры:**
```
/v1/account/register                    # domain + action
/v1/account/check-email                 # domain + query
/v1/users                               # resource collection
/v1/users/{username}                    # resource
/v1/users/{username}/reviews            # sub-resource
/v1/games/{id}/join                     # resource + action
```

### Максимум 3 сегмента после domain

```
# Правильно
/v1/games/{id}/rooms/{roomId}/posts

# Неправильно (слишком глубоко)
/v1/games/{id}/rooms/{roomId}/posts/{postId}/comments

# Решение: вынести на верхний уровень
/v1/posts/{id}/comments
```

### Domains (группы)

| Domain | Назначение |
|--------|------------|
| `/v1/account/...` | Регистрация, авторизация, credentials, безопасность |
| `/v1/users/me/...` | Профиль и данные текущего пользователя (Personal) |
| `/v1/users/...` | Публичные профили пользователей (Community) |
| `/v1/games/...` | Игры и связанные сущности |
| `/v1/blogs/...` | Блоги и публикации |
| `/v1/forum/...` | Форум |
| `/v1/messaging/...` | Личные сообщения |
| `/v1/moderation/...` | Модерация |
| `/v1/search` | Поиск |

> **Примечание:** `/v1/users/me/...` — специальный паттерн где `me` означает текущего аутентифицированного пользователя. Возвращает приватные данные (email, preferences, privacy).

---

## HTTP Methods

| Метод | Использование | Идемпотентность | Body |
|-------|---------------|-----------------|------|
| GET | Получить данные | Да | Нет |
| POST | Создать ресурс / Выполнить action | Нет | Да |
| PATCH | Частичное обновление | Да | Да |
| PUT | Полная замена (редко) | Да | Да |
| DELETE | Удалить | Да | Нет |

### Когда POST vs PATCH vs PUT

| Ситуация | Метод |
|----------|-------|
| Создание нового ресурса | POST |
| Обновление части полей | PATCH |
| Полная замена ресурса | PUT |
| Любое действие (action) | POST |
| Операции с side effects | POST |

---

## Response Codes

### Успех

| Код | Когда | Пример |
|-----|-------|--------|
| 200 | GET успешный, PATCH/PUT успешный, Action успешный с данными | `GET /users/{id}` |
| 201 | POST создал ресурс | `POST /users` |
| 204 | DELETE успешный, Action без возвращаемых данных | `DELETE /users/{id}` |

### Ошибки клиента

| Код | Когда | Пример |
|-----|-------|--------|
| 400 | Невалидный запрос, ошибки валидации | Неверный формат email |
| 401 | Не аутентифицирован | Нет токена |
| 403 | Нет прав | Попытка удалить чужой пост |
| 404 | Ресурс не найден | Несуществующий user ID |
| 409 | Конфликт состояния | Email уже занят |
| 410 | Ресурс удалён/истёк | Истёкший токен активации |
| 422 | Бизнес-логика не позволяет | Нельзя забанить админа |
| 429 | Rate limit | Слишком много запросов |

### Ошибки сервера

| Код | Когда |
|-----|-------|
| 500 | Неожиданная ошибка сервера |
| 503 | Сервис временно недоступен |

---

## Request/Response Format

### JSON Conventions

| Правило | Пример |
|---------|--------|
| camelCase для полей | `userId`, `createdAt` |
| Boolean с is/has/can | `isActive`, `hasAccess`, `canEdit` |
| Даты в ISO 8601 UTC | `2025-01-01T00:00:00Z` |
| UUID для ID | `550e8400-e29b-41d4-a716-446655440000` |
| Null для отсутствующих значений | `"deletedAt": null` |
| Arrays — plural | `"users": [...]` |

### Response Format

**Одиночный ресурс** — возвращается напрямую (без обёртки):
```json
{
  "id": "550e8400-e29b-41d4-a716-446655440000",
  "username": "john",
  "email": "john@example.com",
  "createdAt": "2025-01-01T00:00:00Z",
  "isActive": true
}
```

**Коллекция** — обёрнута в `ListEnvelope` (для pagination):
```json
{
  "resources": [
    { "id": "...", "username": "john" },
    { "id": "...", "username": "jane" }
  ],
  "paging": {
    "skip": 0,
    "take": 20,
    "total": 150
  }
}
```

**Query результат** — возвращается напрямую:
```json
{
  "available": true,
  "reason": "already_registered"
}
```

**Action результат** — возвращается напрямую:
```json
{
  "status": "sent",
  "email": "user@example.com"
}
```

### Почему так

| Тип | Формат | Причина |
|-----|--------|---------|
| Одиночный ресурс | Напрямую | REST стандарт, меньше вложенности, как GitHub/Stripe |
| Коллекция | `ListEnvelope` | Нужен `paging`, нельзя добавить метаданные к массиву |
| Ошибка | `ErrorEnvelope` | Единый формат ошибок |

### C# классы

```csharp
// Одиночный ресурс - возвращать напрямую
return Ok(user);

// Коллекция с pagination
return Ok(new ListEnvelope<User>(users, paging));

// Коллекция без pagination
return Ok(new ListEnvelope<User>(users));
```

---

## Error Format

### Структура ошибки

```json
{
  "errors": [
    {
      "code": "validation_error",
      "message": "Request validation failed",
      "field": "email"
    }
  ]
}
```

### Коды ошибок

| Код | HTTP | Описание |
|-----|------|----------|
| `validation_error` | 400 | Ошибки валидации полей |
| `invalid_request` | 400 | Неверный формат запроса |
| `unauthorized` | 401 | Требуется аутентификация |
| `forbidden` | 403 | Недостаточно прав |
| `not_found` | 404 | Ресурс не найден |
| `conflict` | 409 | Конфликт (уже существует) |
| `gone` | 410 | Ресурс удалён/истёк |
| `business_error` | 422 | Бизнес-логика не позволяет |
| `rate_limited` | 429 | Превышен лимит запросов |
| `server_error` | 500 | Внутренняя ошибка |

### Примеры ошибок

**Validation error (400):**
```json
{
  "errors": [
    { "code": "validation_error", "field": "email", "message": "Email is required" },
    { "code": "validation_error", "field": "password", "message": "Must be at least 8 characters" }
  ]
}
```

**Not found (404):**
```json
{
  "errors": [
    { "code": "not_found", "message": "User not found" }
  ]
}
```

**Business error (422):**
```json
{
  "errors": [
    { "code": "business_error", "message": "Cannot ban an administrator" }
  ]
}
```

---

## Pagination

### Offset-based (для списков с произвольным доступом)

```
GET /v1/users?skip=20&take=10
```

| Param | Описание | Default | Max |
|-------|----------|---------|-----|
| `skip` | Сколько пропустить | 0 | — |
| `take` | Сколько взять | 20 | 100 |

**Response:**
```json
{
  "resources": [...],
  "paging": {
    "skip": 20,
    "take": 10,
    "total": 150
  }
}
```

### Cursor-based (для лент и real-time данных)

```
GET /v1/conversations/{id}/messages?cursor=abc123&limit=50
```

| Param | Описание | Default | Max |
|-------|----------|---------|-----|
| `cursor` | Позиция (opaque string) | null (начало) | — |
| `limit` | Сколько взять | 20 | 100 |

**Response:**
```json
{
  "resources": [...],
  "paging": {
    "nextCursor": "xyz789",
    "hasMore": true
  }
}
```

### Когда что использовать

| Случай | Тип |
|--------|-----|
| Таблица с номерами страниц | Offset |
| Infinite scroll | Cursor |
| Real-time лента | Cursor |
| Админка со страницами | Offset |

---

## Versioning

### Стратегия: URL versioning

```
/v1/users
/v2/users  (будущее)
```

### Правила изменений

| Изменение | Требует новую версию? |
|-----------|----------------------|
| Добавление нового поля | Нет |
| Добавление нового endpoint | Нет |
| Удаление поля | Да |
| Переименование поля | Да |
| Изменение типа поля | Да |
| Изменение семантики | Да |
| Изменение URL | Да |

---

## Authentication

### Способ передачи

Cookie-based (BFF pattern):
```
Cookie: session=xxx
```

Или Bearer token (для external clients):
```
Authorization: Bearer xxx
```

### Ответы при ошибках auth

**Не аутентифицирован (401):**
```json
{
  "errors": [
    { "code": "unauthorized", "message": "Authentication required" }
  ]
}
```

**Нет прав (403):**
```json
{
  "errors": [
    { "code": "forbidden", "message": "You don't have permission to perform this action" }
  ]
}
```

---

## Controller Organization

### Feature Folders (Package by Feature)

**Почему Features/, а не Controllers/v1/:**
- Высокая когезия — Controller, ApiService, DTOs в одном месте
- Удаление фичи = удаление одной папки
- Масштабируется при 100+ endpoints
- Рекомендован Steve Smith (Ardalis), Jimmy Bogard

**Три отдельных концерна:**

| Концерн | Определяется | Пример |
|---------|-------------|--------|
| Файловая структура | Папками | `Features/Account/Authentication/` |
| URL версионирование | Атрибутом `[Route]` | `[Route("v1/account")]` |
| Swagger группы | Атрибутом `[ApiExplorerSettings]` | `GroupName = "Account"` |

### Структура Features/

```
Features/
├── Account/        → GroupName = "Account"    (/v1/account/...)
├── Personal/       → GroupName = "Personal"   (/v1/users/me/...)
├── Community/      → GroupName = "Community"  (/v1/users/...)
├── Blog/           → GroupName = "Blog"
├── General/        → GroupName = "General"
├── Forum/          → GroupName = "Forum"
├── Game/           → GroupName = "Game"
├── Messaging/      → GroupName = "Messaging"
└── Moderation/     → GroupName = "Moderation"
```

### Версионирование API

Версия в URL (`v1`), но определяется атрибутами, **НЕ папками**:

```csharp
// Текущий v1
[Route("v1/account")]
public class AuthenticationController { }

// При необходимости v2 (breaking changes):
[Route("v2/account")]
public class AuthenticationV2Controller { }
```

### Controller Template

```csharp
[ApiController]
[Route("v1/users")]
[ApiExplorerSettings(GroupName = "Community")]
[Tags("Users")]
public class UserController : ControllerBase
{
    /// <summary>
    /// Get user by username
    /// </summary>
    /// <response code="200">User found</response>
    /// <response code="404">User not found</response>
    [HttpGet("{username}")]
    [ProducesResponseType(typeof(User), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ErrorEnvelope), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetUser(string username)
    {
        var user = await _userService.GetByUsername(username);
        return Ok(user);  // Без обёртки
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ListEnvelope<User>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetUsers([FromQuery] PagingQuery query)
    {
        var (users, paging) = await _userService.GetList(query);
        return Ok(new ListEnvelope<User>(users, paging));  // С обёрткой для pagination
    }
}
```

### Несколько routes в одном контроллере

```csharp
[Route("v1/account")]
public class RegistrationController : ControllerBase
{
    [HttpPost("register")]           // POST /v1/account/register
    public Task<IActionResult> Register(...)

    [HttpGet("check-email")]         // GET /v1/account/check-email
    public Task<IActionResult> CheckEmail(...)

    [HttpPost("activate")]           // POST /v1/account/activate
    public Task<IActionResult> Activate(...)
}
```

---

## Чеклист для новых endpoints

### URL
- [ ] kebab-case
- [ ] Правильный тип (Resource/Query/Action)
- [ ] Глаголы только для Query и Action
- [ ] Max 3 сегмента после domain
- [ ] Plural для коллекций, singular для domains

### HTTP
- [ ] Правильный метод (GET/POST/PATCH/DELETE)
- [ ] Правильные response codes
- [ ] Идемпотентность соблюдена

### Response
- [ ] Одиночный ресурс — напрямую (без Envelope)
- [ ] Коллекция — ListEnvelope с paging
- [ ] camelCase поля
- [ ] Правильный error format (ErrorEnvelope)

### Documentation
- [ ] Summary описан
- [ ] Response types задокументированы
- [ ] GroupName соответствует папке
- [ ] Tags заданы

---

## Примеры: Registration Workflow

```
# Query: проверить email
GET /v1/account/check-email?email=user@example.com
→ 200 { "available": true }
→ 200 { "available": false, "reason": "already_registered" }

# Action: создать аккаунт
POST /v1/account/register
Body: { "email": "user@example.com", "password": "..." }
→ 201 { "email": "user@example.com" }
→ 409 { "errors": [{ "code": "conflict", "message": "Email already registered" }] }

# Resource: получить статус активации
GET /v1/account/activation/{token}
→ 200 { "status": "ready", "email": "user@example.com" }
→ 410 { "errors": [{ "code": "gone", "message": "Token expired" }] }

# Query: проверить username
GET /v1/account/check-username?username=john
→ 200 { "available": true }

# Action: завершить активацию
POST /v1/account/activation/{token}
Body: { "username": "john" }
→ 200 { "user": {...}, "token": "...", "preferences": {...} }

# Action: переслать активацию (через recovery)
POST /v1/account/recovery
Body: { "email": "user@example.com" }
→ 200 { "status": "activationResent" }
→ 404 { "errors": [{ "code": "not_found", "message": "Email not found" }] }
```

---

## Ссылки

- [Stripe API Reference](https://stripe.com/docs/api) — эталон Pragmatic REST
- [GitHub REST API](https://docs.github.com/en/rest) — хороший пример
- [Microsoft REST API Guidelines](https://github.com/microsoft/api-guidelines)
- [RFC 7231 - HTTP/1.1 Semantics](https://tools.ietf.org/html/rfc7231)
