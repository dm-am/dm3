# Аутентификация DM3

> **User Story:** "Как работает вход? Какой flow регистрации? Какие параметры безопасности?"

> **Код:** `src/DM.Domain.Account/Features/`, `src/DM.Web.API/Shared/Authentication/`

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

---

## Параметры безопасности

### Пароли

| Параметр | Значение |
|----------|----------|
| Алгоритм | PBKDF2-SHA256 |
| Итерации | 600,000 |
| Salt | 32 bytes |
| Hash | 32 bytes |

### Токены

| Параметр | Значение |
|----------|----------|
| Алгоритм | AES-256-GCM |
| Key | 32 bytes |
| Nonce | 12 bytes |
| Tag | 16 bytes |

### Сессии

| Параметр | Значение |
|----------|----------|
| Длительность (Remember me ✓) | 1 год (8760 часов) |
| Длительность (Remember me ✗) | 24 часа |
| Автообновление | за 7 дней до истечения |
| Отслеживание активности | Каждую 1 минуту |

По умолчанию checkbox "Запомнить меня" включён — сессия на 1 год.

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

| Header | Значение |
|--------|----------|
| X-Frame-Options | DENY |
| X-Content-Type-Options | nosniff |
| X-XSS-Protection | 1; mode=block |
| Referrer-Policy | strict-origin-when-cross-origin |
| Permissions-Policy | Минимальные разрешения |
| Content-Security-Policy | default-src 'self' |
| Strict-Transport-Security | max-age=31536000 (production) |

---

## Ссылки

- [AUTHORIZATION.md](./AUTHORIZATION.md) — роли и права
- [SYSTEM.md](./SYSTEM.md) — архитектура системы
- [SECURITY.md](../conventions/SECURITY.md) — требования безопасности
