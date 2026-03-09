# DM3 Code Standards

> **Принцип:** Код > документация. Паттерны смотреть в коде, здесь только правила.
>
> **Архитектура:** См. [ARCHITECTURE.md](../architecture/ARCHITECTURE.md) — паттерны, структура проектов, блюпринт.

---

## Naming Conventions

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

---

## DI Lifetimes

| Lifetime | Use Case |
|----------|----------|
| `SingleInstance()` | Stateless: factories, providers |
| `InstancePerLifetimeScope()` | Request-scoped: services, repositories |

---

## API Rules

> Подробнее: [API_STANDARDS.md](API_STANDARDS.md)

**Response format:**
```csharp
// Одиночный ресурс — напрямую
return Ok(user);

// Коллекция — ListEnvelope
return Ok(new ListEnvelope<User>(users, paging));
```

**Example:** `DM.Web.API/Features/Forum/Topics/TopicController.cs`

---

## Validation

FluentValidation для бизнес-правил, DataAnnotations для простых ограничений.

**Example:** `DM.Domain.Forum/Features/Topics/CreateTopicValidator.cs`

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

**Example:** `test/DM.Domain.Forum.Tests/TopicCreatingServiceShould.cs`

---

## Frontend (Vue.js)

**Branded types:**
```typescript
export type Message = {
  id: Served<MessageId>;      // Server-provided (read-only)
  text: string;                // User-editable
};
```

**Example:** `src/DM.Web.Client/src/api/models/messaging/index.ts`

---

## Security

| Aspect | Reference |
|--------|-----------|
| Password hash | Argon2id (19 MiB, 2 iter) — [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| Token encryption | AES-256-GCM — [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| Rate limiting | [REFERENCE.md](../api/REFERENCE.md#rate-limiting) |
| BFF Pattern | HttpOnly cookies, SameSite=Strict — [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |

---

## Checklists

### New Feature
- [ ] Feature folder (flat, no CRUD subfolders) — см. [ARCHITECTURE.md](../architecture/ARCHITECTURE.md#целевые-структуры-блюпринт)
- [ ] Unified Service (`I{Feature}Service` + `{Feature}Service`) — все CRUD в одном сервисе
- [ ] Repository (`I{Feature}Repository` + `{Feature}Repository`)
- [ ] Intention enum + resolver (in `Authorization/`)
- [ ] FluentValidation validators
- [ ] AutoMapper profile (`{Feature}MappingProfile`)
- [ ] DI registration in `Web.API/Startup.cs` (RegisterDomainServices)

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
6. ✅ Duplicate endpoints (`{id:guid}` AND `{username}` for same resource)

### API Design

**One canonical access pattern** — users by `{username}`, not GUID.

---

## URL Structure

> Подробнее: [API_STANDARDS.md](API_STANDARDS.md#url-structure)

**Key rules:**
- Users по `{username}`, не по GUID
- Никогда два GUID в URL
- Max 2 уровня вложенности

```csharp
// ✅ Entity — flat route
[HttpDelete("characters/{id}")]

// ✅ Relationship — nested с natural key
[HttpDelete("games/{id}/blacklist/{username}")]

// ❌ Два GUID
[HttpDelete("games/{gameId}/characters/{characterId}")]
```

---

## Ссылки

- [Архитектура и паттерны](../architecture/ARCHITECTURE.md) — Паттерны, структура проектов, блюпринт
- [Системный обзор](../architecture/OVERVIEW.md) — Компоненты, порты, потоки
- [Глоссарий](../reference/GLOSSARY.md) — Термины и определения
- [API Reference](../api/REFERENCE.md) — Справочник API
- [Тестирование](../guides/TESTING.md) — Запуск тестов

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
