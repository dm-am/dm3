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

**Зачем:** Testability — Domain тестируется без БД/HTTP. Flexibility — можно заменить PostgreSQL без изменения бизнес-логики. Четкие границы ответственности.

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

| Слой | Может зависеть от | НЕ может зависеть от |
|------|-------------------|---------------------|
| Domain.Core | Только .NET BCL | Ничего из проекта |
| Domain.* | Domain.Core | Других Domain.*, Infrastructure.* |
| Infrastructure.* | Domain.Core, Domain.* | Web.API, Workers.* |
| Web.API, Workers.* | Все | — |
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

**Workers** обрабатывают события асинхронно: отправка email, уведомления, индексация поиска.

---

## Компоненты системы

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
         │              │              │             │             │
         ▼              ▼              ▼             ▼             ▼
┌──────────────┐ ┌────────────┐ ┌───────────┐ ┌───────────┐ ┌──────────────┐
│ PostgreSQL   │ │  MongoDB   │ │ RabbitMQ  │ │  MinIO    │ │  imgproxy    │
│ (основные    │ │ (счетчики, │ │ (очередь  │ │ (source-  │ │ (on-the-fly  │
│  данные)     │ │  сессии)   │ │  событий) │ │  файлы)   │ │  resize+fmt) │
└──────────────┘ └────────────┘ └─────┬─────┘ └───────────┘ └──────────────┘
                                      │
         ┌────────────────────────────┼───────────────────────────┐
         │                            │                           │
         ▼                            ▼                           ▼
┌──────────────────┐           ┌──────────────────┐    ┌──────────────────┐
│ SearchIndexer    │           │ NotificationDisp │    │ Mail Worker      │
└────────┬─────────┘           └──────────────────┘    └────────┬─────────┘
         │                                                      │
         ▼                                                      ▼
┌──────────────────┐                                   ┌──────────────────┐
│   OpenSearch     │                                   │     MailHog      │
└──────────────────┘                                   └──────────────────┘
```

### Слои backend

| Слой | Назначение |
|------|-----------|
| **Domain.Core** | Shared kernel: интерфейсы, DTOs, enums, exceptions |
| **Domain.*** | Бизнес-логика модулей (Account, Game, Blog, Forum...) |
| **Infrastructure.*** | Технические реализации (Persistence, Mail, Messaging) |
| **Web.API** | REST API + WebSocket + Middleware |
| **Workers.*** | Фоновая обработка событий |

---

## Поток запроса

```
HTTP Request → Middleware Pipeline → Controller → Service → Repository → DB
```

**Middleware pipeline:**
1. SecurityHeadersMiddleware (X-Frame-Options, CSP, HSTS)
2. CorrelationMiddleware (X-Dm-Correlation-Token)
3. ErrorHandlingMiddleware (exceptions → ProblemDetails)
4. CORS
5. CsrfProtectionMiddleware (Origin/Referer validation)
6. RateLimiter (100 req/min global, 5 req/min auth)
7. AuthenticationMiddleware (Cookie → Identity)
8. Authorization
9. Routing → Controllers + SignalR Hub

---

## Система событий

```
Business Service → producer.Send(EventType, entityId) → Exchange (dm.events)
                                                              ↓
                                     ┌──────────────┬──────────────┬──────────────┐
                                     │ dm.search    │ dm.notif     │ dm.mail      │
                                     └──────────────┴──────────────┴──────────────┘
```

Workers подписываются на события и обрабатывают асинхронно.

---

## Система уведомлений

```
Event → NotificationConsumer → NotificationGenerator → MongoDB → SignalR → Frontend
```

---

## Зеркала

Система поддерживает несколько зеркал (разные домены, общая БД).

---

## Ссылки

- [AUTHENTICATION.md](./AUTHENTICATION.md) — как работает вход
- [AUTHORIZATION.md](./AUTHORIZATION.md) — как работают права
- [PATTERNS.md](../conventions/PATTERNS.md) — правила структурирования кода
- [DATA_STORAGE.md](../conventions/DATA_STORAGE.md) — организация хранилищ
