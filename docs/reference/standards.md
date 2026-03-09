# Стандарты разработки DM3

> **Принцип:** Код > документация. Паттерны смотреть в коде, здесь только правила.
>
> **Архитектура:** См. [patterns.md](../architecture/patterns.md) — паттерны, структура проектов, блюпринт.

---

## Часть 1: Code Standards

### Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Private fields | `_camelCase` | `_repository` |
| Async methods | `Async` suffix | `CreateAsync()` not `Create()` |
| Test class | `{Class}Should.cs` | `TopicCreatingServiceShould.cs` |
| Migration | `YYYYMMDDHHMMSS_{Name}.cs` | `20260115000000_AddSessions.cs` |
| Repository (unified) | `I{Feature}Repository.cs` | `ITopicRepository.cs` |
| AutoMapper profile | `{Feature}MappingProfile.cs` | `TopicMappingProfile.cs` |

### Forbidden Terms (Game/Blog context)

| Term | Replacement |
|------|-------------|
| `participant` | `user` (Game/Blog), `participant` OK in Messaging |
| `staff` | `master`, `assistant`, `moderator` |
| `follow`/`follower` | `subscribe`/`subscriber`/`reader` |
| `authority` | `canEdit`, `hasEditAccess`, `master/assistant` |
| `private blog/game` | `draft with private visibility` |
| `HasManagementAccess` | `HasEditAccess` |

### DI Lifetimes

| Lifetime | Use Case |
|----------|----------|
| `SingleInstance()` | Stateless: factories, providers |
| `InstancePerLifetimeScope()` | Request-scoped: services, repositories |

### Validation

FluentValidation для бизнес-правил, DataAnnotations для простых ограничений.

**Example:** `DM.Domain.Forum/Features/Topics/CreateTopicValidator.cs`

### Logging

**Добавлять ILogger:**
- Authentication services
- Business operations (create game, delete post)
- External integrations

**НЕ добавлять:**
- Repositories (EF tracing есть)
- Factories, Validators, IntentionResolvers

```csharp
_logger.LogWarning("Login failed. UserId={UserId}", user.UserId);  // structured
```

### Testing

**Pattern:** `{Behavior}_When_{Condition}`

**CancellationToken в моках:**
```csharp
// ✅ Correct
repository.Setup(r => r.Get(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
```

**Example:** `test/DM.Domain.Forum.Tests/TopicCreatingServiceShould.cs`

### Frontend (Vue.js)

**Branded types:**
```typescript
export type Message = {
  id: Served<MessageId>;      // Server-provided (read-only)
  text: string;                // User-editable
};
```

**Example:** `src/DM.Web.Client/src/api/models/messaging/index.ts`

### Security

| Aspect | Reference |
|--------|-----------|
| Password hash | Argon2id (19 MiB, 2 iter) — [security.md](../architecture/security.md) |
| Token encryption | AES-256-GCM — [security.md](../architecture/security.md) |
| Rate limiting | [api.md](./api.md#rate-limiting) |
| BFF Pattern | HttpOnly cookies, SameSite=Strict — [security.md](../architecture/security.md) |

### Checklists

#### New Feature
- [ ] Feature folder (flat, no CRUD subfolders) — см. [patterns.md](../architecture/patterns.md#целевые-структуры-блюпринт)
- [ ] Unified Service (`I{Feature}Service` + `{Feature}Service`) — все CRUD в одном сервисе
- [ ] Repository (`I{Feature}Repository` + `{Feature}Repository`)
- [ ] Intention enum + resolver (in `Authorization/`)
- [ ] FluentValidation validators
- [ ] AutoMapper profile (`{Feature}MappingProfile`)
- [ ] DI registration in `Web.API/Startup.cs` (RegisterDomainServices)

#### New Endpoint
- [ ] XML documentation
- [ ] `[ProducesResponseType]` for all codes
- [ ] `[AuthenticationRequired]` if needed
- [ ] Route name: `Name = nameof(...)`

#### Before Commit
- [ ] `dotnet build` — no errors
- [ ] `npm run type-check` — passes

### AI Agent Guidelines

#### ASP.NET Core Routing

**Literal routes have priority over templates (regardless of code order):**
```csharp
[HttpGet("{id}")]      // Template
[HttpGet("active")]    // Literal — always wins for "active"
```

#### Error Types

| Type | Use Case |
|------|----------|
| `BadRequestError` | Field validation (FluentValidation) |
| `GeneralError` | Business errors, invalid tokens, not found |

#### Git Rules

**NEVER use:**
- `git checkout` — destructive, loses uncommitted changes
- `git reset --hard` — same reason
- `git clean -fd` — deletes untracked files

**To undo changes:** Ask user or use `git stash`

#### Server Restart

**After changing Swagger groups, controllers, or startup config — restart server:**
```bash
taskkill /F /IM dotnet.exe
dotnet run --project src/DM.Web.API --urls "http://localhost:5000"
```

**Run in background** — use `run_in_background: true` parameter.

#### What NOT to Flag

1. ❌ "Route ordering bug" — ASP.NET Core handles it
2. ❌ "Password MinimumLength=1 in login" — hash comparison handles rejection
3. ❌ "GeneralError for invalid token" — correct, not field validation

#### What TO Flag

1. ✅ Missing `[ProducesResponseType]`
2. ✅ Empty XML documentation tags
3. ✅ Missing validation on Create/Update DTOs
4. ✅ Admin endpoints without `[RequireRole]`
5. ✅ DELETE returning Ok() instead of NoContent()
6. ✅ Duplicate endpoints (`{id:guid}` AND `{username}` for same resource)

#### API Design

**One canonical access pattern** — users by `{username}`, not GUID.

---

## Часть 2: API Standards

> **Подход:** Pragmatic REST
> **Вдохновлено:** Stripe, GitHub, Twilio APIs

### Философия

#### Принципы

1. **Понятность > Догма** — API должен быть интуитивным, а не "правильным по REST"
2. **Консистентность** — одинаковые паттерны везде
3. **Предсказуемость** — разработчик должен угадывать URL без документации
4. **Простота** — минимум сложности для решения задачи

#### Три типа endpoints

| Тип | Назначение | HTTP | Глаголы в URL |
|-----|------------|------|---------------|
| **Resource** | CRUD операции над сущностями | GET/POST/PATCH/DELETE | Нет |
| **Query** | Проверки, поиск, фильтрация | GET | Допустимы |
| **Action** | Операции, не вписывающиеся в CRUD | POST | Допустимы |

### Типы endpoints

#### 1. Resource Endpoints

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

#### 2. Query Endpoints

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

#### 3. Action Endpoints

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

### URL Structure

#### Базовые правила

| Правило | Пример | Неправильно |
|---------|--------|-------------|
| kebab-case | `/global-chat`, `/check-email` | `/globalChat`, `/checkEmail` |
| Lowercase | `/users` | `/Users` |
| Plural для коллекций | `/users`, `/games` | `/user`, `/game` |
| Singular для domains | `/account`, `/search` | `/accounts` (если не коллекция) |
| Без trailing slash | `/users` | `/users/` |
| Без /api prefix | `/v1/users` | `/api/v1/users` |

#### Структура

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

#### Максимум 3 сегмента после domain

```
# Правильно
/v1/games/{id}/rooms/{roomId}/posts

# Неправильно (слишком глубоко)
/v1/games/{id}/rooms/{roomId}/posts/{postId}/comments

# Решение: вынести на верхний уровень
/v1/posts/{id}/comments
```

#### Domains (группы)

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

> **Примечание:** `/v1/users/me/...` — специальный паттерн где `me` означает текущего аутентифицированного пользователя.

### HTTP Methods

| Метод | Использование | Идемпотентность | Body |
|-------|---------------|-----------------|------|
| GET | Получить данные | Да | Нет |
| POST | Создать ресурс / Выполнить action | Нет | Да |
| PATCH | Частичное обновление | Да | Да |
| PUT | Полная замена (редко) | Да | Да |
| DELETE | Удалить | Да | Нет |

### Response Codes

#### Успех

| Код | Когда | Пример |
|-----|-------|--------|
| 200 | GET успешный, PATCH/PUT успешный, Action успешный с данными | `GET /users/{id}` |
| 201 | POST создал ресурс | `POST /users` |
| 204 | DELETE успешный, Action без возвращаемых данных | `DELETE /users/{id}` |

#### Ошибки клиента

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

### Request/Response Format

#### JSON Conventions

| Правило | Пример |
|---------|--------|
| camelCase для полей | `userId`, `createdAt` |
| Boolean с is/has/can | `isActive`, `hasAccess`, `canEdit` |
| Даты в ISO 8601 UTC | `2025-01-01T00:00:00Z` |
| UUID для ID | `550e8400-e29b-41d4-a716-446655440000` |
| Null для отсутствующих значений | `"deletedAt": null` |
| Arrays — plural | `"users": [...]` |

#### Response Format

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

**C# примеры:**
```csharp
// Одиночный ресурс - возвращать напрямую
return Ok(user);

// Коллекция с pagination
return Ok(new ListEnvelope<User>(users, paging));
```

### Error Format

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

#### Коды ошибок

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

### Pagination

#### Offset-based (для списков с произвольным доступом)

```
GET /v1/users?skip=20&take=10
```

| Param | Описание | Default | Max |
|-------|----------|---------|-----|
| `skip` | Сколько пропустить | 0 | — |
| `take` | Сколько взять | 20 | 100 |

#### Cursor-based (для лент и real-time данных)

```
GET /v1/conversations/{id}/messages?cursor=abc123&limit=50
```

| Param | Описание | Default | Max |
|-------|----------|---------|-----|
| `cursor` | Позиция (opaque string) | null (начало) | — |
| `limit` | Сколько взять | 20 | 100 |

### Controller Organization

#### Feature Folders (Package by Feature)

**Почему Features/, а не Controllers/v1/:**
- Высокая когезия — Controller, ApiService, DTOs в одном месте
- Удаление фичи = удаление одной папки
- Масштабируется при 100+ endpoints
- Рекомендован Steve Smith (Ardalis), Jimmy Bogard

**Структура Features/:**
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

### Чеклист для новых endpoints

#### URL
- [ ] kebab-case
- [ ] Правильный тип (Resource/Query/Action)
- [ ] Глаголы только для Query и Action
- [ ] Max 3 сегмента после domain
- [ ] Plural для коллекций, singular для domains

#### HTTP
- [ ] Правильный метод (GET/POST/PATCH/DELETE)
- [ ] Правильные response codes
- [ ] Идемпотентность соблюдена

#### Response
- [ ] Одиночный ресурс — напрямую (без Envelope)
- [ ] Коллекция — ListEnvelope с paging
- [ ] camelCase поля
- [ ] Правильный error format (ErrorEnvelope)

#### Documentation
- [ ] Summary описан
- [ ] Response types задокументированы
- [ ] GroupName соответствует папке
- [ ] Tags заданы

---

## Ссылки

- [Паттерны и структура](../architecture/patterns.md) — Архитектура, блюпринт
- [Системный обзор](../architecture/overview.md) — Компоненты, порты
- [Глоссарий](./glossary.md) — Термины
- [API Reference](./api.md) — Справочник API
- [Тестирование](../guides/testing.md) — Запуск тестов
- [Stripe API Reference](https://stripe.com/docs/api) — эталон Pragmatic REST
- [GitHub REST API](https://docs.github.com/en/rest) — хороший пример
