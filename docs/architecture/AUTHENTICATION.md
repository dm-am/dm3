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
| **Хеширование** | Argon2id (см. [SECURITY.md](../conventions/SECURITY.md)) |
| **CSRF защита** | SameSite=Lax + CSRF middleware |
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
1. Регистрация email + пароля       → PendingRegistration (с TokenId)
2. Email со ссылкой активации        → страница выбора Login
3. Просмотр активации по токену      → форма выбора Login
4. Подтверждение активации + login   → Создать User, auto-login
```

### Защита от сквоттинга

- Имя пользователя (Login) выбирается ПОСЛЕ верификации email
- Нельзя занять логин без подтвержденного email
- PendingRegistration хранит TokenId внутри (без FK на Tokens)
- Cleanup: 7 дней для PendingRegistration

---

## Параметры безопасности

### Пароли

Параметры хеширования (алгоритм, memory cost, итерации, salt) — единый источник [SECURITY.md](../conventions/SECURITY.md#требования-к-хешированию-паролей).

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

По умолчанию checkbox "Запомнить меня" включен — сессия на 1 год.

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
| CSRF | SameSite=Lax, CSRF middleware |
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
