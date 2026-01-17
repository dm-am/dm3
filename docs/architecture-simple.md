# DM3 — Полное описание архитектуры

> Этот документ объясняет как работает проект простым языком.

---

# Часть 1: Что есть в проекте

## 1. Общая картина

```
┌─────────────────────────────────────────────────────────────────┐
│                        ПОЛЬЗОВАТЕЛЬ                              │
│                    (браузер / мобильное приложение)              │
└─────────────────────────────┬───────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                     FRONTEND (Vue 3)                             │
│                     http://localhost:5173                        │
│                                                                  │
│  • 9 Pinia stores (состояние приложения)                        │
│  • 19 маршрутов (страницы)                                      │
│  • 5 API-модулей (запросы к серверу)                            │
└─────────────────────────────┬───────────────────────────────────┘
                              │ HTTP запросы
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                      BACKEND API                                 │
│                   http://localhost:5051                          │
│                                                                  │
│  • 19 контроллеров                                              │
│  • 161 API endpoint                                             │
│  • Авторизация через токены                                     │
└────────┬──────────────┬──────────────┬─────────────┬────────────┘
         │              │              │             │
         ▼              ▼              ▼             ▼
┌──────────────┐ ┌────────────┐ ┌───────────┐ ┌───────────┐
│ PostgreSQL   │ │  MongoDB   │ │ RabbitMQ  │ │  MinIO    │
│ (основные    │ │ (счётчики, │ │ (очередь  │ │ (файлы,   │
│  данные)     │ │  сессии)   │ │  событий) │ │  картинки)│
└──────────────┘ └────────────┘ └─────┬─────┘ └───────────┘
                                      │
         ┌────────────────────────────┼────────────────────────────┐
         │                            │                            │
         ▼                            ▼                            ▼
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

## 2. Backend проекты (18 штук)

### Главные проекты

| Проект | Что делает |
|--------|-----------|
| **DM.Web.API** | REST API — точка входа для всех запросов |
| **DM.Web.Core** | Middleware (аутентификация, ошибки, CORS) |
| **DM.Services.DataAccess** | Работа с базами данных (EF Core + MongoDB) |

### Бизнес-логика (по доменам)

| Проект | Что делает |
|--------|-----------|
| **DM.Services.Authentication** | Логин, сессии, токены |
| **DM.Services.Community** | Пользователи, опросы, отзывы, чат |
| **DM.Services.Forum** | Форумы, топики, комментарии |
| **DM.Services.Gaming** | Игры, комнаты, персонажи, посты |
| **DM.Services.Common** | Общее: авторизация, счётчики непрочитанного |

### Инфраструктура

| Проект | Что делает |
|--------|-----------|
| **DM.Services.Core** | Логирование, correlation tokens, время |
| **DM.Services.MessageQueuing** | Отправка событий в RabbitMQ |
| **DM.Services.Uploading** | Загрузка файлов в MinIO |
| **DM.Services.Notifications** | Создание уведомлений |
| **DM.Services.Mail.Sender** | Отправка email |
| **DM.Services.Mail.Rendering** | Шаблоны email |
| **DM.Services.Search** | gRPC клиент для поиска |

### Background consumers (фоновые сервисы)

| Проект | Порт | Что делает |
|--------|------|-----------|
| **Notifications.Consumer** | 5053 | Слушает события → создаёт уведомления |
| **Search.Consumer** | 5052 | Слушает события → индексирует в OpenSearch |
| **Mail.Sender.Consumer** | 5054 | Слушает очередь → отправляет email |

---

## 3. API контроллеры (19 штук, 161 endpoint)

### Account (10 endpoints)

| Метод | URL | Что делает |
|-------|-----|-----------|
| POST | `/v1/account` | Регистрация |
| PUT | `/v1/account/{token}` | Активация аккаунта |
| GET | `/v1/account` | Получить текущего пользователя |
| POST | `/v1/account/password` | Запросить сброс пароля |
| PUT | `/v1/account/password` | Изменить пароль |
| PUT | `/v1/account/email` | Изменить email |
| POST | `/v1/account/login` | Войти |
| DELETE | `/v1/account/login` | Выйти |
| DELETE | `/v1/account/login/all` | Выйти со всех устройств |
| GET | `/v1/search` | Поиск |

### Community (17 endpoints)

| Метод | URL | Что делает |
|-------|-----|-----------|
| GET | `/v1/users` | Список пользователей |
| GET | `/v1/users/{login}` | Профиль пользователя |
| GET | `/v1/users/{login}/details` | Детали профиля |
| PATCH | `/v1/users/{login}/details` | Обновить профиль |
| POST | `/v1/users/{login}/uploads` | Загрузить аватар |
| GET | `/v1/polls` | Список опросов |
| POST | `/v1/polls/global` | Создать опрос |
| GET | `/v1/polls/{id}` | Получить опрос |
| PATCH | `/v1/polls/{id}` | Обновить опрос |
| DELETE | `/v1/polls/{id}` | Удалить опрос |
| POST | `/v1/polls/{id}/vote` | Голосовать |
| DELETE | `/v1/polls/{id}/vote` | Отменить голос |
| GET | `/v1/websitereviews` | Список отзывов |
| POST | `/v1/websitereviews` | Создать отзыв |
| GET | `/v1/websitereviews/{id}` | Получить отзыв |
| PATCH | `/v1/websitereviews/{id}` | Обновить отзыв |
| DELETE | `/v1/websitereviews/{id}` | Удалить отзыв |

### Forums — Boards (21 endpoint)

| Метод | URL | Что делает |
|-------|-----|-----------|
| GET | `/v1/boards` | Список форумов |
| GET | `/v1/boards/{id}` | Получить форум |
| GET | `/v1/boards/{id}/moderators` | Модераторы форума |
| DELETE | `/v1/boards/{id}/comments/unread` | Пометить всё прочитанным |
| GET | `/v1/boards/{id}/topics` | Топики форума |
| POST | `/v1/boards/{id}/topics` | Создать топик |
| GET | `/v1/topics/{id}` | Получить топик |
| PATCH | `/v1/topics/{id}` | Обновить топик |
| DELETE | `/v1/topics/{id}` | Удалить топик |
| POST | `/v1/topics/{id}/likes` | Лайкнуть топик |
| DELETE | `/v1/topics/{id}/likes` | Убрать лайк |
| DELETE | `/v1/topics/{id}/comments/unread` | Пометить топик прочитанным |
| GET | `/v1/topics/{id}/comments` | Комментарии топика |
| POST | `/v1/topics/{id}/comments` | Создать комментарий |
| GET | `/v1/forum/comments/{id}` | Получить комментарий |
| PATCH | `/v1/forum/comments/{id}` | Обновить комментарий |
| DELETE | `/v1/forum/comments/{id}` | Удалить комментарий |
| POST | `/v1/forum/comments/{id}/likes` | Лайкнуть комментарий |
| DELETE | `/v1/forum/comments/{id}/likes` | Убрать лайк |

### Gaming (78 endpoints)

**Игры:**
| Метод | URL | Что делает |
|-------|-----|-----------|
| GET | `/v1/games` | Список игр (с фильтрами) |
| GET | `/v1/games/owned` | Мои игры |
| GET | `/v1/games/popular` | Популярные игры |
| GET | `/v1/games/tags` | Теги игр |
| GET | `/v1/games/{id}` | Получить игру |
| GET | `/v1/games/{id}/details` | Детали игры |
| POST | `/v1/games` | Создать игру |
| PATCH | `/v1/games/{id}/details` | Обновить игру |
| DELETE | `/v1/games/{id}` | Удалить игру |

**Читатели и чёрный список:**
| Метод | URL | Что делает |
|-------|-----|-----------|
| GET | `/v1/games/{id}/readers` | Читатели игры |
| POST | `/v1/games/{id}/readers` | Подписаться на игру |
| DELETE | `/v1/games/{id}/readers` | Отписаться |
| GET | `/v1/games/{id}/blacklist` | Чёрный список |
| POST | `/v1/games/{id}/blacklist` | Добавить в ЧС |
| DELETE | `/v1/games/{id}/blacklist/{login}` | Убрать из ЧС |

**Персонажи:**
| Метод | URL | Что делает |
|-------|-----|-----------|
| GET | `/v1/games/{id}/characters` | Персонажи игры |
| POST | `/v1/games/{id}/characters` | Создать персонажа |
| GET | `/v1/characters/{id}` | Получить персонажа |
| PATCH | `/v1/characters/{id}/details` | Обновить персонажа |
| DELETE | `/v1/characters/{id}` | Удалить персонажа |

**Комнаты:**
| Метод | URL | Что делает |
|-------|-----|-----------|
| GET | `/v1/games/{id}/rooms` | Комнаты игры |
| POST | `/v1/games/{id}/rooms` | Создать комнату |
| GET | `/v1/rooms/{id}` | Получить комнату |
| PATCH | `/v1/rooms/{id}` | Обновить комнату |
| DELETE | `/v1/rooms/{id}` | Удалить комнату |

**Посты:**
| Метод | URL | Что делает |
|-------|-----|-----------|
| GET | `/v1/rooms/{id}/posts` | Посты комнаты |
| POST | `/v1/rooms/{id}/posts` | Создать пост |
| GET | `/v1/posts/{id}` | Получить пост |
| PATCH | `/v1/posts/{id}` | Обновить пост |
| DELETE | `/v1/posts/{id}` | Удалить пост |

**Схемы атрибутов:**
| Метод | URL | Что делает |
|-------|-----|-----------|
| GET | `/v1/schemas` | Список схем |
| POST | `/v1/schemas` | Создать схему |
| GET | `/v1/schemas/{id}` | Получить схему |
| PATCH | `/v1/schemas/{id}` | Обновить схему |
| DELETE | `/v1/schemas/{id}` | Удалить схему |

### Messaging (15 endpoints)

| Метод | URL | Что делает |
|-------|-----|-----------|
| GET | `/v1/conversations` | Список диалогов |
| GET | `/v1/conversations/direct/{login}` | Диалог с пользователем (1-на-1) |
| GET | `/v1/conversations/{id}` | Получить диалог |
| DELETE | `/v1/conversations/{id}/messages/unread` | Пометить прочитанным |
| GET | `/v1/conversations/{id}/messages` | Сообщения диалога |
| POST | `/v1/conversations/{id}/messages` | Отправить сообщение |
| GET | `/v1/messages/{id}` | Получить сообщение |
| PATCH | `/v1/messages/{id}` | Обновить сообщение |
| DELETE | `/v1/messages/{id}` | Удалить сообщение |
| POST | `/v1/messages/{id}/likes` | Лайкнуть сообщение |
| DELETE | `/v1/messages/{id}/likes` | Убрать лайк с сообщения |
| GET | `/v1/globalchat/messages` | Сообщения чата |
| POST | `/v1/globalchat/messages` | Отправить в чат |
| GET | `/v1/globalchat/messages/{id}` | Получить сообщение чата |

---

## 4. Frontend (Vue 3 + TypeScript + Pinia)

### Pinia stores (9 штук)

| Store | Что хранит | Кэширование |
|-------|-----------|-------------|
| **user** | Текущий пользователь, авторизация | localStorage |
| **ui** | Тема оформления (5 тем) | — |
| **boards** | Форумы, топики, комментарии, новости | 60 сек |
| **games** | 6 списков игр (свои, активные, и т.д.) | 60 сек |
| **community** | Список пользователей, профили | — |
| **polls** | Опросы, голосование | — |
| **websiteReviews** | Отзывы о сайте | — |
| **chat** | Сообщения глобального чата | — |

### Маршруты (19 страниц)

| URL | Страница |
|-----|----------|
| `/` | Главная (новости) |
| `/about` | О проекте (отзывы) |
| `/rules` | Правила |
| `/chat` | Глобальный чат |
| `/community` | Список пользователей |
| `/profile/:login` | Профиль пользователя |
| `/profile/:login/games` | Игры пользователя |
| `/profile/:login/characters` | Персонажи пользователя |
| `/profile/:login/settings` | Настройки профиля |
| `/forum` | Список форумов |
| `/forum/:id` | Топики форума |
| `/topic/:id` | Комментарии топика |
| `/games` | Все игры |
| `/blogs` | Блоги |
| `/moderation` | Модерация |

### API модули (6 файлов)

| Файл | Что вызывает |
|------|-------------|
| **accountApi.ts** | Регистрация, логин, профиль |
| **communityApi.ts** | Пользователи, опросы, отзывы |
| **forumApi.ts** | Форумы, топики, комментарии |
| **gamingApi.ts** | Игры, персонажи, комнаты |
| **chatApi.ts** | Глобальный чат |
| **messagingApi.ts** | Личные сообщения, диалоги, лайки |

---

## 5. Базы данных

### PostgreSQL (основная БД)

**37 таблиц, сгруппированных по доменам:**

| Домен | Таблицы |
|-------|---------|
| **Users** | User, Token |
| **Common** | Comment, Like, Review, Tag, TagGroup, Upload |
| **Forum** | Forum (Boards), ForumTopic, ForumModerator |
| **Games** | Game, GameTag, Character, CharacterAttribute, Room, Post, PendingPost, Vote, Reader, BlackListLink, RoomClaim |
| **Messaging** | Conversation, UserConversationLink, Message, ChatMessage |
| **Admin** | Report, Warning, Ban |

### MongoDB (вспомогательная БД)

| Коллекция | Что хранит |
|-----------|-----------|
| **Polls** | Опросы с вариантами ответов |
| **UnreadCounters** | Счётчики непрочитанного |
| **UserSettings** | Настройки пользователей |
| **UserSessions** | Сессии авторизации |
| **AttributeSchemata** | Схемы атрибутов персонажей |
| **Dice** | Результаты бросков кубиков |
| **RealtimeNotifications** | Push-уведомления |

---

## 6. Docker сервисы (14 штук)

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
| Migration | dm-migration | — | Миграции БД при старте |
| Notifications | dm-notifications-consumer | 5053 | Генерация уведомлений |
| Search | dm-search-engine-consumer | 5052 | Индексация в OpenSearch |
| Mail | dm-email-sender-consumer | 5054 | Отправка email |

---

# Часть 2: Как это работает

## 1. Путь запроса (на примере создания топика)

```
1. ПОЛЬЗОВАТЕЛЬ
   Нажимает "Создать топик" в браузере

2. FRONTEND (Vue)
   → stores/boards.ts вызывает forumApi.createTopic()
   → api/requests/forumApi.ts делает POST запрос
   → Добавляет токен из localStorage в заголовок

3. HTTP ЗАПРОС
   POST http://localhost:5051/v1/boards/{id}/topics
   Headers: { "x-dm-auth-token": "..." }
   Body: { "title": "...", "text": "..." }

4. MIDDLEWARE (по порядку)
   → CorrelationMiddleware: добавляет ID запроса для логов
   → ErrorHandlingMiddleware: ловит ошибки
   → AuthenticationMiddleware: проверяет токен, ставит identity

5. CONTROLLER
   TopicController.PostBoardTopic()
   → Проверяет [AuthenticationRequired]
   → Вызывает topicApiService.Create()

6. API SERVICE
   TopicApiService.Create()
   → Маппит DTO через AutoMapper
   → Вызывает topicCreatingService.CreateTopic()

7. BUSINESS SERVICE
   TopicCreatingService.CreateTopic()
   → Валидирует данные (FluentValidation)
   → Проверяет права (IntentionManager)
   → Создаёт топик через repository
   → Создаёт счётчик непрочитанного (MongoDB)
   → Отправляет событие в RabbitMQ

8. REPOSITORY
   TopicCreatingRepository.Create()
   → Добавляет в DbContext
   → SaveChangesAsync() → PostgreSQL

9. СОБЫТИЯ (асинхронно)
   RabbitMQ получает событие "forum.topic.created"
   → Search Consumer индексирует в OpenSearch
   → Notifications Consumer создаёт уведомления

10. ОТВЕТ
    HTTP 201 Created
    Body: { "resource": { "id": "...", "title": "...", ... } }

11. FRONTEND
    → Получает ответ
    → Обновляет store
    → Перерисовывает UI
```

---

## 2. Аутентификация (пошагово)

### Вход в систему

```
1. Пользователь вводит логин/пароль
2. Frontend отправляет POST /v1/account/login
3. Backend:
   → Ищет пользователя в БД
   → Проверяет: активирован? не забанен? пароль верный?
   → Создаёт сессию в MongoDB
   → Шифрует {userId, sessionId} → токен
4. Возвращает токен в заголовке x-dm-auth-token
5. Frontend сохраняет токен в localStorage
```

### Проверка токена при каждом запросе

```
1. Frontend добавляет токен в заголовок запроса
2. AuthenticationMiddleware:
   → Извлекает токен из заголовка
   → Расшифровывает → получает userId, sessionId
   → Проверяет сессию в MongoDB
   → Загружает пользователя из PostgreSQL
   → Ставит identity в IIdentityProvider
3. Контроллер получает доступ к текущему пользователю
```

---

## 3. Система событий (RabbitMQ)

### Кто отправляет события

```csharp
// В любом сервисе после успешного действия:
await invokedEventProducer.Send(EventType.NewForumTopic, topic.Id);
```

### Типы событий (примеры)

| EventType | Routing Key | Кто слушает |
|-----------|------------|-------------|
| ActivatedUser | community.user.activated | Search, Notifications |
| NewMessage | messaging.message.created | Notifications |
| LikedMessage | messaging.message.liked | Notifications |
| NewForumTopic | forum.topic.created | Search, Notifications |
| NewForumComment | forum.comment.created | Search, Notifications |
| DeletedForumTopic | forum.topic.deleted | Search |
| NewCharacter | game.character.created | Notifications |

### Что делают consumers

| Consumer | Слушает | Делает |
|----------|---------|--------|
| **Search** | forum.*, game.* | Индексирует в OpenSearch |
| **Notifications** | forum.*, game.character.* | Создаёт уведомления в MongoDB |
| **Mail** | dm.mail.sending | Отправляет email через SMTP |

---

## 4. Счётчики непрочитанного

### Как работают

```
MongoDB коллекция "UnreadCounters":
{
  UserId: "...",      // Чей счётчик (или Empty для всех)
  EntityId: "...",    // ID топика/комментария
  ParentId: "...",    // ID форума (для агрегации)
  Counter: 5,         // Сколько непрочитанных
  LastRead: "...",    // Когда последний раз читал
  EntryType: "Message"
}
```

### Жизненный цикл

```
1. СОЗДАНИЕ ТОПИКА
   → Создаётся счётчик с Counter=0

2. НОВЫЙ КОММЕНТАРИЙ
   → Counter++ для всех кто не в этом топике

3. ПОЛЬЗОВАТЕЛЬ ОТКРЫЛ ТОПИК
   → Counter=0 для этого пользователя

4. УДАЛЕНИЕ КОММЕНТАРИЯ
   → Counter-- для тех кто не читал
```

---

## 5. Авторизация (права доступа)

### Паттерн Intention

```csharp
// 1. Определяем намерения
public enum TopicIntention
{
    Create,     // Создать топик
    Edit,       // Редактировать
    Delete,     // Удалить
    Close       // Закрыть
}

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
// Если нет прав → выбрасывает исключение → 403 Forbidden
```

---

# Часть 3: Изменения vs старая версия

## Переименования

| Было | Стало | Где |
|------|-------|-----|
| `Fora` | `Boards` | Папки, классы, таблицы |
| `Reviews` | `WebsiteReviews` | Контроллеры, stores, API |
| `/v1/reviews` | `/v1/websitereviews` | API endpoints |
| `/v1/games/own` | `/v1/games/owned` | API endpoint |
| `/v1/conversations/visavi/{login}` | `/v1/conversations/direct/{login}` | API endpoint |
| `fora.ts` | `boards.ts` | Frontend store |
| `reviews.ts` | `websiteReviews.ts` | Frontend store |

## Новые файлы

### Backend
- `BoardController.cs` — управление форумами
- `GlobalChatController.cs` — глобальный чат
- `ConversationController.cs` — личные сообщения
- `MessageIntention.cs` — намерения для сообщений (Edit, Delete, Like)
- `MessageIntentionResolver.cs` — проверка прав на сообщения
- `MessageLikeService.cs` — лайки личных сообщений
- `MessageUpdatingService.cs` — редактирование сообщений
- 4 новых миграции (денормализация Forum)

### Frontend
- `chat.ts` — store для чата
- `ChatPage.vue` — страница чата
- `messagingApi.ts` — API для личных сообщений
- `api/models/messaging/index.ts` — типы Conversation, Message
- `BlogsPage.vue` — страница блогов
- `GamesPage.vue` — страница игр
- `ModerationPage.vue` — модерация
- `ForumIndexPage.vue` — список форумов
- `UserAutocomplete.vue` — автокомплит пользователей
- `PasswordReset.vue` — сброс пароля

## Изменения в БД

### Новые поля в Forum (Boards)

```sql
-- Денормализация для производительности
TopicsCount INT DEFAULT 0        -- Кэш количества топиков
CommentsCount INT DEFAULT 0      -- Кэш количества комментариев
LastCommentId GUID              -- FK на последний комментарий
LastCommentTopicId GUID         -- Топик последнего комментария
LastCommentAuthorId GUID        -- Автор последнего комментария
LastCommentDate TIMESTAMP       -- Дата последнего комментария
```

### Новые поля в ForumTopic

```sql
LastUpdateDate TIMESTAMP        -- Дата последнего обновления
```

---

# Часть 4: Как запустить

## Быстрый старт

```bash
# 1. Запустить Docker Desktop

# 2. Поднять все контейнеры
cd "D:\Code Projects\dm3"
docker compose -f docker/docker-compose.yml up -d

# 3. Запустить frontend
cd frontend/DM.Web.Modern.Temp
npm run dev

# 4. Открыть в браузере
# Frontend: http://localhost:5173
# API: http://localhost:5051
# RabbitMQ UI: http://localhost:15672 (guest/guest)
# MinIO UI: http://localhost:9001 (minio/miniokey)
# MailHog UI: http://localhost:5025
# Jaeger UI: http://localhost:16686
```

## Проверка здоровья

```bash
# API работает?
curl http://localhost:5051/v1/boards

# Все контейнеры запущены?
docker ps --format "table {{.Names}}\t{{.Status}}"
```

---

# Часть 5: Полезные команды

## Docker

```bash
# Запустить всё
docker compose -f docker/docker-compose.yml up -d

# Остановить всё
docker compose -f docker/docker-compose.yml down

# Пересобрать API
docker compose -f docker/docker-compose.yml build dmapi --no-cache

# Логи API
docker logs dm-api --tail 100 -f

# Перезапустить API
docker compose -f docker/docker-compose.yml restart dmapi
```

## Frontend

```bash
cd frontend/DM.Web.Modern.Temp

# Запустить dev сервер
npm run dev

# Сборка
npm run build

# Проверка типов
npm run type-check

# Линтер
npm run lint
```

## Тестирование API

```bash
# Получить список форумов
curl http://localhost:5051/v1/boards

# Получить игры
curl http://localhost:5051/v1/games?statuses=Active

# С авторизацией
curl -H "X-Dm-Auth-Token: YOUR_TOKEN" http://localhost:5051/v1/games/owned
```
