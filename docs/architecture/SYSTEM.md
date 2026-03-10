# Система DM3

> **User Story:** "Из чего состоит система? Какая архитектура? Как связаны компоненты?"

---

## Архитектурные решения

### Modular Monolith

**Зачем:** Изоляция модулей — изменения в Game не ломают Blog. Простота — один деплой, одна БД. Возможность выделить модуль в микросервис при необходимости.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         MODULAR MONOLITH                                │
│                                                                         │
│  Account  Personal  Community  Moderation  Messaging  Game  Blog  Forum │
│     │        │          │          │           │        │    │      │   │
│     └────────┴──────────┴──────────┴───────────┴────────┴────┴──────┘   │
│                                 │                                       │
│                    ┌────────────▼────────────┐                          │
│                    │     Domain Events       │                          │
│                    │      (RabbitMQ)         │                          │
│                    └─────────────────────────┘                          │
└─────────────────────────────────────────────────────────────────────────┘
```

### Clean Architecture

**Зачем:** Testability — Domain тестируется без БД/HTTP. Flexibility — можно заменить PostgreSQL без изменения бизнес-логики. Чёткие границы ответственности.

```
┌─────────────────────────────────────────────────────────────────────────┐
│                        Entry Points                                     │
│  DM.Web.API          DM.Workers.*           DM.Web.Client               │
│  (ASP.NET Core)      (MassTransit)          (Vue 3)                     │
└───────────────────────────┬─────────────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────────────┐
│                        Infrastructure                                   │
│  Persistence, Mail, Messaging, Core                                     │
│  Реализации интерфейсов из Domain. Никаких публичных интерфейсов.       │
└───────────────────────────┬─────────────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────────────┐
│                        Domain (Business Logic)                          │
│  Account, Personal, Community, Moderation, Messaging, Game, Blog, Forum │
│  Unified Services + Repository Interfaces                               │
└───────────────────────────┬─────────────────────────────────────────────┘
                            │
┌───────────────────────────▼─────────────────────────────────────────────┐
│                        Domain.Core (Shared Kernel)                      │
│  Интерфейсы, DTO, Enums, Exceptions. Никакой бизнес-логики.             │
└─────────────────────────────────────────────────────────────────────────┘
```

**Ключевое правило:** Все публичные интерфейсы — в Domain. Infrastructure НЕ определяет публичных интерфейсов.

### Dependency Rule

| Проект | Может зависеть от | НЕ может зависеть от |
|--------|-------------------|---------------------|
| Domain.Core | Только .NET BCL | Ничего из проекта |
| Domain.* | Domain.Core | Других Domain.*, Infrastructure.* |
| Infrastructure.* | Domain.Core, Domain.* | Web.API, Workers.* |
| Web.API, Workers.* | Всё | — |
| Web.Client | Только HTTP API | Backend напрямую |

**Важно:** Domain.Game НЕ может зависеть от Domain.Blog. Модули общаются только через Domain Events.

### Domain Events

**Зачем:** Decoupling — модули не знают друг о друге. Async — отправка email не блокирует HTTP-ответ. Reliability — RabbitMQ гарантирует доставку.

```csharp
// Правильно: публикуем событие
await _eventProducer.Send(EventType.GameCreated, game.Id);

// Неправильно: прямой вызов другого модуля
await _notificationService.CreateAsync(...); // ЗАПРЕЩЕНО
```

**Обработчики:** `Workers.Mail`, `Workers.NotificationDispatcher`, `Workers.SearchIndexer`

---

## Компоненты системы

### Общая картина

```
┌─────────────────────────────────────────────────────────────────┐
│                        ПОЛЬЗОВАТЕЛЬ                             │
└─────────────────────────────┬───────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     FRONTEND (Vue 3)                            │
│                     http://localhost:5173                       │
└─────────────────────────────┬───────────────────────────────────┘
                              │ HTTP запросы
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      BACKEND API                                │
│                   http://localhost:5000                         │
└────────┬──────────────┬──────────────┬─────────────┬────────────┘
         │              │              │             │
         ▼              ▼              ▼             ▼
┌──────────────┐ ┌────────────┐ ┌───────────┐ ┌───────────┐
│ PostgreSQL   │ │  MongoDB   │ │ RabbitMQ  │ │  MinIO    │
│ (основные    │ │ (счетчики, │ │ (очередь  │ │ (файлы)   │
│  данные)     │ │  сессии)   │ │  событий) │ │           │
└──────────────┘ └────────────┘ └─────┬─────┘ └───────────┘
                                     │
         ┌───────────────────────────┼───────────────────────────┐
         │                           │                           │
         ▼                           ▼                           ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ SearchIndexer  │    │ NotificationDisp  │    │ Mail Worker    │
└────────┬─────────┘    └──────────────────┘    └────────┬─────────┘
         │                                               │
         ▼                                               ▼
┌──────────────────┐                          ┌──────────────────┐
│   OpenSearch     │                          │     MailHog      │
└──────────────────┘                          └──────────────────┘
```

### Backend проекты (17)

#### Infrastructure (4)

| Проект | Назначение |
|--------|-----------|
| **DM.Infrastructure.Core** | Authorization, logging, parsing, S3 storage, search |
| **DM.Infrastructure.Mail** | Email templates и отправка |
| **DM.Infrastructure.Messaging** | RabbitMQ события |
| **DM.Infrastructure.Persistence** | EF Core + MongoDB, репозитории |

#### Domain (9)

| Проект | Назначение |
|--------|-----------|
| **DM.Domain.Core** | Shared kernel: интерфейсы, DTOs, enums |
| **DM.Domain.Account** | Регистрация, логин, сессии, токены |
| **DM.Domain.Blog** | Блоги, публикации, комментарии |
| **DM.Domain.Community** | Пользователи, опросы, отзывы |
| **DM.Domain.Forum** | Форумы, топики, комментарии |
| **DM.Domain.Game** | Игры, комнаты, персонажи, посты |
| **DM.Domain.Messaging** | Личные сообщения, глобальный чат |
| **DM.Domain.Moderation** | Модерация, баны, предупреждения |
| **DM.Domain.Personal** | Профили, уведомления, подписки |

#### Workers (3)

| Проект | Назначение |
|--------|-----------|
| **DM.Workers.Mail** | Email отправка и шаблоны |
| **DM.Workers.NotificationDispatcher** | События → уведомления |
| **DM.Workers.SearchIndexer** | События → индексация OpenSearch |

#### Web (1)

| Проект | Назначение |
|--------|-----------|
| **DM.Web.API** | REST API + WebSocket хаб + Middleware + SignalR |

---

## Поток запроса

```
HTTP Request → Middleware Pipeline → Controller → Service → Repository → DB
```

**Middleware:**
1. SecurityHeadersMiddleware (X-Frame-Options, CSP, HSTS, etc.)
2. CorrelationMiddleware (X-Dm-Correlation-Token)
3. ErrorHandlingMiddleware (exceptions → ProblemDetails)
4. CORS
5. CsrfProtectionMiddleware (Origin/Referer validation)
6. BotApiKeyMiddleware (bot authentication)
7. RateLimiter (100 req/min global, 5 req/min auth)
8. AuthenticationMiddleware (Cookie → Identity)
9. Authorization
10. Routing → Controllers + SignalR Hub (/whatsup)

**Пример:** `src/DM.Web.API/Features/Forum/Topics/TopicController.cs`

---

## Dependency Injection (Autofac)

```
CoreModule (DM.Infrastructure.Core)
├── DataAccessModule
├── AuthorizationModule
├── ServicesModule
├── MessageQueuingModule
├── AccountModule
├── CommunityModule
├── BlogModule
├── MessagingModule
├── ModerationModule
├── ForumModule
├── GameModule
├── GeneralModule
├── PersonalModule
└── WebCoreModule
```

**Паттерны:** `RegisterDefaultTypes()`, `RegisterModuleOnce<>()`, `RegisterMapper()`

---

## Хранилища данных

### PostgreSQL

См. [DATABASE.md](./DATABASE.md) — 54 таблицы по доменам

### MongoDB

| Коллекция | Назначение |
|-----------|-----------|
| **Polls** | Опросы |
| **UnreadCounters** | Счетчики непрочитанного |
| **UserSettings** | Настройки пользователей |
| **UserSessions** | Сессии |
| **AttributeSchemata** | Схемы атрибутов персонажей |
| **Dice** | Броски кубиков |
| **RealtimeNotifications** | Push-уведомления |
| **LoginAttempts** | Счетчики неудачных входов |
| **SecurityAuditLog** | Журнал событий безопасности |

---

## Система событий (RabbitMQ)

```
Business Service → producer.Send(EventType, entityId) → Exchange (dm.events)
                                                              ↓
                                     ┌──────────────┬──────────────┬──────────────┐
                                     │ dm.search    │ dm.notif     │ dm.mail      │
                                     └──────────────┴──────────────┴──────────────┘
```

**EventTypes:** `src/DM.Infrastructure.Core/Extensions/EventRoutingKeyAttribute.cs`

---

## Система приглашений

| TokenType | Назначение |
|-----------|-----------|
| AssistantAssignment (2) | Приглашение ассистентом |
| PlayerInvitation (3) | Приглашение игроком |
| ReaderInvitation (4) | Приглашение читателем |

**API:**
- Game masters: `GET/POST/DELETE /v1/games/{id}/invitations/*`
- Users: `GET/POST /v1/account/invitations/*`

---

## Система уведомлений

```
Event → NotificationConsumer → NotificationGenerator → MongoDB → SignalR → Frontend
```

**Generators:** `src/DM.Workers.NotificationDispatcher/Implementation/Generators/`

---

## Frontend (Vue 3 + TypeScript + Pinia)

**Stores:** `src/DM.Web.Client/src/stores/`

**API типизация:**
```typescript
type User = {
  username: Served<Username>  // read-only
  status: string              // editable
}
```

---

## Зеркала

Система поддерживает несколько зеркал (разные домены, общая БД).

**Конфигурация:** `MirrorConfiguration` в `appsettings.json`

**Frontend:** `useRegion()` composable

---

## Ссылки

- [DATABASE.md](./DATABASE.md) — схема БД
- [AUTHENTICATION.md](./AUTHENTICATION.md) — как работает вход
- [AUTHORIZATION.md](./AUTHORIZATION.md) — как работают права
- [PATTERNS.md](../conventions/PATTERNS.md) — правила структурирования кода
