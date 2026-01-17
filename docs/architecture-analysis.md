# Архитектурный анализ DM3

## Часть 1: Текущая архитектура

---

## 1. Общая структура проекта

### 1.1 Backend проекты (src/)

| Проект | Назначение |
|--------|-----------|
| **DM.Web.API** | Главный REST API + WebSocket хаб |
| **DM.Web.Core** | Middleware, аутентификация, SignalR |
| **DM.Services.Core** | Базовые сервисы (логирование, correlation tokens) |
| **DM.Services.DataAccess** | EF Core + MongoDB интеграция |
| **DM.Services.Authentication** | Аутентификация и сессии |
| **DM.Services.Common** | Общие сервисы (авторизация, счётчики непрочитанного) |
| **DM.Services.Community** | Пользователи, опросы, отзывы, чат |
| **DM.Services.Forum** | Форумы, топики, комментарии |
| **DM.Services.Gaming** | Игры, комнаты, персонажи, посты |
| **DM.Services.Uploading** | Загрузка файлов (MinIO) |
| **DM.Services.Notifications** | Система уведомлений |
| **DM.Services.MessageQueuing** | Абстракция RabbitMQ |
| **DM.Services.Mail.Sender** | Отправка email |
| **DM.Services.Mail.Rendering** | Рендеринг email шаблонов |
| **DM.Services.Search** | gRPC клиент для поиска |

### 1.2 Background consumers

| Consumer | Порт | Назначение |
|----------|------|-----------|
| **Notifications.Consumer** | 5053 | Генерация уведомлений |
| **Search.Consumer** | 5052 | Индексация в OpenSearch + gRPC API |
| **Mail.Sender.Consumer** | 5054 | Отправка email через SMTP |

### 1.3 Frontend (Vue 3 + TypeScript + Pinia)

```
frontend/DM.Web.Modern.Temp/
├── src/
│   ├── api/           # Axios + модели + запросы
│   ├── stores/        # Pinia stores (8 штук)
│   ├── router/        # Vue Router
│   ├── composables/   # Vue composables
│   ├── components/    # Переиспользуемые компоненты
│   ├── views/         # Страницы и layout
│   └── assets/        # Стили (SASS) и изображения
```

---

## 2. Инфраструктура (Docker)

### 2.1 Сервисы

| Сервис | Контейнер | Порт | Назначение |
|--------|-----------|------|-----------|
| PostgreSQL | dm-pg | 5432 | Основная БД |
| MongoDB | dm-mongo | 27017 | NoSQL (опросы, сессии, счётчики) |
| RabbitMQ | dm-rmq | 5672, 15672 | Message broker |
| OpenSearch | dm-es | 9200 | Полнотекстовый поиск + логи |
| MinIO | dm-minio | 9000, 9001 | S3-совместимое хранилище файлов |
| MailHog | dm-mailhog | 1025, 5025 | Перехват email (dev) |
| Jaeger | dm-jaeger | 16686 | Distributed tracing |
| Kibana | dm-kibana | 5601 | Дашборд OpenSearch |

### 2.2 Приложения

| Сервис | Контейнер | Порт | Назначение |
|--------|-----------|------|-----------|
| API | dm-api | 5051 | REST API + SignalR |
| Migration | dm-migration | - | Миграции БД при старте |
| Notifications | dm-notifications-consumer | 5053 | Генерация уведомлений |
| Search | dm-search-engine-consumer | 5052 | Индексация + gRPC |
| Mail | dm-email-sender-consumer | 5054 | Отправка email |

---

## 3. Слои архитектуры

### 3.1 Поток запроса

```
HTTP Request
    ↓
┌─────────────────────────────────────────────────────────┐
│ Middleware Pipeline                                      │
│  1. Swagger                                              │
│  2. CorrelationMiddleware (X-Dm-Correlation-Token)       │
│  3. ErrorHandlingMiddleware (exceptions → ProblemDetails)│
│  4. AuthenticationMiddleware (X-Dm-Auth-Token → Identity)│
│  5. CORS                                                 │
│  6. Routing                                              │
│  7. Health Checks (/_health)                             │
│  8. Controllers + SignalR Hub (/whatsup)                 │
└─────────────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────────────┐
│ Controller (DM.Web.API/Controllers/)                     │
│  - Принимает HTTP запрос                                 │
│  - Вызывает API Service                                  │
│  - Возвращает Envelope<T> / ListEnvelope<T>             │
└─────────────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────────────┐
│ API Service (DM.Web.API/Services/)                       │
│  - Маппинг DTO (AutoMapper)                             │
│  - Вызывает Business Service                             │
└─────────────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────────────┐
│ Business Service (DM.Services.*/BusinessProcesses/)      │
│  - Бизнес-логика                                        │
│  - Авторизация (IntentionManager)                       │
│  - Валидация (FluentValidation)                         │
│  - Публикация событий (IInvokedEventProducer)           │
│  - Вызывает Repository                                  │
└─────────────────────────────────────────────────────────┘
    ↓
┌─────────────────────────────────────────────────────────┐
│ Repository                                               │
│  - EF Core (PostgreSQL) или MongoDB                     │
│  - ProjectTo<T> для эффективной проекции                │
└─────────────────────────────────────────────────────────┘
```

### 3.2 Dependency Injection (Autofac)

**Модули:**
```
CoreModule
├── DataAccessModule
├── AuthenticationModule
├── CommonModule
├── CommunityModule (+ MailSender, Rendering)
├── ForumModule (+ MessageQueuing)
├── GamingModule (+ MessageQueuing)
├── NotificationsModule
└── WebCoreModule
```

**Паттерн регистрации:**
```csharp
builder.RegisterDefaultTypes();     // Автоматическая регистрация всех классов
builder.RegisterModuleOnce<Module>(); // Предотвращение дублирования
builder.RegisterMapper();           // AutoMapper profiles
```

---

## 4. Хранилища данных

### 4.1 PostgreSQL (EF Core)

**37 DbSet'ов:**

| Категория | Таблицы |
|-----------|---------|
| Users | User, Token |
| Common | Comment, Like, Review, TagGroup, Tag, Upload |
| Forum | Forum (Boards), ForumTopic, ForumModerator |
| Games | Game, GameTag, Reader, BlackListLink, RoomClaim, Character, CharacterAttribute, Room, Post, PendingPost, Vote |
| Messaging | Conversation, UserConversationLink, Message, ChatMessage |
| Admin | Report, Warning, Ban |

**Ключевые особенности:**
- Soft delete (IRemovable интерфейс)
- Денормализация для производительности (Forum.TopicsCount, LastComment*)
- Полиморфные FK (Comment.EntityId → ForumTopic или Game)

### 4.2 MongoDB

| Коллекция | Назначение |
|-----------|-----------|
| Polls | Опросы с вложенными вариантами |
| Dice | Броски кубиков |
| AttributeSchemata | Схемы атрибутов персонажей |
| UnreadCounters | Счётчики непрочитанного |
| UserSettings | Настройки пользователей |
| UserSessions | Сессии аутентификации |
| RealtimeNotifications | Push-уведомления |

---

## 5. Аутентификация и авторизация

### 5.1 Аутентификация

```
1. Клиент → Header: X-Dm-Auth-Token
2. AuthenticationMiddleware → ICredentialsStorage.ExtractToken()
3. IWebAuthenticationService.Authenticate()
4. IIdentityProvider.Current = identity (scoped)
```

**Токены:** Хранятся в MongoDB (UserSessions), привязаны к User.

### 5.2 Авторизация (Intention Pattern)

```csharp
// Определение намерений
public enum PollIntention { Create, Vote, Unvote }

// Резолвер
public class PollIntentionResolver : IIntentionResolver<PollIntention>
{
    public bool IsAllowed(PollIntention intention) => intention switch
    {
        PollIntention.Create => identity.User.Role.HasFlag(UserRole.Administrator),
        PollIntention.Vote => identity.User.IsAuthenticated && !poll.HasEnded,
        _ => false
    };
}

// Использование в сервисе
intentionManager.ThrowIfForbidden(PollIntention.Create);
```

---

## 6. Message Queue (RabbitMQ)

### 6.1 Архитектура событий

```
Business Service
    ↓ producer.Send(EventType.NewForumTopic, topicId)
    ↓
┌─────────────────────────────────────────────────────────┐
│ Exchange: dm.events (Topic)                             │
│ Routing Key: forum.topic.created                        │
└─────────────────────────────────────────────────────────┘
    ↓                    ↓                    ↓
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ Queue:       │  │ Queue:       │  │ Queue:       │
│ dm.search    │  │ dm.notif     │  │ dm.mail      │
│ -engine      │  │ -ications    │  │ .sender      │
└──────────────┘  └──────────────┘  └──────────────┘
    ↓                    ↓                    ↓
 OpenSearch         MongoDB           SMTP (MailHog)
```

### 6.2 EventType (примеры)

| Event | Routing Key | Consumers |
|-------|-------------|-----------|
| ActivatedUser | community.user.activated | Search, Notifications |
| NewForumTopic | forum.topic.created | Search, Notifications |
| NewForumComment | forum.comment.created | Search, Notifications |
| NewCharacter | game.character.created | Notifications |

---

## 7. Frontend архитектура

### 7.1 Pinia Stores

| Store | Назначение | Кэширование |
|-------|-----------|-------------|
| user | Аутентификация, профиль | localStorage |
| ui | Тема оформления | - |
| boards | Форумы, топики, комментарии | 60 сек (stale-while-revalidate) |
| games | Списки игр по категориям | 60 сек |
| community | Пользователи | - |
| polls | Опросы и голосование | - |
| websiteReviews | Отзывы о сайте | - |
| chat | Глобальный чат | - |

### 7.2 API Layer

```typescript
// Типизация: Served<T> для серверных полей
type User = {
  login: Served<UserLogin>  // read-only
  status: string            // editable
}

// Post<T> извлекает только редактируемые поля
type CreateUser = Post<User>  // { status: string }
```

### 7.3 Темы оформления (5 штук)

- Modern (светлая, по умолчанию)
- ClassicPale (контрастная светлая)
- Classic (традиционная)
- Pale (бледная)
- Night (тёмная)

---

## 8. Observability

### 8.1 Логирование (Serilog)

```
Sinks:
  - OpenSearch: dm_logstash-{yyyy.MM.dd}
  - Console

Enrichers:
  - Application name
  - Environment
  - LogContext
```

### 8.2 Tracing (OpenTelemetry → Jaeger)

Инструментация:
- ASP.NET Core requests
- gRPC calls
- HTTP client
- EF Core queries
- MongoDB operations
- RabbitMQ (Jamq)

---

## Часть 2: Сравнение с предыдущей версией (b8331728)

---

## 1. Переименованные файлы/папки

### Backend

| Было | Стало |
|------|-------|
| `BusinessObjects/Fora/` | `BusinessObjects/Boards/` |
| `Services/Fora/` | `Services/Boards/` |
| `Controllers/v1/Fora/` | `Controllers/v1/Forums/` |
| `Dto/Fora/` | `Dto/Boards/` |
| `ReviewController.cs` | `WebsiteReviewController.cs` |
| `MessagingController.cs` | `MessageController.cs` + `ConversationController.cs` + `GlobalChatController.cs` |

### Frontend

| Было | Стало |
|------|-------|
| `stores/fora.ts` | `stores/boards.ts` |
| `stores/reviews.ts` | `stores/websiteReviews.ts` |
| `TheReview.vue` | `TheWebsiteReview.vue` |
| `ReviewList.vue` | `WebsiteReviewList.vue` |
| `RandomReview.vue` | `RandomWebsiteReview.vue` |
| `api/models/community/reviews.ts` | `websiteReviews.ts` |

---

## 2. Новые файлы

### Backend

| Файл | Назначение |
|------|-----------|
| `Controllers/v1/Forums/BoardController.cs` | Управление досками |
| `Controllers/v1/Messaging/GlobalChatController.cs` | Глобальный чат |
| `Controllers/v1/Messaging/ConversationController.cs` | Личные сообщения |
| `Dto/Boards/Board.cs` | DTO для доски |
| `20260114000000_RenameRecruitmentForum.cs` | Миграция: переименование форума |
| `20260114100000_LimitLoginLength.cs` | Миграция: ограничение длины логина |
| `20260115000000_AddBoardDenormalizedFields.cs` | Миграция: денормализация |
| `20260115100000_UpdateBoardDescriptions.cs` | Миграция: обновление описаний |

### Frontend

| Файл | Назначение |
|------|-----------|
| `stores/chat.ts` | Store для чата |
| `api/requests/chatApi.ts` | API чата |
| `api/models/chat/` | Модели чата |
| `views/pages/chat/ChatPage.vue` | Страница чата |
| `views/pages/blogs/BlogsPage.vue` | Страница блогов |
| `views/pages/games/GamesPage.vue` | Страница игр |
| `views/pages/moderation/ModerationPage.vue` | Модерация |
| `views/pages/forum/ForumIndexPage.vue` | Индекс форумов |
| `components/inputs/UserAutocomplete.vue` | Автокомплит пользователей |
| `components/layout/SidebarTitle.vue` | Заголовок сайдбара |
| `views/account/PasswordReset.vue` | Сброс пароля |

---

## 3. Удалённые файлы

| Файл | Причина |
|------|---------|
| `Controllers/v1/Fora/ForumController.cs` | Заменён на BoardController + новый ForumController |
| `Controllers/v1/Community/ChatController.cs` | Перенесён в Messaging namespace |

---

## 4. Изменения API

### Переименованные эндпоинты

| Было | Стало |
|------|-------|
| `GET /v1/reviews` | `GET /v1/websitereviews` |
| `POST /v1/reviews` | `POST /v1/websitereviews` |
| `GET /v1/reviews/{id}` | `GET /v1/websitereviews/{id}` |
| `PATCH /v1/reviews/{id}` | `PATCH /v1/websitereviews/{id}` |
| `DELETE /v1/reviews/{id}` | `DELETE /v1/websitereviews/{id}` |
| `GET /v1/games/own` | `GET /v1/games/owned` |

### Новые эндпоинты

| Эндпоинт | Назначение |
|----------|-----------|
| `GET /v1/boards` | Список досок |
| `GET /v1/boards/{id}` | Детали доски |
| `GET /v1/boards/{id}/moderators` | Модераторы доски |
| `DELETE /v1/boards/{id}/comments/unread` | Пометить всё прочитанным |
| `GET /v1/globalchat/messages` | Сообщения чата |
| `POST /v1/globalchat/messages` | Отправить сообщение |
| `GET /v1/conversations` | Список диалогов |
| `GET /v1/conversations/{id}` | Детали диалога |
| `GET /v1/conversations/visavi/{login}` | Диалог с пользователем |

---

## 5. Изменения БД

### Миграция: AddBoardDenormalizedFields

**Новые поля в ForumTopic:**
- `LastUpdateDate` (DateTimeOffset?) — дата последнего обновления

**Новые поля в Forum (Boards):**
- `TopicsCount` (int) — кэшированное количество топиков
- `CommentsCount` (int) — кэшированное количество комментариев
- `LastCommentId` (Guid?) — FK на последний комментарий
- `LastCommentTopicId` (Guid?) — топик последнего комментария
- `LastCommentAuthorId` (Guid?) — автор последнего комментария
- `LastCommentDate` (DateTimeOffset?) — дата последнего комментария

**Новые индексы:**
- `IX_Fora_LastCommentId`
- `IX_Fora_LastCommentAuthorId`

**Цель:** Оптимизация запросов списка форумов (избежание JOIN и COUNT).

### Миграция: LimitLoginLength

**Изменение:** `Users.Login` VARCHAR(100) → VARCHAR(20)

---

## 6. Структурные изменения

### Реорганизация Messaging

**Было:**
```
Controllers/v1/Community/
├── MessagingController.cs  (всё в одном)
```

**Стало:**
```
Controllers/v1/Messaging/
├── MessageController.cs        (CRUD сообщений)
├── ConversationController.cs   (диалоги)
└── GlobalChatController.cs     (глобальный чат)
```

### Терминология

| Было | Стало | Причина |
|------|-------|---------|
| Fora | Boards | Более понятный термин |
| Reviews | WebsiteReviews | Отличие от возможных игровых отзывов |

---

## 7. Сводка изменений

| Категория | Количество |
|-----------|-----------|
| Переименованных файлов | ~25 |
| Новых файлов | ~20 |
| Удалённых файлов | 2 |
| Новых миграций | 4 |
| Новых API эндпоинтов | ~10 |
| Изменённых компонентов | ~50 |

---

## 8. Архитектурные улучшения

1. **Денормализация БД** — кэширование счётчиков и последней активности в Forum
2. **Разделение Messaging** — чёткое разделение ответственности между контроллерами
3. **Унификация терминологии** — Fora → Boards, Reviews → WebsiteReviews
4. **Новые страницы** — Chat, Blogs, Games, Moderation, ForumIndex
5. **Улучшенная навигация** — новые компоненты для сайдбара и меню
6. **Stale-while-revalidate** — кэширование на фронтенде (60 сек)

---

## Приложение: Диаграмма архитектуры

```
┌──────────────────────────────────────────────────────────────────────────┐
│                           КЛИЕНТЫ                                        │
│                    (Web, Mobile, API)                                    │
└────────────────────────────┬─────────────────────────────────────────────┘
                             │ HTTP/WebSocket
                             ▼
┌───────────────────────────────────────────────────────────────────────────┐
│                       DM.Web.API (5051)                                   │
│  ┌─────────────────┐  ┌──────────────────────┐  ┌──────────────────────┐  │
│  │ REST Controllers│  │  SignalR Hub         │  │ RealtimeNotification │  │
│  │ (Forums, Games, │  │  (/whatsup)          │  │ Consumer             │  │
│  │  Community)     │  │                      │  │                      │  │
│  └─────────────────┘  └──────────────────────┘  └──────────────────────┘  │
└────┬───────────────────────────────┬───────────────────────────┬──────────┘
     │ RabbitMQ                      │ gRPC                      │ SignalR
     │                               │                           │
     ▼                               ▼                           ▼
┌──────────────────┐     ┌─────────────────────────┐   ┌────────────────────┐
│  dm.events       │     │ Search Consumer (5052)  │   │ Клиенты WebSocket  │
│  (Topic Exchange)│     │ - Индексация            │   │                    │
└──────────────────┘     │ - gRPC Search API       │   └────────────────────┘
     │                   └─────────────────────────┘
     │                              │
     ├──────────────────────────────┼──────────────────────────────┐
     │                              ▼                              │
     │                   ┌─────────────────────────┐               │
     │                   │ OpenSearch (9200)       │               │
     │                   │ Index: dm_search        │               │
     │                   └─────────────────────────┘               │
     │                                                             │
     ├───────────────────────────────────────────────┐             │
     │                                               │             │
     ▼                                               ▼             ▼
┌──────────────────┐                    ┌──────────────────┐ ┌──────────────┐
│ Notifications    │                    │ Mail Sender      │ │ dm.notif.    │
│ Consumer (5053)  │                    │ Consumer (5054)  │ │ sent         │
│ - Генерация      │───────────────────▶│ - SMTP отправка  │ │ (Exchange)   │
│   уведомлений    │                    │ - Dead Letter Q  │ └──────────────┘
└──────────────────┘                    └──────────────────┘        │
     │                                           │                  │
     ▼                                           ▼                  │
┌──────────────────┐                    ┌──────────────────┐        │
│ MongoDB (27017)  │                    │ MailHog (1025)   │        │
│ - Notifications  │                    │ (dev email)      │        │
│ - Sessions       │                    └──────────────────┘        │
│ - Polls          │                                                │
└──────────────────┘                                                │
                                                                    │
┌───────────────────────────────────────────────────────────────────┘
│
▼
┌──────────────────────────────────────────────────────────────────────────┐
│                      ВНЕШНИЕ СЕРВИСЫ                                     │
│                                                                          │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐  ┌─────────────┐   │
│  │ PostgreSQL   │  │ RabbitMQ     │  │ MinIO        │  │ Jaeger      │   │
│  │ (5432)       │  │ (5672/15672) │  │ (9000/9001)  │  │ (16686)     │   │
│  │ Основная БД  │  │ Message Bus  │  │ S3 хранилище │  │ Tracing     │   │
│  └──────────────┘  └──────────────┘  └──────────────┘  └─────────────┘   │
│                                                                          │
└──────────────────────────────────────────────────────────────────────────┘
```
