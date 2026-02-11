# Role-Based Access Control (RBAC)

## Overview

DM3 использует иерархическую систему ролей с дополнительными статусами и ограничениями.

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

**Файл:** `src/DM.Services.Core/Dto/Enums/UserRole.cs`

---

## Специальные статусы

### Honorary (Почётный гоблин)

| Аспект | Статус |
|--------|--------|
| **Определение** | `User.IsHonorary` (bool) |
| **Отображение** | Бейдж на профиле |
| **Привилегии** | Отсутствуют (только косметика) |
| **Назначение** | Не реализовано |

**TODO:** Реализовать логику назначения и привилегии для почётных пользователей.

### Newbie (Новичок)

| Условие | `QuantityRating < 100` (менее 100 постов) |
|---------|-------------------------------------------|
| **Проверка** | `GeneralUser.IsNewbie`, `ReviewEligibilityService.IsNewbie()` |

---

## Ограничения по статусам

### 1. Незавершённые регистрации (PendingRegistrations)

Email не подтверждён — пользователь ещё не существует в таблице `Users`:

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
| Отзывы на пользователей | Нельзя создавать | `ReviewCreatingService.cs` |
| Отзывы на игры | Нельзя создавать | `ReviewCreatingService.cs` |
| Отзывы на посты | Только нейтральные (без рейтинга) | `IReviewEligibilityService.cs` |

### 3. Общие ограничения

| Ограничение | Условие | Файл |
|-------------|---------|------|
| Редактирование ревью | 24 часа с момента создания | `ReviewEligibilityService.cs` |
| Кулдаун ревью постов | 3 дня на игру | `ReviewEligibilityService.cs` |

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

### GameParticipation (флаги)

```csharp
None = 0           // Нет участия
Reader = 1         // Подписчик
Player = 2         // Игрок с активным персонажем
Moderator = 4      // Ментор игры
PendingAssistant = 8
Authority = 16     // Мастер или активный ассистент
Owner = 32         // Создатель игры (мастер)
```

### Права мастера/ассистента (Authority)

| Действие | Условие |
|----------|---------|
| Редактирование игры | Authority |
| Удаление игры | Owner или SeniorMod+ |
| Создание/удаление комнат | Authority |
| Принятие/отклонение персонажей | Authority |
| Управление NPC | Authority |
| Приглашение игроков/читателей | Authority |
| Редактирование чужих постов | Authority + (NPC или CharacterAccessPolicy.PostEditAllowed) |

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

## IntentionResolver'ы

| Резолвер | Файл | Модуль |
|----------|------|--------|
| UserIntentionResolver | `Community/BusinessProcesses/Users/` | Профили |
| ReviewIntentionResolver | `Community/BusinessProcesses/Reviews/` | Отзывы |
| PollIntentionResolver | `Community/BusinessProcesses/Polls/` | Опросы |
| ConversationIntentionResolver | `Community/BusinessProcesses/Messaging/` | Диалоги |
| MessageIntentionResolver | `Community/BusinessProcesses/Messaging/` | Сообщения |
| GlobalChatEventIntentionResolver | `Community/BusinessProcesses/Messaging/GlobalChatEvents/` | Чат-события |
| GameIntentionResolver | `Game/Authorization/` | Игры |
| CharacterIntentionResolver | `Game/Authorization/` | Персонажи |
| RoomIntentionResolver | `Game/Authorization/` | Комнаты |
| PostIntentionResolver | `Game/Authorization/` | Посты |
| CommentIntentionResolver | `Game/Authorization/` | Комментарии (игры) |
| AttributeSchemaIntentionResolver | `Game/Authorization/` | Схемы атрибутов |
| ForumIntentionResolver | `Forum/Authorization/` | Форум/доски |
| TopicIntentionResolver | `Forum/Authorization/` | Темы |
| CommentIntentionResolver | `Forum/Authorization/` | Комментарии (форум) |

---

## Проблемы и рекомендации

### Выявленные проблемы

1. **Honorary (Почётный гоблин)**
   - Статус существует в БД, но не имеет привилегий
   - Нет UI/API для назначения статуса
   - **Рекомендация:** Определить привилегии или удалить поле

2. **Отсутствует система банов**
   - В БД есть таблицы `Ban`, `Warning`, но сервисы не реализованы
   - **Рекомендация:** Реализовать систему предупреждений и банов

3. **Нет админ-панели**
   - Управление ролями только через БД
   - **Рекомендация:** Добавить admin endpoints для управления пользователями

4. **Дублирование CommentIntentionResolver**
   - Одинаковая логика в Game и Forum модулях
   - **Рекомендация:** Вынести общую логику в Common

### Реализованные улучшения

1. **Like soft delete** — лайки имеют поле `IsRemoved` для GDPR compliance
2. **Review audit** — отслеживание редактора через `ModifiedByUserId`
3. **GlobalChatEvents** — система глобальных чат-событий с участниками

### Предложения по улучшению

1. **Добавить привилегии Honorary:**
   - Bypass премодерации игр
   - Особый бейдж в постах
   - Доступ к закрытым разделам форума

2. **Унифицировать систему прав:**
   - Создать централизованный Permission service
   - Добавить кэширование прав пользователя

3. **Добавить audit log:**
   - Логирование действий модераторов
   - История изменений ролей

4. **Расширить role-based restrictions:**
   - Rate limiting по ролям
   - Разные лимиты загрузки файлов

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

## Примеры использования

### Проверка намерения (без контекста)
```csharp
_intentionManager.ThrowIfForbidden(GameIntention.Create);
```

### Проверка намерения (с контекстом)
```csharp
_intentionManager.ThrowIfForbidden(GameIntention.Edit, game);
```

### Проверка в резолвере
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

### Иерархическая проверка роли
```csharp
// Admin >= SeniorModerator >= Moderator >= Mentor >= RegularUser >= Guest
if (user.Role >= UserRole.Mentor)
{
    // Mentor и выше
}
```

---

## Ссылки

- [README](../README.md) — Обзор документации
- [Архитектура](./OVERVIEW.md) — Общая архитектура
- [Аутентификация](./AUTHENTICATION.md) — BFF и сессии
- [Глоссарий](../reference/GLOSSARY.md) — Термины и определения

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
