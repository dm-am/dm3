# Безопасность DM3

> **Код:** `src/DM.Domain.Account/Features/Authentication/`, `src/DM.Domain.*/Authorization/`

---

## Часть 1: Аутентификация

### Обзор

| Компонент | Реализация |
|-----------|------------|
| **Pattern** | BFF (Backend-For-Frontend) |
| **Токен** | HttpOnly cookie `dm_session` |
| **Шифрование** | AES-256-GCM |
| **Хеширование** | PBKDF2-SHA256, 600K итераций |
| **CSRF защита** | SameSite=Strict |
| **Сессии** | MongoDB |

### Архитектура

```
Browser (Vue.js)
    │
    │ Cookie: dm_session (HttpOnly)
    ▼
DM.Web.API
    │
    ├─► ApiAuthenticationMiddleware
    │   └─► Расшифровка токена → userId, sessionId
    │
    └─► AuthenticationService
        └─► Валидация сессии и пользователя
              │
    ┌─────────┴─────────┐
    ▼                   ▼
PostgreSQL          MongoDB
(Users)             (Sessions)
```

**Файлы:**
- `src/DM.Web.API/Shared/Authentication/ApiCredentialsStorage.cs`
- `src/DM.Domain.Account/Features/Authentication/AuthenticationService.cs`

### Регистрация

#### Flow: Email → Имя пользователя

```
1. POST /v1/account {email, password}       → PendingRegistration (с TokenId)
2. Email со ссылкой                         → /activate/{token}
3. GET /v1/account/activate/{token}         → Показать форму выбора Login
4. POST /v1/account/activate {token, login} → Создать User, auto-login
```

#### Защита от сквоттинга

- Имя пользователя (Login) выбирается ПОСЛЕ верификации email
- Нельзя занять логин без подтверждённого email
- PendingRegistration хранит TokenId внутри (без FK на Tokens)
- Cleanup: 7 дней для PendingRegistration

#### Таблица PendingRegistrations

| Поле | Назначение |
|------|-----------|
| TokenId | Токен для email-ссылки |
| TokenCreatedUtc | Для проверки 48h expiry |
| CreatedUtc | Для cleanup (7 дней) |
| Email | Адрес для верификации |
| PasswordHash, Salt | Хеш пароля (PBKDF2) |

**Файлы:**
- `src/DM.Infrastructure.Persistence/Entities/Account/PendingRegistration.cs`
- `src/DM.Domain.Account/Features/Registration/RegistrationService.cs`
- `src/DM.Domain.Account/Features/Registration/ActivationService.cs`

### Параметры безопасности

#### Пароли

| Параметр | Значение |
|----------|----------|
| Алгоритм | PBKDF2-SHA256 |
| Итерации | 600,000 |
| Salt | 32 bytes |
| Hash | 32 bytes |

**Файл:** `src/DM.Domain.Account/Features/Security/SecurityManager.cs`

#### Токены

| Параметр | Значение |
|----------|----------|
| Алгоритм | AES-256-GCM |
| Key | 32 bytes |
| Nonce | 12 bytes |
| Tag | 16 bytes |

**Файл:** `src/DM.Domain.Account/Features/Security/AesGcmSymmetricCryptoService.cs`

#### Сессии

| Параметр | Значение |
|----------|----------|
| Длительность (Remember me ✓) | 1 год (8760 часов) |
| Длительность (Remember me ✗) | 24 часа |
| Автообновление | за 7 дней до истечения |
| Отслеживание активности | Каждую 1 минуту |

По умолчанию checkbox "Запомнить меня" включён — сессия на 1 год.

**Файлы:**
- Конфиг: `src/DM.Web.API/appsettings.json` → `AuthenticationConfiguration`
- Дефолты: `src/DM.Domain.Account/Configuration/AuthenticationConfiguration.cs`

#### Политика паролей

| Параметр | Значение |
|----------|----------|
| Минимум | 8 символов |
| Максимум | 128 символов |
| Заглавные | Не обязательно |
| Строчные | Не обязательно |
| Цифры | Не обязательно |
| Спецсимволы | Не обязательно |

NIST SP 800-63B-4 (2024): composition rules не требуются при сильном хешировании.

**Файл:** `src/DM.Domain.Account/Configuration/PasswordPolicyConfiguration.cs`

### Защита от атак

| Атака | Защита |
|-------|--------|
| XSS | HttpOnly cookies, CSP headers |
| CSRF | SameSite=Strict, CSRF middleware |
| Brute-force | Progressive delay + **блокировка** (15 попыток → 30 мин) |
| Timing | FixedTimeEquals |
| Token forgery | AES-GCM auth tag |
| Clickjacking | X-Frame-Options: DENY |
| MIME sniffing | X-Content-Type-Options: nosniff |
| SSRF | Блокировка private IP в BBCode URL |

### Security Headers

Middleware добавляет защитные заголовки:

| Header | Значение |
|--------|----------|
| X-Frame-Options | DENY |
| X-Content-Type-Options | nosniff |
| X-XSS-Protection | 1; mode=block |
| Referrer-Policy | strict-origin-when-cross-origin |
| Permissions-Policy | Минимальные разрешения |
| Content-Security-Policy | default-src 'self' |
| Strict-Transport-Security | max-age=31536000 (production) |

**Файл:** `src/DM.Web.API/Middleware/SecurityHeadersMiddleware.cs`

### Troubleshooting

#### 401 Unauthorized

1. Проверь `withCredentials: true` в axios/fetch
2. Сессия истекла — перелогиниться
3. Пользователь забанен/удалён

#### Cookie не устанавливается

1. HTTPS в production
2. Правильные CORS origins
3. `credentials: 'include'` в fetch

---

## Часть 2: Авторизация (RBAC)

### Иерархия ролей

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

### Специальные статусы

#### Honorary (Почетный гоблин)

| Аспект | Статус |
|--------|--------|
| **Определение** | `User.IsHonorary` (bool) |
| **Отображение** | Бейдж на профиле |
| **Привилегии** | Отсутствуют (только косметика) |
| **Назначение** | Не реализовано |

**TODO:** Реализовать логику назначения и привилегии для почетных пользователей.

#### Newbie (Новичок)

| Условие | `QuantityRating < 100` (менее 100 постов) |
|---------|-------------------------------------------|
| **Проверка** | `GeneralUser.IsNewbie`, `ReviewEligibilityService.IsNewbie()` |

### Ограничения по статусам

#### 1. Незавершенные регистрации (PendingRegistrations)

Email не подтвержден — пользователь еще не существует в таблице `Users`:

| Действие | Файл |
|----------|------|
| Войти в систему | `AuthenticationService.cs` — pending check ДО throttling |
| Быть найдены в поиске | Невозможно (нет в `Users`) |

**Ответ API:** `HTTP 400` с `_pendingActivation: true` для показа ссылки на повторную отправку письма.

#### 2. Новички (< 100 постов)

Порог настраивается через `ProbationConfiguration.NewbiePostThreshold` (по умолчанию 100).

| Ограничение | Описание | Файл |
|-------------|----------|------|
| Премодерация игр | Новые игры требуют одобрения ментора | `GameCreationValidator.cs` |
| Отзывы на пользователей | Нельзя создавать | `ReviewService.cs` |
| Отзывы на игры | Нельзя создавать | `ReviewService.cs` |
| Отзывы на посты | Только нейтральные (без рейтинга) | `ReviewService.cs` |

#### 3. Общие ограничения

| Ограничение | Условие | Файл |
|-------------|---------|------|
| Редактирование ревью | 24 часа с момента создания | `ReviewService.cs` |
| Кулдаун ревью постов | 3 дня на игру | `ReviewService.cs` |

### Матрица прав по ролям

#### Администрирование

| Действие | Guest | User | Mentor | Mod | SeniorMod | Admin |
|----------|-------|------|--------|-----|-----------|-------|
| Администрирование форумов | - | - | - | - | - | ✓ |
| Override 24-часового лимита ревью | - | - | - | - | - | ✓ |

#### Модерация

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

#### Менторство

| Действие | Guest | User | Mentor | Mod | SeniorMod | Admin |
|----------|-------|------|--------|-----|-----------|-------|
| Премодерация игр (взять на ревью) | - | - | ✓ | ✓ | ✓ | ✓ |
| Выпуск игр из премодерации | - | - | ✓ | ✓ | ✓ | ✓ |

#### Базовые действия

| Действие | Guest | User | Mentor | Mod | SeniorMod | Admin |
|----------|-------|------|--------|-----|-----------|-------|
| Чтение публичного контента | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| Создание игр | - | ✓ | ✓ | ✓ | ✓ | ✓ |
| Создание персонажей | - | ✓ | ✓ | ✓ | ✓ | ✓ |
| Лайки (нельзя свой контент) | - | ✓ | ✓ | ✓ | ✓ | ✓ |
| Редактирование своего контента | - | ✓ | ✓ | ✓ | ✓ | ✓ |

### Игровые права

#### GameRole (simple enum)

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

#### Права мастера и ассистента

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

#### Права игрока

| Действие | Условие |
|----------|---------|
| Создание персонажа | Игра активна + рекрутинг открыт |
| Редактирование персонажа | Игра активна + владелец персонажа |
| Удаление персонажа | Игра активна + владелец |
| Создание постов | Владелец персонажа в комнате |
| Выход из игры | Персонаж активен |

### Форумные права

#### BoardAccessPolicy (bitmap)

```csharp
None            = 0       // Нет доступа
Administrator   = 1 << 0  // 1
SeniorModerator = 1 << 1  // 2
Moderator       = 1 << 2  // 4
Mentor          = 1 << 3  // 8
RegularUser     = 1 << 5  // 32
Guest           = 1 << 6  // 64
```

#### Права на форуме

| Действие | Условие |
|----------|---------|
| Создание темы | `(Board.CreateTopicPolicy & userPolicy) != None` |
| Комментирование | Аутентифицирован + тема не закрыта |
| Редактирование темы | Автор (если не закрыта) / Модератор доски / Admin |
| Лайк темы/комментария | Аутентифицирован + не свой контент |

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

### Intention Pattern

**Пример:** `src/DM.Domain.Messaging/Authorization/ChatIntentionResolver.cs`

```csharp
_intentionManager.ThrowIfForbidden(ConversationIntention.CreateMessage, conversation);
```

### Примеры использования

#### Проверка намерения (без контекста)
```csharp
_intentionManager.ThrowIfForbidden(GameIntention.Create);
```

#### Проверка намерения (с контекстом)
```csharp
_intentionManager.ThrowIfForbidden(GameIntention.Edit, game);
```

#### Проверка в резолвере
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

#### Иерархическая проверка роли
```csharp
// Admin >= SeniorModerator >= Moderator >= Mentor >= RegularUser >= Guest
if (user.Role >= UserRole.Mentor)
{
    // Mentor и выше
}
```

### Конфигурация

#### Временные лимиты

| Параметр | Значение | Конфигурация |
|----------|----------|--------------|
| Окно редактирования ревью | 24 часа | Константа в `ReviewEligibilityService.cs` |
| Кулдаун post review (по игре) | 3 дня | Константа в `ReviewEligibilityService.cs` |
| Порог новичка | 100 постов | `ProbationConfiguration.NewbiePostThreshold` |
| Лимит редактирования сообщений | 15 минут | `MessagingConfiguration.EditTimeoutMinutes` |
| Срок активационного токена | 48 часов | `TokenConfiguration.ActivationTokenLifetimeHours` |

### Проблемы и рекомендации

#### Выявленные проблемы

1. **Honorary (Почетный гоблин)**
   - Статус существует в БД, но не имеет привилегий
   - Нет UI/API для назначения статуса
   - **Рекомендация:** Определить привилегии или удалить поле

2. **Система банов**
   - Реализованы: `BanService`, `WarningService` в `DM.Domain.Moderation/Features/Warnings/`
   - Таблицы БД: `Bans`, `Warnings`

3. **Нет админ-панели**
   - Управление ролями только через БД
   - **Рекомендация:** Добавить admin endpoints для управления пользователями

4. **Дублирование CommentIntentionResolver**
   - Одинаковая логика в Game и Forum модулях
   - **Рекомендация:** Вынести общую логику в Common

#### Реализованные улучшения

1. **Like soft delete** — лайки имеют поле `IsRemoved` для GDPR compliance
2. **Review audit** — отслеживание редактора через `ModifiedByUserId`
3. **GlobalChatEvents** — система глобальных чат-событий с участниками

---

## Ссылки

- [Паттерны и структура](./patterns.md) — Архитектура, Naming Conventions
- [Системный обзор](./overview.md) — Компоненты, порты
- [API Reference](../reference/api.md) — REST API
- [Глоссарий](../reference/glossary.md) — Термины
