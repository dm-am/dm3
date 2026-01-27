# Архитектура DM3

> **Обновлено:** 2026-01-26

## Общая картина

```
┌─────────────────────────────────────────────────────────────────┐
│                        ПОЛЬЗОВАТЕЛЬ                             │
│                    (браузер / мобильное приложение)             │
└─────────────────────────────┬───────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     FRONTEND (Vue 3)                            │
│                     http://localhost:5173                       │
│                                                                 │
│  - 9 Pinia stores (состояние приложения)                       │
│  - 19 маршрутов (страницы)                                     │
│  - 6 API-модулей (запросы к серверу)                           │
└─────────────────────────────┬───────────────────────────────────┘
                              │ HTTP запросы
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      BACKEND API                                │
│                   http://localhost:5051                         │
│                                                                 │
│  - 20 контроллеров                                             │
│  - ~130 API endpoints                                          │
│  - Авторизация через токены                                    │
└────────┬──────────────┬──────────────┬─────────────┬────────────┘
         │              │              │             │
         ▼              ▼              ▼             ▼
┌──────────────┐ ┌────────────┐ ┌───────────┐ ┌───────────┐
│ PostgreSQL   │ │  MongoDB   │ │ RabbitMQ  │ │  MinIO    │
│ (основные    │ │ (счётчики, │ │ (очередь  │ │ (файлы,   │
│  данные)     │ │  сессии)   │ │  событий) │ │  картинки)│
└──────────────┘ └────────────┘ └─────┬─────┘ └───────────┘
                                     │
         ┌───────────────────────────┼───────────────────────────┐
         │                           │                           │
         ▼                           ▼                           ▼
┌──────────────────┐    ┌──────────────────┐    ┌──────────────────┐
│ Search Consumer  │    │ Notif. Consumer  │    │ Mail Consumer    │
│ (индексация      │    │ (уведомления)    │    │ (отправка email) │
│  поиска)         │    │                  │    │                  │
└────────┬─────────┘    └──────────────────┘    └────────┬─────────┘
         │                                               │
         ▼                                               ▼
┌──────────────────┐                          ┌──────────────────┐
│   OpenSearch     │                          │     MailHog      │
│   (поиск)        │                          │   (тест email)   │
└──────────────────┘                          └──────────────────┘
```

---

## Backend проекты

### Главные проекты

| Проект | Назначение |
|--------|-----------|
| **DM.Web.API** | Главный REST API + WebSocket хаб |
| **DM.Web.Core** | Middleware, аутентификация, SignalR |
| **DM.Services.DataAccess** | EF Core + MongoDB интеграция |

### Бизнес-логика (по доменам)

| Проект | Назначение |
|--------|-----------|
| **DM.Services.Authentication** | Логин, сессии, токены |
| **DM.Services.Community** | Пользователи, опросы, отзывы, чат |
| **DM.Services.Forum** | Форумы, топики, комментарии |
| **DM.Services.Gaming** | Игры, комнаты, персонажи, посты |
| **DM.Services.Common** | Общее: авторизация, счётчики непрочитанного |

### Инфраструктура

| Проект | Назначение |
|--------|-----------|
| **DM.Services.Core** | Логирование, correlation tokens, время |
| **DM.Services.MessageQueuing** | Отправка событий в RabbitMQ |
| **DM.Services.Uploading** | Загрузка файлов в MinIO |
| **DM.Services.Notifications** | Создание уведомлений |
| **DM.Services.Mail.Sender** | Отправка email |
| **DM.Services.Mail.Rendering** | Шаблоны email |
| **DM.Services.Search** | gRPC клиент для поиска |

### Background consumers

| Проект | Порт | Назначение |
|--------|------|-----------|
| **Notifications.Consumer** | 5053 | Слушает события → создаёт уведомления |
| **Search.Consumer** | 5052 | Слушает события → индексирует в OpenSearch |
| **Mail.Sender.Consumer** | 5054 | Слушает очередь → отправляет email |

---

## Поток запроса

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

### Пример: создание топика

```
1. Пользователь нажимает "Создать топик"
2. Frontend: stores/boards.ts → forumApi.createTopic()
3. HTTP: POST /v1/boards/{id}/topics с токеном
4. Middleware: проверяет токен, ставит identity
5. Controller: TopicController.PostBoardTopic()
6. Service: валидация → проверка прав → создание → событие в RabbitMQ
7. Repository: SaveChangesAsync() → PostgreSQL
8. Consumers: Search индексирует, Notifications создаёт уведомления
9. Ответ: HTTP 201 Created
```

---

## Dependency Injection (Autofac)

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

## Хранилища данных

### PostgreSQL (32 таблицы)

| Домен | Таблицы |
|-------|---------|
| **Users** | User, Token |
| **Common** | Comment, Like, Review, Tag, TagGroup, Upload, OutboxEvent, ContentBlock |
| **Forum** | Board, ForumTopic, BoardModerator |
| **Games** | Game, GameTag, Character, CharacterAttribute, Room, Post, PendingPost, Vote, Reader, BlackListLink, RoomClaim |
| **Messaging** | Conversation (visavi + групповые), UserConversationLink, Message, ChatMessage, ChatMessageEdit, MessageEdit |
| **Admin** | Report, Warning, Ban |

**Таблицы аудита (Edit History):**
- `ChatMessageEdits` - история редактирования сообщений чата
- `CommentEdits` - история редактирования комментариев
- `TopicEdits` - история редактирования тем форума
- `PostEdits` - история редактирования игровых постов
- `CharacterEdits` - история редактирования персонажей

**Особенности:**
- Soft delete через ISoftDeletable (IsRemoved, DeletedByUserId, DeletedAtUtc)
- Аудит редактирования через IEditable (CreatedUtc, ModifiedUtc, ModifiedByUserId)
- История изменений через IHasEditHistory<TEdit>
- Денормализация для производительности (Board.TopicsCount, LastComment*)
- Полиморфные FK (Comment.EntityId → ForumTopic или Game)

### MongoDB

| Коллекция | Назначение |
|-----------|-----------|
| **Polls** | Опросы с вариантами ответов |
| **UnreadCounters** | Счётчики непрочитанного |
| **UserSettings** | Настройки пользователей |
| **UserSessions** | Сессии авторизации |
| **AttributeSchemata** | Схемы атрибутов персонажей |
| **Dice** | Результаты бросков кубиков |
| **RealtimeNotifications** | Push-уведомления |

---

## Ключевые перечисления (Enums)

### GameStatus (3 значения + флаги)

```csharp
public enum GameStatus
{
    Draft = 0,   // Оформляется
    Active = 1,  // Идет игра
    Closed = 2   // Закрыта
}

// Дополнительные флаги в Game:
// - IsFinished: игра завершена успешно
// - IsFrozen: игра заморожена
// - IsRecruitmentOpen: набор игроков открыт
```

### PremoderationStatus

```csharp
public enum PremoderationStatus
{
    Approved = 0,         // Не требует премодерации
    AwaitingApproval = 1, // Ожидает проверки
    AwaitingEdits = 2     // Требует правок
}
```

### CharacterStatus (4 значения + флаги)

```csharp
public enum CharacterStatus
{
    UnderReview = 0,  // Заявка на рассмотрении
    Declined = 1,     // Отклонен
    Active = 2,       // В игре
    Retired = 3       // Вне игры
}

// Дополнительные флаги в Character:
// - IsDead: персонаж мертв
// - IsPlayerLeft: игрок покинул игру
// - IsPlayerExiled: игрок выведен из игры
```

### GameRecruitment (структура)

```csharp
public class GameRecruitment
{
    public bool IsOpen { get; set; }           // Набор открыт
    public int? PlayerLimit { get; set; }      // Лимит игроков (null = безлимит)
    public int PlayerCount { get; set; }       // Текущее число игроков
    public DateTimeOffset? StartedUtc { get; set; } // Когда открылся набор
}
```

---

## Аутентификация

### Вход в систему

```
1. Пользователь вводит логин/пароль
2. POST /v1/account/login
3. Backend:
   → Ищет пользователя в БД
   → Проверяет: активирован? не забанен? пароль верный?
   → Проверяет: нужен ли rehash пароля (opportunistic rehashing)
   → Создаёт сессию в MongoDB
   → Шифрует {userId, sessionId} → токен (TripleDES, ключи из конфигурации)
4. Возвращает токен в x-dm-auth-token
5. Frontend сохраняет в localStorage
```

### Хеширование паролей

- **Алгоритм:** PBKDF2 с SHA256, 100,000 итераций
- **Соль:** 75 bytes, случайная для каждого пользователя
- **Версионирование:** `PasswordHashVersion` для миграции старых хешей
- **Сравнение:** Constant-time через `CryptographicOperations.FixedTimeEquals()`

### Проверка токена

```
1. Frontend: токен в заголовке X-Dm-Auth-Token
2. AuthenticationMiddleware:
   → Расшифровывает токен → userId, sessionId
   → Проверяет сессию в MongoDB
   → Загружает пользователя из PostgreSQL
   → Ставит identity в IIdentityProvider
3. Контроллер получает доступ к текущему пользователю
```

---

## Авторизация (Intention Pattern)

```csharp
// 1. Определяем намерения
public enum TopicIntention { Create, Edit, Delete, Close }

// 2. Реализуем резолвер
public class TopicIntentionResolver : IIntentionResolver<TopicIntention, Topic>
{
    public bool IsAllowed(TopicIntention intention, Topic topic)
    {
        var user = identity.User;
        return intention switch
        {
            TopicIntention.Create => user.IsAuthenticated,
            TopicIntention.Edit => user.UserId == topic.AuthorId || user.IsAdmin,
            TopicIntention.Delete => user.IsAdmin || user.IsModerator,
            TopicIntention.Close => user.IsAdmin,
            _ => false
        };
    }
}

// 3. Используем в сервисе
intentionManager.ThrowIfForbidden(TopicIntention.Delete, topic);
// Нет прав → исключение → 403 Forbidden
```

---

## Система событий (RabbitMQ)

### Архитектура

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
```

### Типы событий

| EventType | Routing Key | Consumers |
|-----------|-------------|-----------|
| ActivatedUser | community.user.activated | Search, Notifications |
| NewForumTopic | forum.topic.created | Search, Notifications |
| NewForumComment | forum.comment.created | Search, Notifications |
| NewCharacter | game.character.created | Notifications |
| NewMessage | messaging.message.created | Notifications |
| **Invitations** | | |
| PlayerInvitationCreated | game.invitation.player.created | Notifications |
| ReaderInvitationCreated | game.invitation.reader.created | Notifications |
| AssignmentRequestCreated | game.assignment.created | Notifications |
| **Game Status** | | |
| StatusGameActive | game.status.active | Notifications |
| StatusGameClosed | game.status.closed | Notifications |
| StatusGameFrozen | game.status.frozen | Notifications |
| StatusGameFinished | game.status.finished | Notifications |
| **Character Status** | | |
| StatusCharacterAccepted | game.character.accepted | Notifications |
| StatusCharacterDeclined | game.character.declined | Notifications |
| StatusCharacterDied | game.character.died | Notifications |
| StatusCharacterLeft | game.character.left | Notifications |
| StatusCharacterReturned | game.character.returned | Notifications |
| **Votes** | | |
| PostVoted | game.post.voted | Notifications |

---

## Система приглашений

### Типы приглашений

| TokenType | Назначение | При Accept |
|-----------|-----------|------------|
| `AssistantAssignment (2)` | Приглашение ассистентом | Становится ассистентом игры |
| `PlayerInvitation (3)` | Приглашение игроком | Добавляется в whitelist (может создать персонажа) |
| `ReaderInvitation (4)` | Приглашение читателем | Автоматическая подписка на игру |

### Поток приглашения

```
1. GM вызывает POST /v1/games/{id}/invitations/players с логином
2. InvitationService:
   → Проверка прав (GameIntention.InvitePlayer)
   → Поиск пользователя по логину
   → Создание Token с типом PlayerInvitation
   → Публикация EventType.PlayerInvitationCreated
3. NotificationConsumer:
   → PlayerInvitationNotificationGenerator создаёт уведомление
   → SignalR доставляет в реальном времени
4. Приглашённый видит уведомление и вызывает PUT /v1/invitations/{id}/accept
5. InvitationService:
   → Проверка валидности токена
   → Добавление в whitelist игры
   → Удаление токена
   → Публикация EventType.PlayerInvitationAccepted
```

### API Endpoints

**Для GM (управление приглашениями игры):**
```
GET    /v1/games/{id}/invitations           - список приглашений
POST   /v1/games/{id}/invitations/players   - пригласить игрока
POST   /v1/games/{id}/invitations/readers   - пригласить читателя
DELETE /v1/games/{id}/invitations/{tokenId} - отменить приглашение
```

**Для пользователя (управление своими приглашениями):**
```
GET    /v1/invitations                      - мои приглашения
PUT    /v1/invitations/{tokenId}/accept     - принять
PUT    /v1/invitations/{tokenId}/reject     - отклонить
```

---

## Система уведомлений

### Notification Generators

| Generator | EventTypes | Получатели |
|-----------|------------|------------|
| `NewCharacterNotificationGenerator` | NewCharacter | GM игры |
| `NewForumCommentNotificationGenerator` | NewForumComment | Автор топика |
| `NewGameCommentNotificationGenerator` | NewGameComment | GM игры |
| `AssistantAssignmentNotificationGenerator` | AssignmentRequestCreated | Приглашаемый |
| `PlayerInvitationNotificationGenerator` | PlayerInvitationCreated | Приглашаемый |
| `ReaderInvitationNotificationGenerator` | ReaderInvitationCreated | Приглашаемый |
| `GameStatusChangedNotificationGenerator` | StatusGame* | Все участники (кроме GM) |
| `PostVotedNotificationGenerator` | PostVoted | Автор поста |
| `CharacterStatusChangedNotificationGenerator` | StatusCharacter* | Владелец персонажа |

### Поток уведомления

```
Business Service
    ↓ producer.Send(EventType.NewCharacter, characterId)
    ↓
RabbitMQ (dm.notifications queue)
    ↓
NotificationConsumer
    ↓ NotificationProcessor.Process(eventType, entityId)
    ↓
NotificationGenerator.Generate(entityId)
    ↓ returns List<RealtimeNotification>
    ↓
MongoDB (RealtimeNotifications collection)
    ↓
SignalR Hub (/whatsup)
    ↓
Frontend (real-time update)
```

---

## Frontend (Vue 3 + TypeScript + Pinia)

### Pinia stores

| Store | Назначение | Кэширование |
|-------|-----------|-------------|
| **user** | Аутентификация, профиль | localStorage |
| **ui** | Тема оформления | - |
| **boards** | Форумы, топики, комментарии | 60 сек |
| **games** | Списки игр по категориям | 60 сек |
| **community** | Пользователи | - |
| **polls** | Опросы | - |
| **websiteReviews** | Отзывы о сайте | - |
| **chat** | Глобальный чат | - |
| **messaging** | Личные сообщения | - |

### API типизация

```typescript
// Served<T> для серверных полей
type User = {
  login: Served<UserLogin>  // read-only
  status: string            // editable
}

// Post<T> извлекает редактируемые поля
type CreateUser = Post<User>  // { status: string }
```

---

## Docker сервисы

### Инфраструктура

| Сервис | Контейнер | Порт | Назначение |
|--------|-----------|------|-----------|
| PostgreSQL | dm-pg | 5432 | Основная БД |
| MongoDB | dm-mongo | 27017 | Вспомогательная БД |
| RabbitMQ | dm-rmq | 5672, 15672 | Очередь сообщений |
| OpenSearch | dm-es | 9200 | Поиск + логи |
| Kibana | dm-kibana | 5601 | UI для OpenSearch |
| MinIO | dm-minio | 9000, 9001 | Хранилище файлов |
| MailHog | dm-mailhog | 1025, 5025 | Тест email |
| Jaeger | dm-jaeger | 16686 | Трассировка запросов |

### Приложения

| Сервис | Контейнер | Порт | Назначение |
|--------|-----------|------|-----------|
| API | dm-api | 5051 | REST API + WebSocket |
| Migration | dm-migration | - | Миграции БД при старте |
| Notifications | dm-notifications-consumer | 5053 | Генерация уведомлений |
| Search | dm-search-engine-consumer | 5052 | Индексация в OpenSearch |
| Mail | dm-email-sender-consumer | 5054 | Отправка email |

---

## Observability

### Логирование (Serilog)

```
Sinks:
  - OpenSearch: dm_logstash-{yyyy.MM.dd}
  - Console

Enrichers:
  - Application name
  - Environment
  - LogContext
```

### Tracing (OpenTelemetry → Jaeger)

Инструментация:
- ASP.NET Core requests
- gRPC calls
- HTTP client
- EF Core queries
- MongoDB operations
- RabbitMQ (Jamq)

---

## Быстрый старт

```bash
# 1. Запустить Docker Desktop

# 2. Поднять все контейнеры
cd "D:\Code Projects\dm3"
docker compose -f docker/docker-compose.yml up -d

# 3. Запустить frontend
cd frontend/DM.Web.Modern
yarn dev

# 4. Открыть в браузере
# Frontend: http://localhost:5173
# API: http://localhost:5051
# RabbitMQ UI: http://localhost:15672 (guest/guest)
# MinIO UI: http://localhost:9001 (minio/miniokey)
# MailHog UI: http://localhost:5025
# Jaeger UI: http://localhost:16686
```

## Полезные команды

**См. полный список команд:** [../getting-started/SETUP.md](../getting-started/SETUP.md#команды-разработки)

Быстрые команды:

```bash
# Запустить всё
docker compose -f docker/docker-compose.yml up -d

# Остановить всё
docker compose -f docker/docker-compose.yml down

# Запустить frontend
cd frontend/DM.Web.Modern && yarn dev
```
