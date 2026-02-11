# Аутентификация DM3

> **Код:** `src/DM.Services.Authentication/`

---

## Обзор

| Компонент | Реализация |
|-----------|------------|
| **Pattern** | BFF (Backend-For-Frontend) |
| **Токен** | HttpOnly cookie `dm_session` |
| **Шифрование** | AES-256-GCM |
| **Хеширование** | PBKDF2-SHA256, 600K итераций |
| **CSRF защита** | SameSite=Strict |
| **Сессии** | MongoDB |

---

## Архитектура

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
- `src/DM.Web.API/Authentication/ApiCredentialsStorage.cs`
- `src/DM.Services.Authentication/Implementation/AuthenticationService.cs`

---

## Регистрация

### Flow: Email → Имя пользователя

```
1. POST /v1/account {email, password}       → PendingRegistration (с TokenId)
2. Email со ссылкой                         → /activate/{token}
3. GET /v1/account/activate/{token}         → Показать форму выбора Login
4. POST /v1/account/activate {token, login} → Создать User, auto-login
```

### Защита от сквоттинга

- Имя пользователя (Login) выбирается ПОСЛЕ верификации email
- Нельзя занять логин без подтверждённого email
- PendingRegistration хранит TokenId внутри (без FK на Tokens)
- Cleanup: 7 дней для PendingRegistration

### Таблица PendingRegistrations

| Поле | Назначение |
|------|-----------|
| TokenId | Токен для email-ссылки |
| TokenCreatedUtc | Для проверки 48h expiry |
| CreatedUtc | Для cleanup (7 дней) |
| Email | Адрес для верификации |
| PasswordHash, Salt | Хеш пароля (PBKDF2) |

**Файлы:**
- `src/DM.Services.DataAccess/BusinessObjects/Users/PendingRegistration.cs`
- `src/DM.Services.Community/BusinessProcesses/Account/Registration/RegistrationService.cs`
- `src/DM.Services.Community/BusinessProcesses/Account/Activation/ActivationService.cs`

---

## Параметры безопасности

### Пароли

| Параметр | Значение |
|----------|----------|
| Алгоритм | PBKDF2-SHA256 |
| Итерации | 600,000 |
| Salt | 32 bytes |
| Hash | 32 bytes |

**Файл:** `src/DM.Services.Authentication/Implementation/Security/SecurityManager.cs`

### Токены

| Параметр | Значение |
|----------|----------|
| Алгоритм | AES-256-GCM |
| Key | 32 bytes |
| Nonce | 12 bytes |
| Tag | 16 bytes |

**Файл:** `src/DM.Services.Authentication/Implementation/Security/AesGcmSymmetricCryptoService.cs`

### Сессии

| Параметр | Значение |
|----------|----------|
| Длительность (Remember me ✓) | 1 год (8760 часов) |
| Длительность (Remember me ✗) | 24 часа |
| Автообновление | за 7 дней до истечения |
| Отслеживание активности | Каждую 1 минуту |

По умолчанию checkbox "Запомнить меня" включён — сессия на 1 год.

**Файлы:**
- Конфиг: `src/DM.Web.API/appsettings.json` → `AuthenticationConfiguration`
- Дефолты: `src/DM.Services.Authentication/Configuration/AuthenticationConfiguration.cs`

### Политика паролей

| Параметр | Значение |
|----------|----------|
| Минимум | 8 символов |
| Максимум | 128 символов |
| Заглавные | Не обязательно |
| Строчные | Не обязательно |
| Цифры | Не обязательно |
| Спецсимволы | Не обязательно |

NIST SP 800-63B-4 (2024): composition rules не требуются при сильном хешировании.

**Файл:** `src/DM.Services.Community/Configuration/PasswordPolicyConfiguration.cs`

---

## Защита от атак

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

---

## Security Headers

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

---

## Авторизация

**Intention Pattern** — декларативная проверка прав.

**Пример:** `src/DM.Services.Community/BusinessProcesses/Messaging/ConversationIntentionResolver.cs`

```csharp
_intentionManager.ThrowIfForbidden(ConversationIntention.CreateMessage, conversation);
```

---

## Troubleshooting

### 401 Unauthorized

1. Проверь `withCredentials: true` в axios/fetch
2. Сессия истекла — перелогиниться
3. Пользователь забанен/удалён

### Cookie не устанавливается

1. HTTPS в production
2. Правильные CORS origins
3. `credentials: 'include'` в fetch

---

## Ссылки

- [README](../README.md) — Обзор документации
- [Архитектура](./OVERVIEW.md) — Общая архитектура
- [RBAC](./RBAC.md) — Роли и права
- [API Reference](../api/REFERENCE.md) — Справочник API

---

## Принципы документации


- **Минимум дублирования** — ссылки вместо копирования
- **Код > документация** — паттерны смотреть в коде
- **Только необходимое** — то, что нельзя узнать из кода
