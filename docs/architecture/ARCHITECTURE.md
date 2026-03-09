# DM3 — Архитектура и паттерны

> **SSOT:** Это единственный источник истины по архитектуре и паттернам DM3. Другие документы ссылаются сюда.

---

## Архитектура

DM3 построен на **Modular Monolith** с **Clean Architecture** внутри каждого модуля. Сервисы проектируются как **Unified Services**, модули общаются через **Domain Events**. Тестирование следует **Testing Pyramid** с акцентом на unit-тесты Domain-слоя.

### Структура решения

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

### Слои внутри каждого модуля (Clean Architecture)

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

### Dependency Rule

| Проект | Может зависеть от | НЕ может зависеть от |
|--------|-------------------|---------------------|
| Domain.Core | Только .NET BCL | Ничего из проекта |
| Domain.* | Domain.Core | Других Domain.*, Infrastructure.* |
| Infrastructure.* | Domain.Core, Domain.* | Web.API, Workers.* |
| Web.API, Workers.* | Всё | — |
| Web.Client | Только HTTP API | Backend напрямую |

**Важно:** Domain.Game НЕ может зависеть от Domain.Blog. Модули общаются только через Domain Events.

---

## Принципы и паттерны

### Архитектурные принципы (строго)

Эти принципы определяют структуру решения и соблюдаются **без исключений**.

#### Modular Monolith

**Зачем:** Изоляция модулей — изменения в Game не ломают Blog. Простота — один деплой, одна БД. Возможность выделить модуль в микросервис при необходимости.

Монолит, разделённый на изолированные модули. Каждый модуль — bounded context со своей бизнес-логикой. Модули показаны на диаграмме в разделе [Структура решения](#структура-решения).

#### Clean Architecture

**Зачем:** Testability — Domain тестируется без БД/HTTP. Flexibility — можно заменить PostgreSQL без изменения бизнес-логики. Чёткие границы ответственности.

Правило направления зависимостей — внутренние слои не знают о внешних. Слои показаны на диаграмме, зависимости — в таблице [Dependency Rule](#dependency-rule).

**Ключевое правило:** Все публичные интерфейсы — в Domain. Infrastructure НЕ определяет публичных интерфейсов.

#### Domain Events

**Зачем:** Decoupling — модули не знают друг о друге. Async — отправка email не блокирует HTTP-ответ. Reliability — RabbitMQ гарантирует доставку.

Асинхронные события для межмодульной коммуникации. Модули НЕ вызывают сервисы друг друга напрямую.

```csharp
// ✅ Правильно: публикуем событие
await _eventProducer.Send(EventType.GameCreated, game.Id);

// ❌ Неправильно: прямой вызов другого модуля
await _notificationService.CreateAsync(...); // ЗАПРЕЩЕНО
```

**Обработчики:** `Workers.Mail`, `Workers.NotificationDispatcher`, `Workers.SearchIndexer`

---

### Организация кода по слоям

Паттерны применяются в соответствующих слоях. Это **прагматичные решения**, не догмы.

#### Domain.Core — Shared Kernel

**Зачем:** Общие контракты без дублирования. Модули общаются через абстракции, не зная друг о друге.

Паттерн из DDD. Domain.Core содержит общие контракты для всех модулей:
- Интерфейсы системных абстракций (`IDateTimeProvider`, `IGuidFactory`)
- Интерфейсы cross-module фич (`ICommentService`, `INotepadRepository`)
- Общие DTO, enums, exceptions

**Правило:** Domain.* НЕ импортирует другие Domain.* — только через Domain.Core.

#### Domain.* — Unified Services

**Зачем:** Один интерфейс вместо трёх-четырёх. Все операции с Game в одном месте. Меньше файлов и DI-регистраций.

Один сервис на фичу со всеми CRUD-операциями:

```csharp
public interface IGameService
{
    Task<Game> GetAsync(Guid id);
    Task<PagingResult<Game>> GetAllAsync(GamesQuery query);
    Task<Game> CreateAsync(CreateGame create);
    Task<Game> UpdateAsync(Guid id, UpdateGame update);
    Task DeleteAsync(Guid id);
}
```

**Где применяется:** Domain.* (`IGameService`), Web.API (`IGameApiService`).

#### Infrastructure.* — Technical Concerns

**Зачем:** Чёткое разделение технических ответственностей. Легко найти где реализован кэш, где парсинг, где репозитории.

Организация по **техническому назначению**, НЕ по бизнес-фичам:

```
Infrastructure.Core/
├── Authorization/    # IntentionManager
├── Caching/          # Реализация ICache
├── Correlation/      # Реализация ICorrelationTokenProvider
├── Logging/          # Конфигурация логирования
└── Parsing/          # BBCode parser

Infrastructure.Persistence/
├── Entities/         # EF Core entities (группировка по модулям)
├── Repositories/     # Реализации IXxxRepository (группировка по модулям)
├── Shared/           # Cross-module реализации
└── Migrations/

Infrastructure.Mail/
├── Rendering/        # Email templates
└── Assets/

Infrastructure.Messaging/
├── GeneralBus/       # MassTransit
└── Outbox/           # Transactional outbox
```

#### Repository Pattern

**Зачем:** Абстракция над БД. Domain не знает про EF Core. Тестируемость без реальной БД.

Интерфейс в Domain, реализация в Infrastructure:

```csharp
// Domain.Game/Features/Games/IGameRepository.cs
public interface IGameRepository { ... }

// Infrastructure.Persistence/Repositories/Game/GameRepository.cs
internal class GameRepository : IGameRepository { ... }
```

#### Web.API — Feature Folders

**Зачем:** Всё связанное с фичей в одном месте. Легко найти controller, service, DTOs для конкретного endpoint.

Controller + ApiService + DTOs группируются в папку фичи:

```
Features/{Module}/{Feature}/
├── {Feature}Controller.cs
├── I{Feature}ApiService.cs
├── {Feature}ApiService.cs
├── {Feature}Request.cs
└── {Feature}Response.cs
```

**Примечание:** Feature Folders — это организация Web.API, не путать с Vertical Slices. Бизнес-логика остаётся в Domain.

#### Workers — Event Handlers

**Зачем:** Асинхронная обработка событий. Отправка email, уведомления, индексация — не блокируют HTTP-ответ.

Минимальная структура — workers это тонкий слой оркестрации:

```
Workers.{Name}/
├── {Name}Consumer.cs     # MassTransit consumer
├── Program.cs
└── Startup.cs
```

**Workers DM3:** `Mail`, `NotificationDispatcher`, `SearchIndexer`.

#### Frontend — Feature-Sliced Design (FSD)

**Зачем:** Чёткие правила импортов предотвращают спагетти-код. Переиспользуемость компонентов. Масштабируемость frontend.

Архитектурная методология для frontend. Слои:

```
app → pages → widgets → features → entities → shared
```

| Слой | Назначение |
|------|-----------|
| `app/` | Инициализация: Pinia, Router, глобальные стили |
| `pages/` | Страницы роутера |
| `widgets/` | Сложные UI блоки (композиция features + entities) |
| `features/` | Действия пользователя (формы, кнопки) |
| `entities/` | Бизнес-сущности (store + api + ui) |
| `shared/` | UI kit, HTTP client, утилиты |

**Правила импортов:** Верхние слои → нижние. Слои одного уровня НЕ импортируют друг друга.

#### Tests — Testing Pyramid

**Зачем:** Быстрая обратная связь. Unit tests дешёвые и быстрые, ловят большинство багов. E2E дорогие — только критичные сценарии.

Стратегия распределения тестов: Unit tests (Domain) > Integration tests (API) > E2E tests.

#### Tests — Mirrored Structure

**Зачем:** Легко найти тесты для любого класса. Нет вопросов "где тест для X?".

Организация файлов — тесты зеркалят production код:
```
Domain.Forum/Features/Topics/TopicService.cs
  → Domain.Forum.Tests/Features/Topics/TopicServiceShould.cs
```

---

## Naming Conventions (Стандарт именования)

> **Принципы:** Унифицированность, самодокументируемость, future-proof.

---

### 1. Паттерн именования классов

```
{Context}{Entity}{Suffix?}
```

| Часть | Описание | Примеры |
|-------|----------|---------|
| Context | Область/модуль/владелец | `User`, `Personal`, `Moderated`, `Blog`, `Game`, `Topic` |
| Entity | Сущность | `Profile`, `ProfileNote`, `Comment`, `Blacklist` |
| Suffix | Тип (опционально) | `Dto`, `Request`, `Service`, `Repository` |

**Классы всегда в единственном числе:** `UserProfile`, `GameComment`, `ModeratedProfileNote`.

---

### 2. Контексты (префиксы)

| Контекст | Значение | Модуль | Пример |
|----------|----------|--------|--------|
| — | Базовая сущность без контекста | Core | `User`, `Comment` |
| `Personal` | Данные текущего пользователя (self) | Personal | `PersonalProfile` |
| `User` | Публичная информация о любом пользователе | Community | `UserProfile` |
| `Moderated` | Данные для модераторов | Moderation | `ModeratedProfile` |
| `Game` | Контент игры | Game | `GameComment`, `GameBlacklist` |
| `Blog` | Контент блога | Blog | `BlogComment`, `BlogBlacklist` |
| `Publication` | Публикация в блоге | Blog | `PublicationComment` |
| `Topic` | Контент топика форума | Forum | `TopicComment` |

---

### 3. Иерархия сущностей (примеры)

```
User (Core/Community)
├── PersonalProfile (Personal) — профиль текущего пользователя (self)
├── UserProfile (Community) — публичный профиль для просмотра
└── ModeratedProfile (Moderation) — профиль с данными для модераторов

ProfileNote
├── UserProfileNote (Personal) — заметка пользователя о другом
└── ModeratedProfileNote (Moderation) — заметка модератора

Comment (Core)
├── GameComment (Game)
├── BlogComment (Blog)
├── PublicationComment (Blog)
└── TopicComment (Forum)

Blacklist
├── UserBlacklist (Personal) — личный ЧС
├── GameBlacklist (Game)
└── BlogBlacklist (Blog)
```

---

### 4. Распределение по модулям

**Принцип: "Кто владеет данными?"**

| Вопрос | Модуль |
|--------|--------|
| Данные для входа/сессии? | Account |
| Данные текущего пользователя (self)? | Personal |
| Публичная информация о пользователях? | Community |
| Данные для модераторов? | Moderation |
| Данные конкретного контента? | Messaging / Game / Blog / Forum |

**Карта сущностей:**

| Модуль | Сущности | Примечание |
|--------|----------|------------|
| **Account** | Session, Token, Credentials, LoginAttempt | Аутентификация |
| **Personal** | PersonalProfile, UserProfileNote, UserSettings, Notification, Subscription, UserBlacklist | Данные текущего пользователя |
| **Community** | User, UserProfile, Poll, Review | Публичные данные |
| **Moderation** | ModeratedProfile, ModeratedProfileNote, Warning, Ban, Ticket, Mentorship | Данные для модераторов |
| **Messaging** | Chat, Message, GlobalChatEvent | — |
| **Game** | Game, Room, Post, Character, GameComment, GameBlacklist, GameNotepad, GameInvitation | — |
| **Blog** | Blog, Publication, BlogComment, PublicationComment, BlogBlacklist, BlogInvitation | — |
| **Forum** | Board, Topic, TopicComment | — |

---

### 5. Правила для папок Features

**Ресурсы (CRUD над сущностью) — множественное число:**
```
Features/Profiles/        # PersonalProfile, UserProfile, ModeratedProfile
Features/ProfileNotes/    # UserProfileNote, ModeratedProfileNote
Features/Comments/        # TopicComment, GameComment, etc.
Features/Blacklists/      # UserBlacklist, BlogBlacklist, etc.
Features/Notepads/        # UserNotepad, BlogNotepad, GameNotepad
```

**Действия/процессы — единственное число:**
```
Features/Authentication/  # процесс входа
Features/Registration/    # процесс регистрации
Features/Recovery/        # восстановление пароля
Features/Search/          # поиск
```

**Запрещённые названия:**
- ❌ `MyProfile` — использовать `Profiles/` в модуле Personal
- ❌ `Self` — использовать `Profiles/` в модуле Personal
- ❌ `Blacklist` (ед.ч.) — использовать `Blacklists/`
- ❌ `Notepad` (ед.ч.) — использовать `Notepads/`

---

### 6. Frontend (FSD) — стиль именования

| Элемент | Стиль | Примеры |
|---------|-------|---------|
| Папки слоёв (`entities/`, `features/`, `pages/`) | **kebab-case** | `global-chat/`, `create-game/`, `profile-note/` |
| Папки UI компонентов (`shared/ui/`) | **PascalCase** | `Button/`, `Modal/` |
| Файлы Vue компонентов | **PascalCase** | `UserProfile.vue`, `GameCard.vue` |
| Composables/utils | **camelCase** | `useAuth.ts`, `formatDate.ts` |

---

## Целевые структуры (Блюпринт)

> `{Placeholder}` — переменная часть. Паттерны и правила — в разделе [Принципы и паттерны](#принципы-и-паттерны).

---

### DM.Domain.Core

```
DM.Domain.Core/
├── Abstractions/         # IDateTimeProvider, IGuidFactory, etc.
├── Authorization/        # IIntentionManager, CommentIntention
├── Blacklists/           # IUserBlacklistChecker, IContentBlacklistService
├── Caching/              # ICache, CachePolicy
├── Comments/             # ICommentService, Comment (cross-module)
├── Configuration/        # Shared configs
├── Dto/                  # PagingResult, CursorResult, GeneralUser
├── Enums/                # UserRole, GameRole, EventType, etc.
├── Events/               # IEventProducer, DomainEvent
├── Exceptions/           # HttpException, ValidationError
├── Extensions/           # QueryableExtensions, etc.
├── Identity/             # IIdentity, Session, UserSettings
├── Likes/                # ILikable, ILikeOperations (cross-module)
├── Mail/                 # IMailSender, ITemplateRenderer
├── Notepads/             # INotepadRepository (cross-module)
├── Parsing/              # UserAgentParser
├── Reviews/              # Review (shared DTO)
├── Search/               # ISearchService (cross-module)
├── Subscriptions/        # ISubscriptionRepository (cross-module)
├── Tokens/               # Token, CreateToken (shared DTO)
├── UnreadCounters/       # IUnreadCountersRepository (cross-module)
├── Uploads/              # IPublicImageService (cross-module)
└── Users/                # IUserLookupService (cross-module)
```

---

### DM.Domain.{Module}

```
DM.Domain.{Module}/
├── Features/
│   └── {Feature}/
│       ├── I{Feature}Service.cs
│       ├── {Feature}Service.cs
│       ├── I{Feature}Repository.cs
│       ├── {Feature}.cs                    # DTO
│       ├── Create{Feature}.cs              # Command DTO
│       ├── Update{Feature}.cs              # Command DTO
│       ├── Create{Feature}Validator.cs     # FluentValidation
│       └── Update{Feature}Validator.cs     # FluentValidation
├── Authorization/
└── Configuration/
```

---

### DM.Infrastructure.Core

```
DM.Infrastructure.Core/
├── Authorization/
├── Caching/
├── Configuration/
├── Correlation/
├── Extensions/
├── Logging/
├── Parsing/
├── Search/
├── Storage/
├── Tracing/
└── CoreModule.cs
```

---

### DM.Infrastructure.Persistence

```
DM.Infrastructure.Persistence/
├── Entities/
│   └── {Module}/
├── Repositories/
│   └── {Module}/
│       └── {Feature}Repository.cs
├── Shared/
│   ├── Comments/
│   ├── Likes/
│   ├── Notepads/
│   ├── Subscriptions/
│   ├── UnreadCounters/
│   └── Users/
├── Migrations/
├── Design/
├── DmDbContext.cs
└── PersistenceModule.cs
```

---

### DM.Infrastructure.Mail

```
DM.Infrastructure.Mail/
├── Rendering/
├── Assets/
└── MailModule.cs
```

---

### DM.Infrastructure.Messaging

```
DM.Infrastructure.Messaging/
├── GeneralBus/
├── Outbox/
└── MessagingModule.cs
```

---

### DM.Web.API

```
DM.Web.API/
├── Features/
│   └── {Module}/
│       └── {Feature}/
│           ├── {Feature}Controller.cs
│           ├── I{Feature}ApiService.cs
│           ├── {Feature}ApiService.cs
│           ├── {Feature}Request.cs
│           └── {Feature}Response.cs
├── Shared/
│   ├── Authentication/
│   ├── BbRendering/
│   ├── Comments/
│   ├── Configuration/
│   └── Dto/
├── HostedServices/
├── Middleware/
├── Notifications/
├── Realtime/
├── Swagger/
├── Validation/
├── Warmup/
├── Program.cs
└── Startup.cs
```

---

### DM.Web.Client

```
DM.Web.Client/src/
├── app/
│   ├── providers/
│   ├── styles/
│   ├── App.vue
│   └── main.ts
├── pages/
│   └── {domain}/
├── widgets/
│   └── {Widget}/
├── features/
│   └── {feature}/
├── entities/
│   └── {entity}/
│       ├── model/
│       ├── api/
│       └── ui/
├── shared/
│   ├── ui/
│   ├── api/
│   ├── lib/
│   ├── stores/        # Auth, UI state (used by all layers)
│   └── config/
└── assets/
```

---

### DM.Workers.{Name}

```
DM.Workers.{Name}/
├── {Name}Consumer.cs
├── Program.cs
└── Startup.cs
```

---

### test/

```
test/
├── DM.Domain.{Module}.Tests/
│   ├── Authorization/
│   ├── Features/
│   │   └── {Feature}/
│   └── Dsl/
├── DM.Infrastructure.{Name}.Tests/
├── DM.Web.API.IntegrationTests/
│   └── Controllers/
│       └── {Module}/
└── DM.Testing/
```

---

## Частые ошибки и недопонимания

### ❌ "Модули могут вызывать сервисы друг друга"

Нет. `Domain.Game` не может вызывать `INotificationService` из `Domain.Personal`. Коммуникация — только через Domain Events.

### ❌ "Infrastructure.* может определять интерфейсы"

Нет. Все публичные интерфейсы — в Domain.Core или Domain.*. Infrastructure только реализует.

### ❌ "Web.Client должен следовать Clean Architecture"

Нет. Clean Architecture — для backend. Frontend использует **Feature-Sliced Design (FSD)** — специализированную методологию для frontend с чёткими правилами импортов между слоями.

### ❌ "Тесты можно организовать плоско или по-своему"

Нет. Тесты зеркалят структуру production кода. `Domain.Forum/Features/Topics/TopicService.cs` → `Domain.Forum.Tests/Features/Topics/TopicServiceShould.cs`. Это упрощает навигацию и поддержку.

### ❌ "Domain.Core может содержать бизнес-логику"

Нет. Domain.Core — это **Shared Kernel**: интерфейсы, DTO, enums, exceptions. Никаких реализаций, никакой бизнес-логики. Бизнес-логика — в Domain.*.

### ❌ "Features везде означает одно и то же"

Нет. `Features/` в разных слоях — разные концепции:
- **Domain.*/Features/** — бизнес-фичи (сервисы, репозитории, DTOs)
- **Web.API/Features/** — Feature Folders (controller + API service + request/response)
- **Web.Client/features/** — FSD слой действий пользователя (формы, кнопки)

### ❌ "Feature Folders = Vertical Slices"

Нет. Feature Folders в Web.API — это только организация API-слоя. Бизнес-логика остаётся в Domain. Vertical Slices подразумевают всё в одной папке включая Domain — мы так не делаем.

### ❌ "Workers содержат бизнес-логику"

Нет. Workers — тонкий слой оркестрации. Вся бизнес-логика в Domain.*. Worker только получает событие и вызывает соответствующий сервис.

### ❌ "Сервисы надо разбивать на CreateGameService, UpdateGameService"

Нет. Мы используем **Unified Services** — один сервис на фичу со всеми CRUD-операциями. Split Services добавляют файлы и усложняют DI без пользы.

### ❌ "Infrastructure организуется по фичам"

Нет. Infrastructure организуется по **Technical Concerns** — техническому назначению (Caching/, Parsing/, Repositories/). Не по бизнес-фичам.

### ❌ "FSD entities могут импортировать друг друга"

Нет. В FSD слои одного уровня НЕ импортируют друг друга. `entities/game/` не импортирует `entities/user/`. Общий код — в `shared/`.

### ❌ "Domain.Game может импортировать Domain.Blog"

Нет. Domain.* НЕ импортирует другие Domain.* напрямую. Только через Domain.Core (Shared Kernel) или Domain Events.

### ❌ "Repository может содержать бизнес-логику"

Нет. Repository — только доступ к данным (CRUD). Бизнес-логика — в Service. Repository не принимает решений, не валидирует бизнес-правила.

---

## Что мы НЕ используем (и почему)

| Паттерн | Почему не используем |
|---------|---------------------|
| **MediatR** | Unified Services достаточно. MediatR добавляет implicit dependencies и boilerplate. |
| **CQRS** | Нет отдельной read-модели, домен не настолько сложен. |
| **Event Sourcing** | Нет требований к аудиту всех изменений или time-travel. |
| **Specification Pattern** | Текущие запросы не требуют. Добавим если запросы станут сложными. |
| **BDD / SpecFlow** | Unit tests + integration tests достаточно. BDD добавляет overhead без пользы для текущего размера. |

---

## Ссылки

- [Системный обзор](./OVERVIEW.md) — Компоненты, порты, потоки данных
- [База данных](./DATABASE.md) — Схема БД
- [Аутентификация](./AUTHENTICATION.md) — Сессии, токены, безопасность
- [RBAC](./RBAC.md) — Роли и права доступа
- [Стандарты кода](../standards/CODE.md) — Naming, testing, logging
- [Стандарты API](../standards/API_STANDARDS.md) — REST, endpoints, responses
- [Глоссарий](../reference/GLOSSARY.md) — Термины

---

## Принципы документации

- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
