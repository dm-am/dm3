# Авторизация DM3 (RBAC)

> **User Story:** "Какие роли? Кто что может? Как работают права?"

> **Код:** `src/DM.Domain.*/Authorization/`

---

## Иерархия ролей

```
Admin (5)              Полный доступ
    │
SeniorModerator (4)    Бан пользователей, создание опросов, утверждение отзывов
    │
Moderator (3)          Редактирование/удаление чужого контента, удаление отзывов
    │
Mentor (2)             Премодерация игр новичков, помощь новым игрокам
    │
RegularUser (1)        Базовый доступ
    │
Guest (0)              Только чтение публичного контента
```

**Файл:** `src/DM.Infrastructure.Core/Dto/Enums/UserRole.cs`

---

## Специальные статусы

### Honorary (Почетный гоблин)

| Аспект | Статус |
|--------|--------|
| **Определение** | `User.IsHonorary` (bool) |
| **Отображение** | Бейдж на профиле |
| **Привилегии** | Отсутствуют (только косметика) |
| **Назначение** | Не реализовано |

### Newbie (Новичок)

| Условие | `QuantityRating < 100` (менее 100 постов) |
|---------|-------------------------------------------|
| **Проверка** | `GeneralUser.IsNewbie`, `ReviewEligibilityService.IsNewbie()` |

---

## Ограничения по статусам

### 1. Незавершенные регистрации (PendingRegistrations)

Email не подтвержден — пользователь еще не существует в таблице `Users`:

| Действие | Файл |
|----------|------|
| Войти в систему | `AuthenticationService.cs` — pending check ДО throttling |
| Быть найдены в поиске | Невозможно (нет в `Users`) |

**Ответ API:** `HTTP 400` с `_pendingActivation: true` для показа ссылки на повторную отправку письма.

### 2. Новички (< 100 постов)

Порог настраивается через `ProbationConfiguration.NewbiePostThreshold` (по умолчанию 100).

| Ограничение | Описание | Файл |
|-------------|----------|------|
| Премодерация игр | Новые игры требуют одобрения ментора | `GameCreationValidator.cs` |
| Отзывы на пользователей | Нельзя создавать | `ReviewService.cs` |
| Отзывы на игры | Нельзя создавать | `ReviewService.cs` |
| Отзывы на посты | Только нейтральные (без рейтинга) | `ReviewService.cs` |

### 3. Общие ограничения

| Ограничение | Условие | Файл |
|-------------|---------|------|
| Редактирование ревью | 24 часа с момента создания | `ReviewService.cs` |
| Кулдаун ревью постов | 3 дня на игру | `ReviewService.cs` |

---

## Матрица прав по ролям

### Администрирование

| Действие | Guest | User | Mentor | Mod | SeniorMod | Admin |
|----------|-------|------|--------|-----|-----------|-------|
| Администрирование форумов | - | - | - | - | - | ✓ |
| Override 24-часового лимита ревью | - | - | - | - | - | ✓ |

### Модерация

| Действие | Guest | User | Mentor | Mod | SeniorMod | Admin |
|----------|-------|------|--------|-----|-----------|-------|
| Редактирование/удаление тем форума | - | - | - | ✓ | ✓ | ✓ |
| Редактирование/удаление комментариев (форум/игры) | - | - | - | ✓ | ✓ | ✓ |
| Редактирование/удаление сообщений (без лимита) | - | - | - | ✓ | ✓ | ✓ |
| Модерация профилей пользователей (Info) | - | - | - | - | ✓ | ✓ |
| Удаление user/game ревью | - | - | - | - | ✓ | ✓ |
| Создание опросов | - | - | - | - | ✓ | ✓ |
| Утверждение platform ревью | - | - | - | - | ✓ | ✓ |
| Создание global chat events | - | - | - | - | ✓ | ✓ |
| Создание platform ревью | - | - | - | - | ✓ | ✓ |

### Менторство

| Действие | Guest | User | Mentor | Mod | SeniorMod | Admin |
|----------|-------|------|--------|-----|-----------|-------|
| Премодерация игр (взять на ревью) | - | - | ✓ | ✓ | ✓ | ✓ |
| Выпуск игр из премодерации | - | - | ✓ | ✓ | ✓ | ✓ |

### Базовые действия

| Действие | Guest | User | Mentor | Mod | SeniorMod | Admin |
|----------|-------|------|--------|-----|-----------|-------|
| Чтение публичного контента | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Создание игр | - | ✓ | ✓ | ✓ | ✓ | ✓ |
| Создание персонажей | - | ✓ | ✓ | ✓ | ✓ | ✓ |
| Лайки (нельзя свой контент) | - | ✓ | ✓ | ✓ | ✓ | ✓ |
| Редактирование своего контента | - | ✓ | ✓ | ✓ | ✓ | ✓ |

---

## Игровые права

### GameRole (simple enum)

```csharp
None = 0           // Нет участия
Reader = 1         // Подписчик (из Subscriptions)
Applicant = 2      // Заявка на рассмотрении (Character pending)
Player = 3         // Игрок с активным персонажем
Mentor = 4         // Ментор игры (Game.MentorId)
Assistant = 5      // Ассистент (GameAssistants table)
Master = 6         // Мастер (Game.AuthorId)
```

> **Примечание:** PendingAssistant/PendingInvitation хранятся в Tokens table

**Helper:** `HasEditAccess()` = Master || Assistant

### Права мастера и ассистента

| Действие | Условие |
|----------|---------|
| Редактирование игры | Master или Assistant |
| Удаление игры | Master или SeniorMod+ |
| Создание/удаление комнат | Master или Assistant |
| Принятие/отклонение персонажей | Master или Assistant |
| Управление NPC | Master или Assistant |
| Приглашение игроков/читателей | Master или Assistant |
| Приглашение ассистента | Master only |
| Удаление ассистента | Master only |
| Редактирование чужих постов | (Master или Assistant) + (NPC или CharacterAccessPolicy.PostEditAllowed) |

### Права игрока

| Действие | Условие |
|----------|---------|
| Создание персонажа | Игра активна + рекрутинг открыт |
| Редактирование персонажа | Игра активна + владелец персонажа |
| Удаление персонажа | Игра активна + владелец |
| Создание постов | Владелец персонажа в комнате |
| Выход из игры | Персонаж активен |

---

## Форумные права

### BoardAccessPolicy (bitmap)

```csharp
None            = 0       // Нет доступа
Administrator   = 1 << 0  // 1
SeniorModerator = 1 << 1  // 2
Moderator       = 1 << 2  // 4
Mentor          = 1 << 3  // 8
RegularUser     = 1 << 5  // 32
Guest           = 1 << 6  // 64
```

### Права на форуме

| Действие | Условие |
|----------|---------|
| Создание темы | `(Board.CreateTopicPolicy & userPolicy) != None` |
| Комментирование | Аутентифицирован + тема не закрыта |
| Редактирование темы | Автор (если не закрыта) / Модератор доски / Admin |
| Лайк темы/комментария | Аутентифицирован + не свой контент |

---

## Intention Pattern

### Использование

```csharp
// Проверка намерения (без контекста)
_intentionManager.ThrowIfForbidden(GameIntention.Create);

// Проверка намерения (с контекстом)
_intentionManager.ThrowIfForbidden(GameIntention.Edit, game);
```

**Пример резолвера:**
```csharp
public bool IsAllowed(AuthenticatedUser user, ReviewIntention intention)
{
    return intention switch
    {
        ReviewIntention.Create => user.Role >= UserRole.SeniorModerator,
        _ => false
    };
}
```

**Иерархическая проверка роли:**
```csharp
// Admin >= SeniorModerator >= Moderator >= Mentor >= RegularUser >= Guest
if (user.Role >= UserRole.Mentor)
{
    // Mentor и выше
}
```

### IntentionResolver'ы

| Резолвер | Файл | Модуль |
|----------|------|--------|
| UserIntentionResolver | `DM.Domain.Personal/Authorization/` | Профили |
| ReviewIntentionResolver | `DM.Domain.Community/Authorization/` | Отзывы |
| PollIntentionResolver | `DM.Domain.Community/Authorization/` | Опросы |
| ChatIntentionResolver | `DM.Domain.Messaging/Authorization/` | Диалоги |
| MessageIntentionResolver | `DM.Domain.Messaging/Authorization/` | Сообщения |
| GlobalChatEventIntentionResolver | `DM.Domain.Messaging/Authorization/` | Чат-события |
| GameIntentionResolver | `DM.Domain.Game/Authorization/` | Игры |
| CharacterIntentionResolver | `DM.Domain.Game/Authorization/` | Персонажи |
| RoomIntentionResolver | `DM.Domain.Game/Authorization/` | Комнаты |
| PostIntentionResolver | `DM.Domain.Game/Authorization/` | Посты |
| CommentIntentionResolver | `DM.Domain.Game/Authorization/` | Комментарии (игры) |
| AttributeSchemaIntentionResolver | `DM.Domain.Game/Authorization/` | Схемы атрибутов |
| ForumIntentionResolver | `DM.Domain.Forum/Authorization/` | Форум/доски |
| TopicIntentionResolver | `DM.Domain.Forum/Authorization/` | Темы |
| CommentIntentionResolver | `DM.Domain.Forum/Authorization/` | Комментарии (форум) |

---

## Конфигурация

### Временные лимиты

| Параметр | Значение | Конфигурация |
|----------|----------|--------------|
| Окно редактирования ревью | 24 часа | Константа в `ReviewEligibilityService.cs` |
| Кулдаун post review (по игре) | 3 дня | Константа в `ReviewEligibilityService.cs` |
| Порог новичка | 100 постов | `ProbationConfiguration.NewbiePostThreshold` |
| Лимит редактирования сообщений | 15 минут | `MessagingConfiguration.EditTimeoutMinutes` |
| Срок активационного токена | 48 часов | `TokenConfiguration.ActivationTokenLifetimeHours` |

---

## Ссылки

- [AUTHENTICATION.md](./AUTHENTICATION.md) — как работает вход
- [SYSTEM.md](./SYSTEM.md) — архитектура системы
- [SECURITY.md](../conventions/SECURITY.md) — требования безопасности
