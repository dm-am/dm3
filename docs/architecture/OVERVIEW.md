# Архитектура DM3

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
│ (основные    │ │ (счётчики, │ │ (очередь  │ │ (файлы)   │
│  данные)     │ │  сессии)   │ │  событий) │ │           │
└──────────────┘ └────────────┘ └─────┬─────┘ └───────────┘
                                     │
         ┌───────────────────────────┼───────────────────────────┐
         │                           │                           │
         ▼                           ▼                           ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ Search Consumer  │    │ Notif. Consumer  │    │ Mail Consumer    │
└────────┬─────────┘    └──────────────────┘    └────────┬─────────┘
         │                                               │
         ▼                                               ▼
┌──────────────────┐                          ┌──────────────────┐
│   OpenSearch     │                          │     MailHog      │
└──────────────────┘                          └──────────────────┘
```

---

## Backend проекты

### Главные проекты

| Проект | Назначение |
|--------|-----------|
| **DM.Web.API** | REST API + WebSocket хаб |
| **DM.Web.Core** | Middleware, аутентификация, SignalR |
| **DM.Services.DataAccess** | EF Core + MongoDB |

### Бизнес-логика (по доменам)

| Проект | Назначение |
|--------|-----------|
| **DM.Services.Authentication** | Логин, сессии, токены |
| **DM.Services.Community** | Пользователи, опросы, отзывы, сообщения |
| **DM.Services.Forum** | Форумы, топики, комментарии |
| **DM.Services.Game** | Игры, комнаты, персонажи, посты |
| **DM.Services.Common** | Авторизация, счётчики непрочитанного |

### Инфраструктура

| Проект | Назначение |
|--------|-----------|
| **DM.Services.Core** | Логирование, correlation tokens |
| **DM.Services.MessageQueuing** | RabbitMQ события |
| **DM.Services.Uploading** | Загрузка файлов в MinIO |
| **DM.Services.Notifications** | Push-уведомления |
| **DM.Services.Mail.*** | Email отправка и шаблоны |
| **DM.Services.Search** | gRPC клиент для поиска |

### Background consumers

| Проект | Назначение |
|--------|-----------|
| **Notifications.Consumer** | События → уведомления |
| **Search.Consumer** | События → индексация OpenSearch |
| **Mail.Sender.Consumer** | Очередь → email |

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

**Пример:** `src/DM.Web.API/Controllers/v1/Forum/TopicController.cs`

---

## Dependency Injection (Autofac)

```
CoreModule
├── DataAccessModule
├── AuthenticationModule
├── CommonModule
├── CommunityModule
├── ForumModule
├── GameModule
├── NotificationsModule
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
| **UnreadCounters** | Счётчики непрочитанного |
| **UserSettings** | Настройки пользователей |
| **UserSessions** | Сессии |
| **AttributeSchemata** | Схемы атрибутов персонажей |
| **Dice** | Броски кубиков |
| **RealtimeNotifications** | Push-уведомления |
| **LoginAttempts** | Счётчики неудачных входов |

---

## Аутентификация

См. [AUTHENTICATION.md](./AUTHENTICATION.md)

---

## Авторизация (Intention Pattern)

**Пример:** `src/DM.Services.Forum/Authorization/TopicIntentionResolver.cs`

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

**EventTypes:** `src/DM.Services.Core/Extensions/EventRoutingKeyAttribute.cs`

---

## Система приглашений

| TokenType | Назначение |
|-----------|-----------|
| AssistantAssignment (2) | Приглашение ассистентом |
| PlayerInvitation (3) | Приглашение игроком |
| ReaderInvitation (4) | Приглашение читателем |

**API:** `GET/POST /v1/games/{id}/invitations/*`, `POST /v1/users/me/invitations/{id}/accept`

---

## Система уведомлений

```
Event → NotificationConsumer → NotificationGenerator → MongoDB → SignalR → Frontend
```

**Generators:** `src/DM.Services.Notifications.Consumer/Implementation/Generators/`

---

## Frontend (Vue 3 + TypeScript + Pinia)

**Stores:** `frontend/DM.Web.Modern/src/stores/`

**API типизация:**
```typescript
type User = {
  login: Served<UserLogin>  // read-only
  status: string            // editable
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

- [README](../README.md)
- [Глоссарий](../reference/GLOSSARY.md)
- [База данных](./DATABASE.md)
- [Аутентификация](./AUTHENTICATION.md)
- [API Reference](../api/REFERENCE.md)
- [Установка](../guides/SETUP.md)

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
