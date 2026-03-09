# Архитектура DM3 — Системный обзор

> **Паттерны и структура:** См. [ARCHITECTURE.md](./ARCHITECTURE.md) — паттерны, Feature Folders, Unified Services, блюпринт.
>
> Этот документ описывает компоненты системы, порты и потоки данных.

## Общая картина

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

---

## Backend проекты (17)

### Infrastructure (4)

| Проект | Назначение |
|--------|-----------|
| **DM.Infrastructure.Core** | Authorization, logging, parsing, S3 storage, search |
| **DM.Infrastructure.Mail** | Email templates и отправка |
| **DM.Infrastructure.Messaging** | RabbitMQ события |
| **DM.Infrastructure.Persistence** | EF Core + MongoDB, репозитории |

### Domain (9)

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

### Workers (3)

| Проект | Назначение |
|--------|-----------|
| **DM.Workers.Mail** | Email отправка и шаблоны |
| **DM.Workers.NotificationDispatcher** | События → уведомления |
| **DM.Workers.SearchIndexer** | События → индексация OpenSearch |

### Web (1)

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

## Аутентификация

См. [AUTHENTICATION.md](./AUTHENTICATION.md)

---

## Авторизация (Intention Pattern)

**Пример:** `src/DM.Domain.Forum/Authorization/TopicIntentionResolver.cs`

```csharp
intentionManager.ThrowIfForbidden(TopicIntention.Delete, topic);
// Нет прав → 403 Forbidden
```

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

**API:** См. [API Reference](../api/REFERENCE.md)

---

## Observability

### Логирование (Serilog)

```
Sinks: OpenSearch (dm_logstash-{date}), Console
Enrichers: Application, Environment, LogContext, ActivityEnricher (TraceId, SpanId)
```

### Tracing (OpenTelemetry → Jaeger)

Инструментация: ASP.NET Core, gRPC, HTTP client, EF Core, MongoDB, RabbitMQ

**Протокол:** OTLP gRPC (порт 4317)

### Metrics (Prometheus)

**Endpoint:** `/metrics` на всех .NET сервисах

**Scrape targets:** dm-api, 3 consumers, postgres-exporter, node-exporter

**Dashboards:** 3 Grafana dashboard'а (API Overview, Infrastructure, Consumers) — auto-provisioned

### Alerting

7 Prometheus правил: `docker/prometheus/alerts.yml`

---

## Ссылки

- [Архитектура и паттерны](./ARCHITECTURE.md) — Паттерны, структура проектов, блюпринт
- [База данных](./DATABASE.md) — Схема БД
- [Аутентификация](./AUTHENTICATION.md) — Сессии, токены, безопасность
- [RBAC](./RBAC.md) — Роли и права
- [Глоссарий](../reference/GLOSSARY.md) — Термины
- [API Reference](../api/REFERENCE.md) — REST API
- [Установка](../guides/SETUP.md) — Локальная разработка

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
