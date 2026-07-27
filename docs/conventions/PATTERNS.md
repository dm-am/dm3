# Паттерны и структура кода DM3

> **User Story:** "Как структурировать новую фичу? Какой блюпринт?"
>
> **SSOT:** Это единственный источник истины по структуре кода.

---

## Паттерны по слоям

### Domain.Core — Shared Kernel

**Зачем:** Общие контракты без дублирования. Модули общаются через абстракции, не зная друг о друге.

Паттерн из DDD. Domain.Core содержит общие контракты для всех модулей:
- Интерфейсы системных абстракций (`IDateTimeProvider`, `IGuidFactory`)
- Интерфейсы cross-module фич (`ICommentService`, `INotepadRepository`)
- Общие DTO, enums, exceptions

**Правило:** Domain.* НЕ импортирует другие Domain.* — только через Domain.Core.

### Domain.* — Unified Services

**Зачем:** Один интерфейс вместо трех-четырех. Все операции с Game в одном месте. Меньше файлов и DI-регистраций.

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

### Infrastructure.* — Technical Concerns

**Зачем:** Четкое разделение технических ответственностей. Легко найти где реализован кэш, где парсинг, где репозитории.

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

### Repository Pattern

**Зачем:** Абстракция над БД. Domain не знает про EF Core. Тестируемость без реальной БД.

Интерфейс в Domain, реализация в Infrastructure:

```csharp
// Domain.Game/Features/Games/IGameRepository.cs
public interface IGameRepository { ... }

// Infrastructure.Persistence/Repositories/Game/GameRepository.cs
internal class GameRepository : IGameRepository { ... }
```

### Web.API — Feature Folders

**Зачем:** Все связанное с фичей в одном месте. Легко найти controller, service, DTOs для конкретного endpoint.

Controller + ApiService + DTOs группируются в папку фичи:

```
Features/{Module}/{Feature}/
├── {Feature}Controller.cs
├── I{Feature}ApiService.cs
├── {Feature}ApiService.cs
├── {Feature}Request.cs
└── {Feature}Response.cs
```

**Примечание:** Feature Folders — это организация Web.API, не путать с Vertical Slices. Бизнес-логика остается в Domain.

### Workers — Event Handlers

**Зачем:** Асинхронная обработка событий. Отправка email, уведомления, индексация — не блокируют HTTP-ответ.

Минимальная структура — workers это тонкий слой оркестрации:

```
Workers.{Name}/
├── {Name}Consumer.cs     # MassTransit consumer
├── Program.cs
└── Startup.cs
```

**Workers DM3:** `Mail`, `NotificationDispatcher`, `SearchIndexer`.

### Frontend — Feature-Sliced Design (FSD)

**Зачем:** Четкие правила импортов предотвращают спагетти-код. Переиспользуемость компонентов. Масштабируемость frontend.

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

#### Публичный API слайса

- Каждый слайс (`entities/{x}`, `features/{x}`, `widgets/{x}`, `pages/{x}`) экспортирует наружу **только через свой barrel** `index.ts`. Deep-import мимо barrel во внутренние файлы слайса запрещен — потребитель не знает внутреннюю раскладку.
- Имя папки слайса == его концепт (kebab-case). Одна папка — одна связная единица.
- `*.spec.ts` лежит **рядом** с тестируемой единицей (co-located), а не в отдельном дереве тестов.

#### Единственное разрешенное исключение из запрета same-layer импортов: `@x`

Слои одного уровня не импортируют друг друга напрямую. Единственная санкционированная дверь между двумя слайсами одного слоя — явный cross-import public API в папке `@x`:

```
entities/user/@x/game.ts        # что entities/game имеет право взять из entities/user
entities/user/@x/testimonial.ts # что entities/testimonial имеет право взять из entities/user
```

Потребитель импортирует из `@/entities/user/@x/game`, а не из корня чужого слайса. Всякий same-layer импорт вне `@x` — нарушение. `@x` создается точечно и держится узким (реэкспорт только реально нужных символов).

#### Suffix `*Page` — только у route-target

- Компонент, на который **напрямую указывает роут** верхнего уровня, несет суффикс `*Page` (`CreateGamePage`, `PulsePage`).
- Вложенное под-представление внутри страницы (таб, секция, под-view игры/блога) — **без** глобального `*Page`; действует локальная конвенция слайса. Не навешивать `*Page` на каждый под-компонент.

#### Модалки — общий примитив `Dialog`

Модальные окна строятся на общем примитиве `Dialog` (`shared/ui/Layout/Dialog.vue`) + `DialogTitle`; конкретные модалки именуются `*Dialog`. **Самокатные оверлеи запрещены** — не разворачивать собственный backdrop/focus-trap/teleport в обход примитива (единственное задокументированное исключение — `MobileDrawer`, см. UI_STANDARDS).

#### Размещение пикеров и редакторов

- **Доменный lookup** (поиск/выбор конкретной сущности) живет в `entities/{x}`: composable в `model/`, UI-пикеры в `ui/`.
- **Domain-free композер** (редактор, оболочка фильтров), не знающий ни одной доменной сущности, живет в `shared/ui/` и получает доменные части через слот/проп, а не импортом сущности.

### Tests — Testing Pyramid

**Зачем:** Быстрая обратная связь. Unit tests дешевые и быстрые, ловят большинство багов. E2E дорогие — только критичные сценарии.

Стратегия распределения тестов: Unit tests (Domain) > Integration tests (API) > E2E tests.

### Tests — Mirrored Structure

**Зачем:** Легко найти тесты для любого класса. Нет вопросов "где тест для X?".

Организация файлов — тесты зеркалят production код:
```
Domain.Forum/Features/Topics/TopicService.cs
  → Domain.Forum.Tests/Features/Topics/TopicServiceShould.cs
```

---

## Naming Conventions

> **Принципы:** Унифицированность, самодокументируемость, future-proof.

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
| **Community** | User, UserProfile, Poll, UserEndorsement, WebsiteTestimonial | Публичные данные |
| **Moderation** | ModeratedProfile, ModeratedProfileNote, Warning, Ban, Ticket, Mentorship | Данные для модераторов |
| **Messaging** | Chat, Message, GlobalChatEvent | — |
| **Game** | Game, Room, Post, Character, GameComment, GameBlacklist, GameNotepad, GameInvitation | — |
| **Blog** | Blog, Publication, BlogComment, PublicationComment, BlogBlacklist, BlogInvitation | — |
| **Forum** | Board, Topic, TopicComment | — |

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

**Запрещенные названия:**
- ❌ `MyProfile` — использовать `Profiles/` в модуле Personal
- ❌ `Self` — использовать `Profiles/` в модуле Personal
- ❌ `Blacklist` (ед.ч.) — использовать `Blacklists/`
- ❌ `Notepad` (ед.ч.) — использовать `Notepads/`

### 6. Frontend (FSD) — стиль именования

| Элемент | Стиль | Примеры |
|---------|-------|---------|
| Папки слоев (`entities/`, `features/`, `pages/`) | **kebab-case** | `global-chat/`, `create-game/`, `profile-note/` |
| Папки UI компонентов (`shared/ui/`) | **PascalCase** | `Button/`, `Modal/` |
| Файлы Vue компонентов | **PascalCase** | `UserProfile.vue`, `GameCard.vue` |
| Composables/utils | **camelCase** | `useAuth.ts`, `formatDate.ts` |

**Префикс компонентов в `entities/{module}/ui`:**
- Компонент уровня самого модуля берет **префикс модуля** (`entities/game` → `GameCard`, `GameLink`).
- Компонент под-сущности внутри модуля берет **имя под-сущности**, а не имя модуля (персонаж внутри `entities/game` → `Character*`, не `GameCharacter`).

---

## DTO Projection Pattern (Expansion Hierarchy)

### Принцип

DTOs организованы в иерархию наследования с тремя уровнями:

```
{Entity}Ref → {Entity} → {Entity}Details
```

| Уровень | Назначение | Пример |
|---------|-----------|--------|
| `{Entity}Ref` | Минимум для сайдбаров/меню | `GameRef`, `BlogRef`, `UserRef` |
| `{Entity}` | Средний уровень для таблиц/карточек | `Game`, `Blog`, `User` |
| `{Entity}Details` | Полный для детальных страниц | `GameDetails`, `BlogDetails`, `UserProfile` |

### Правила

**1. Counts + Details:** Counts (`playersCount`) присутствуют на **ВСЕХ** уровнях. Arrays (`players[]`) **ДОБАВЛЯЮТСЯ**, не заменяют counts.

```csharp
// GameRef — сайдбар (counts only)
{
  "id": "...",
  "title": "...",
  "playersCount": 5,
  "subscribersCount": 12
}

// Game — таблица (counts + arrays)
{
  "id": "...",
  "title": "...",
  "playersCount": 5,
  "subscribersCount": 12,
  "players": [{ "id": "...", "username": "..." }],  // ДОБАВЛЯЕТСЯ
  "readers": [...]
}
```

**2. Строгое наследование:** Каждый уровень `extends` предыдущий:

```csharp
// Backend (C#)
public class Game : GameRef { ... }
public class GameDetails : Game { ... }

public class User : UserRef { ... }
public class UserProfile : User { ... }
```

```typescript
// Frontend (TypeScript)
export interface Game extends GameRef { ... }
export interface GameDetails extends Game { ... }

export interface User extends UserRef { ... }
```

**3. Nullable для expansions:** Расширяемые массивы nullable — могут быть не запрошены:

```csharp
public class Game : GameRef
{
    // Inherited: PlayersCount, SubscribersCount (always present)

    // Expanded (nullable - populated when requested)
    public IEnumerable<UserRef>? Players { get; set; }
    public IEnumerable<UserRef>? Readers { get; set; }
}
```

**4. UserRef vs User:**
- В Ref/основном типе — `UserRef` (lightweight: id, username, lastActivityUtc)
- В Details — `User` где нужна полная информация

### API

```
GET /games?projection=ref   → GameRef[]   (сайдбары)
GET /games                  → Game[]      (таблицы)
GET /games/{id}             → GameDetails (страница)

GET /blogs?projection=ref   → BlogRef[]
GET /blogs                  → Blog[]
```

### Иерархии

```
UserRef → User → UserProfile → PersonalProfile
                            → ModeratedProfile

GameRef → Game → GameDetails

BlogRef → Blog → BlogDetails
```

---

## Целевые структуры (Блюпринт)

> `{Placeholder}` — переменная часть.

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
├── Search/               # ISearchService (cross-module)
├── Subscriptions/        # ISubscriptionRepository (cross-module)
├── Tokens/               # Token, CreateToken (shared DTO)
├── UnreadCounters/       # IUnreadCountersRepository (cross-module)
├── Uploads/              # IImageProcessingService, IUploadGarbageCollector (cross-module)
└── Users/                # IUserLookupService (cross-module)
```

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

### DM.Infrastructure.Mail

```
DM.Infrastructure.Mail/
├── Rendering/
├── Assets/
└── MailModule.cs
```

### DM.Infrastructure.Messaging

```
DM.Infrastructure.Messaging/
├── GeneralBus/
├── Outbox/
└── MessagingModule.cs
```

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
│   ├── BackgroundServices/
│   ├── BbRendering/
│   ├── Binding/
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

### DM.Workers.{Name}

```
DM.Workers.{Name}/
├── {Name}Consumer.cs
├── Program.cs
└── Startup.cs
```

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

## Частые ошибки

### ❌ "Запрос к удаленным записям пишется как обычный"

Нет. Мягкое удаление скрыто глобальным фильтром запросов: любой запрос, которому нужны удаленные строки (сборка мусора, отличие 404 от 410, восстановление), обязан явно отключить фильтр. Без этого условие превращается во взаимоисключающее и не находит ничего — молча, без ошибки.

### ❌ "Порядок регистрации middleware — дело вкуса"

Нет. Компонент, читающий метаданные эндпоинта, обязан стоять после маршрутизации, иначе он не видит ничего и работает вхолостую. Заголовки безопасности, наоборот, регистрируются первыми — иначе они не попадут на ответы, отданные более ранними обработчиками (статика, документация). Оба класса ошибок не проявляются ни в сборке, ни в тестах, ни на глаз.

### ❌ "Один scope может отдать несколько экземпляров DbContext"

Нет. Один scope — ровно один контекст, и это проверяется тестом. Следствие: внутри одного scope нельзя выполнять запросы к БД параллельно (`Task.WhenAll` над двумя репозиториями) — контекст не потокобезопасен. Нужна параллельность — нужен отдельный scope на ветку.

### ❌ "Сканирование сборок может переопределять явную регистрацию"

Нет. Автоматическая регистрация по соглашению только заполняет пробелы и никогда не побеждает явную: иначе настройки пула, фабрики типизированных клиентов и время жизни молча заменяются на дефолтные.

### ❌ "Модули могут вызывать сервисы друг друга"

Нет. `Domain.Game` не может вызывать `INotificationService` из `Domain.Personal`. Коммуникация — только через Domain Events.

### ❌ "Infrastructure.* может определять бизнес-интерфейсы"

Нет. Бизнес-интерфейсы (`IGameService`, `ITopicRepository`) — в Domain.Core или Domain.*.
Инфраструктурные интерфейсы (обертки над внешними библиотеками типа `IBbParserProvider`) остаются в Infrastructure.*.

### ❌ "Web.Client должен следовать Clean Architecture"

Нет. Clean Architecture — для backend. Frontend использует **Feature-Sliced Design (FSD)**.

### ❌ "Тесты можно организовать плоско или по-своему"

Нет. Тесты зеркалят структуру production кода.

### ❌ "Domain.Core может содержать бизнес-логику"

Нет. Domain.Core — это **Shared Kernel**: интерфейсы, DTO, enums, exceptions. Никакой бизнес-логики.

### ❌ "Features везде означает одно и то же"

Нет. `Features/` в разных слоях — разные концепции:
- **Domain.*/Features/** — бизнес-фичи (сервисы, репозитории, DTOs)
- **Web.API/Features/** — Feature Folders (controller + API service + request/response)
- **Web.Client/features/** — FSD слой действий пользователя (формы, кнопки)

### ❌ "Feature Folders = Vertical Slices"

Нет. Feature Folders в Web.API — это только организация API-слоя. Бизнес-логика остается в Domain.

### ❌ "Workers содержат бизнес-логику"

Нет. Workers — тонкий слой оркестрации. Вся бизнес-логика в Domain.*.

### ❌ "Сервисы надо разбивать на CreateGameService, UpdateGameService"

Нет. Мы используем **Unified Services** — один сервис на фичу со всеми CRUD-операциями.

### ❌ "Infrastructure организуется по фичам"

Нет. Infrastructure организуется по **Technical Concerns**.

### ❌ "FSD entities могут импортировать друг друга"

Нет. В FSD слои одного уровня НЕ импортируют друг друга.

### ❌ "Domain.Game может импортировать Domain.Blog"

Нет. Domain.* НЕ импортирует другие Domain.* напрямую. Только через Domain.Core или Domain Events.

### ❌ "Repository может содержать бизнес-логику"

Нет. Repository — только доступ к данным (CRUD). Бизнес-логика — в Service.

---

## Что НЕ используем (и почему)

| Паттерн | Почему не используем |
|---------|---------------------|
| **MediatR** | Unified Services достаточно. MediatR добавляет implicit dependencies и boilerplate. |
| **CQRS** | Нет отдельной read-модели, домен не настолько сложен. |
| **Event Sourcing** | Нет требований к аудиту всех изменений или time-travel. |
| **Specification Pattern** | Текущие запросы не требуют. Добавим если запросы станут сложными. |
| **BDD / SpecFlow** | Unit tests + integration tests достаточно. |

---

## Рендеринг BBCode

Пользовательский контент в формате BBCode **рендерится на сервере до сериализации JSON**, всегда. Клиент на display-путях биндит `v-html` на уже отрендеренную строку — никогда не вызывает BBCode-renderer. Это инвариант конфиденциальности: privacy-sensitive теги (`[private]`, `[mod]`) не могут быть отфильтрованы на клиенте безопасно.

Исключение — внутренняя работа редактора: BBCodeEditor поддерживает два режима (WYSIWYG превью и raw BBCode source); переключение между ними — не display-путь, и клиентский BBCode↔HTML конвертер используется **только внутри модуля редактора**. Source of truth на фронте — всегда BBCode, он и отправляется на сохранение.

Полный контракт — правила surface, audience-модель, матрица видимости privacy-тегов, permission-бакеты кэша, асимметричный контракт редактора — в [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md).

---

## Ссылки

- [SYSTEM.md](../architecture/SYSTEM.md) — архитектура системы
- [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md) — рендеринг BBCode (SSOT)
- [CODE_STYLE.md](./CODE_STYLE.md) — стиль кода
- [API_DESIGN.md](./API_DESIGN.md) — проектирование API
