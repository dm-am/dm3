# DM3 Code Standards

> **Принцип:** Код > документация. Паттерны смотреть в коде, здесь только правила.

---

## Архитектура

**Hexagonal Architecture (Ports & Adapters)**

```
Controller → ApiService → DomainService → Repository → Database
                ↓
            AutoMapper (DTO mapping)
```

**Примеры в коде:**
- Service: `DM.Services.Forum/BusinessProcesses/Topics/Creating/TopicCreatingService.cs`
- Repository: `DM.Services.Forum/BusinessProcesses/Topics/Creating/TopicCreatingRepository.cs`
- Factory: `DM.Services.Forum/BusinessProcesses/Topics/Creating/TopicFactory.cs`

---

## Naming Conventions

| Element | Convention | Example |
|---------|------------|---------|
| Private fields | `_camelCase` | `_repository` |
| Async methods | No `Async` suffix | `Create()` not `CreateAsync()` |
| Test class | `{Class}Should.cs` | `TopicCreatingServiceShould.cs` |
| Migration | `YYYYMMDDHHMMSS_{Name}.cs` | `20260115000000_AddSessions.cs` |

**File structure:** `BusinessProcesses/{Feature}/{Creating,Reading,Updating,Deleting}/`

---

## DI Lifetimes

| Lifetime | Use Case |
|----------|----------|
| `SingleInstance()` | Stateless: factories, providers |
| `InstancePerLifetimeScope()` | Request-scoped: services, repositories |

---

## API Rules

**HTTP Methods:**
- POST = create / action
- PATCH = partial update
- DELETE = returns 204 NoContent

**HTTP Status Codes:**
- 404 Not Found = resource doesn't exist (never use 410 Gone)
- 409 Conflict = duplicate resource, already liked, etc.
- 400 Bad Request = validation errors (field-level)
- 403 Forbidden = authorization denied

**Required attributes:**
```csharp
[ProducesResponseType(typeof(Envelope<T>), 200)]
[ProducesResponseType(typeof(GeneralError), 404)]
```

**Example:** `DM.Web.API/Controllers/v1/Forum/TopicController.cs`

---

## Validation

FluentValidation для бизнес-правил, DataAnnotations для простых ограничений.

**Example:** `DM.Services.Forum/Dto/Input/CreateTopicValidator.cs`

---

## Logging

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

---

## Testing

**Pattern:** `{Behavior}_When_{Condition}`

**CancellationToken в моках:**
```csharp
// ✅ Correct
repository.Setup(r => r.Get(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
```

**Example:** `test/DM.Services.Forum.Tests/TopicCreatingServiceShould.cs`

---

## Frontend (Vue.js)

**Branded types:**
```typescript
export type Message = {
  id: Served<MessageId>;      // Server-provided (read-only)
  text: string;                // User-editable
};
```

**Example:** `frontend/DM.Web.Modern/src/api/models/messaging/index.ts`

---

## Security

| Aspect | Reference |
|--------|-----------|
| Password hash | PBKDF2-SHA256, 600K iterations — [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| Token encryption | AES-256-GCM — [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| Rate limiting | [REFERENCE.md](../api/REFERENCE.md#rate-limiting) |
| BFF Pattern | HttpOnly cookies, SameSite=Strict — [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |

---

## Checklists

### New Service
- [ ] CRUD folder structure
- [ ] Intention enum + resolver
- [ ] FluentValidation validator
- [ ] AutoMapper profile
- [ ] Autofac registration

### New Endpoint
- [ ] XML documentation
- [ ] `[ProducesResponseType]` for all codes
- [ ] `[AuthenticationRequired]` if needed
- [ ] Route name: `Name = nameof(...)`

### Before Commit
- [ ] `dotnet build` — no errors
- [ ] `npm run type-check` — passes

---

## AI Agent Guidelines

### ASP.NET Core Routing

**Literal routes have priority over templates (regardless of code order):**
```csharp
[HttpGet("{id}")]      // Template
[HttpGet("active")]    // Literal — always wins for "active"
```

### Error Types

| Type | Use Case |
|------|----------|
| `BadRequestError` | Field validation (FluentValidation) |
| `GeneralError` | Business errors, invalid tokens, not found |

### Git Rules

**NEVER use:**
- `git checkout` — destructive, loses uncommitted changes
- `git reset --hard` — same reason
- `git clean -fd` — deletes untracked files

**To undo changes:** Ask user or use `git stash`

### Server Restart

**After changing Swagger groups, controllers, or startup config — restart server:**
```bash
taskkill /F /IM dotnet.exe
dotnet run --project src/DM.Web.API --urls "http://localhost:5000"
```

**Run in background** — use `run_in_background: true` parameter.

### What NOT to Flag

1. ❌ "Route ordering bug" — ASP.NET Core handles it
2. ❌ "Password MinimumLength=1 in login" — hash comparison handles rejection
3. ❌ "GeneralError for invalid token" — correct, not field validation

### What TO Flag

1. ✅ Missing `[ProducesResponseType]`
2. ✅ Empty XML documentation tags
3. ✅ Missing validation on Create/Update DTOs
4. ✅ Admin endpoints without `[RequireRole]`
5. ✅ DELETE returning Ok() instead of NoContent()
6. ✅ Duplicate endpoints (`{id:guid}` AND `{login}` for same resource)

### API Design

**One canonical access pattern:**
```csharp
// ✅ CORRECT
[HttpGet("{login}")]  // login is unique

// ❌ WRONG — duplicates
[HttpGet("{id:guid}")]
[HttpGet("{login}")]
```

**Don't duplicate:**
- `GET /v1/account` = current user
- `GET /v1/users/{login}` = any user
- ❌ `GET /v1/users/me` — duplicates account
- ❌ `GET /v1/users/{id:guid}` — login sufficient

---

## Swagger Structure

### Groups

Swagger разбит на 8 логических групп: Account, Blog, Community, Forum, Game, Messaging, Common, Moderation.

Конкретные теги и эндпоинты смотреть в Swagger: `http://localhost:5000/swagger`

### URL Structure — Entities vs Relationships

**Entity** (Character, Game, Post, Room) — имеет собственную идентичность:
```
GET    /parents/{id}/entities       ← list
POST   /parents/{id}/entities       ← create
GET    /entities/{id}               ← get by own ID
PATCH  /entities/{id}               ← update
DELETE /entities/{id}               ← delete
```

**Relationship** (blacklist, participant, reader) — связь между сущностями:
```
GET    /parents/{id}/links          ← list
PUT    /parents/{id}/links/{key}    ← add (idempotent)
DELETE /parents/{id}/links/{key}    ← remove
```
Где `{key}` — natural key (login, slug), не GUID.

### Single ID Rule (уточнённое)

**Никогда два opaque ID (GUID) в URL.**

Natural key + parent ID — допустимо для relationships.

```csharp
// ✅ Entity — flat route с own ID
[HttpDelete("characters/{id}")]
[HttpDelete("invitations/{id}")]

// ✅ Relationship — nested route с natural key
[HttpDelete("games/{id}/blacklist/{login}")]
[HttpDelete("events/{id}/participants/{login}")]

// ❌ Два GUID — никогда
[HttpDelete("games/{gameId}/characters/{characterId}")]
[HttpDelete("events/{eventId}/participants/{userId}")]
```

### Избыточная вложенность (Anti-pattern)

**Принцип минимальной идентификации:** если ID глобально уникален — родительские ID избыточны.

```csharp
// ❌ НЕПРАВИЛЬНО — characterId уже уникален, gameId избыточен
[HttpGet("games/{gameId}/characters/{characterId}/notepad")]

// ✅ ПРАВИЛЬНО — characterId достаточен
[HttpGet("characters/{id}/notepad")]
```

**Когда вложенность оправдана:**

| Оправдано | НЕ оправдано |
|-----------|--------------|
| `/games/{id}/characters` — список в контексте | `/games/{id}/characters/{characterId}` — characterId уникален |
| `/games/{id}/blacklist/{login}` — login НЕ уникален | `/games/{id}/notepad/{entryId}` — entryId уникален |

**Максимум 2 уровня:** `/collection/{id}/subcollection` — норма, больше — антипаттерн.

### Parameter Naming

| Тип | Правило | Пример |
|-----|---------|--------|
| GUID (entity ID) | Всегда `{id}` | `games/{id}`, `characters/{id}` |
| String (natural key) | Семантическое имя | `{login}`, `{slug}`, `{role}` |

### Tag Naming

- **Единственное число** для главного ресурса группы (Forum, не Forums)
- **Множественное число** для остальных (Users, Games, Topics)
- **Без префиксов контекста** — группа даёт контекст (Comments, не GameComments)

### Key Principles

- Users идентифицируются по login (уникальный, читаемый), не по id
- Reviews всех типов в Community group (не в Games!)
- Account группа разбита на теги: Login, Registration, Profile, Security

---

## Ссылки

- [README](../README.md) — Обзор документации
- [Архитектура](../architecture/OVERVIEW.md) — Как устроено
- [Глоссарий](../reference/GLOSSARY.md) — Термины и определения
- [API Reference](../api/REFERENCE.md) — Справочник API
- [Тестирование](../guides/TESTING.md) — Запуск тестов

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
