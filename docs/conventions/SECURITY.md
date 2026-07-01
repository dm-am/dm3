# Требования безопасности DM3

> **User Story:** "Какие требования к безопасности? Какой чеклист?"

---

## Требования к хешированию паролей

| Параметр | Значение | Обоснование |
|----------|----------|-------------|
| Алгоритм | **Argon2id** | OWASP #1 рекомендация 2025, memory-hard |
| Memory | 46 MiB (m=47104) | Защита от GPU-атак |
| Iterations | t=1 | OWASP рекомендация |
| Parallelism | p=1 | OWASP рекомендация |
| Salt | 75 bytes | Избыточно, но безопасно (минимум 16 bytes) |
| Hash | 32 bytes | — |

> **Почему не PBKDF2?** PBKDF2 рекомендуется только для FIPS-140 compliance.
> Argon2id устойчивее к GPU/ASIC атакам благодаря memory-hardness.

**Файл:** `src/DM.Domain.Account/Features/Security/SecurityManager.cs`

---

## Требования к токенам

| Параметр | Требование |
|----------|------------|
| Алгоритм | AES-256-GCM |
| Key | 32 bytes |
| Nonce | 12 bytes, уникальный |
| Tag | 16 bytes |

**Файл:** `src/DM.Domain.Account/Features/Security/AesGcmSymmetricCryptoService.cs`

---

## Защита от атак (чеклист)

| Атака | Требуемая защита |
|-------|------------------|
| XSS | HttpOnly cookies, CSP header |
| CSRF | SameSite=Lax + CSRF middleware |

> **Почему SameSite=Lax, а не Strict?** Lax — OWASP рекомендация для 90% приложений.
> Strict блокирует cookies при переходах из email/внешних ссылок.
> Lax необходим для mirror transfer. Defense-in-depth: CSRF middleware дополнительно защищает.

| Brute-force | Rate limiting + lockout (15 попыток → 30 мин) |
| Timing | Constant-time comparison (FixedTimeEquals) |
| Token forgery | AES-GCM auth tag |
| Clickjacking | X-Frame-Options: DENY |
| MIME sniffing | X-Content-Type-Options: nosniff |
| SSRF | Блокировка private IP в BBCode URL |

---

## Security Headers (требования)

| Header | Требуемое значение |
|--------|-------------------|
| X-Frame-Options | DENY |
| X-Content-Type-Options | nosniff |
| X-XSS-Protection | 1; mode=block |
| Referrer-Policy | strict-origin-when-cross-origin |
| Permissions-Policy | Минимальные разрешения |
| Content-Security-Policy | default-src 'self' |
| Strict-Transport-Security | max-age=31536000 (production) |

**Файл:** `src/DM.Web.API/Middleware/SecurityHeadersMiddleware.cs`

---

## Чеклист для новых endpoints

- [ ] Rate limiting (если auth endpoint)
- [ ] Input validation (FluentValidation)
- [ ] Output encoding (BBCode → safe HTML)
- [ ] Authorization check (`[AuthenticationRequired]`)
- [ ] Не раскрывать существование пользователей (одинаковые ответы)
- [ ] Логирование security-событий

---

## Требования к паролям

| Параметр | Требование |
|----------|------------|
| Минимум | 8 символов |
| Максимум | 128 символов |
| Composition rules | Не обязательны (NIST SP 800-63B-4) |

**Файл:** `src/DM.Domain.Account/Configuration/PasswordPolicyConfiguration.cs`

---

## Требования к сессиям

| Параметр | Требование |
|----------|------------|
| Длительность (Remember me ✓) | 1 год (8760 часов) |
| Длительность (Remember me ✗) | 24 часа |
| Автообновление | за 7 дней до истечения |
| Storage | MongoDB (UserSessions) |

---

## NIST SP 800-63-4 Compliance

### Реализовано

| Требование | Статус |
|------------|--------|
| HIBP проверка паролей | ✓ k-anonymity API |
| Предупреждение коротких паролей | ✓ 15+ символов на фронтенде |
| Device info в сессиях | ✓ IP, User-Agent, время |
| Таймауты сессий | ✓ 24ч / 30д |
| Argon2id хеширование | ✓ Реализовано (OWASP #1) |
| Security audit log | ✓ MongoDB + API просмотра |

### Планируется (AAL2)

| Требование | Описание |
|------------|----------|
| TOTP 2FA | Authenticator app для второго фактора |
| Обязательный 2FA для ролей | Принудительно для Admin/SeniorModerator |
| Backup codes | Резервные коды при потере устройства |
| Password history | Запрет повторного использования паролей |

---

## Конфиденциальность пользовательского контента (BBCode)

Privacy-sensitive теги (`[private]`, `[mod]`) фильтруются **на сервере до сериализации JSON**. Отфильтрованный контент никогда не попадает на клиент к неавторизованному зрителю — это неотъемлемый инвариант, а не optimisation.

**Правила:**
- Клиентский рендеринг пользовательского BBCode на display-путях запрещен. Клиент биндит `v-html` на уже отрендеренную строку.
- Фильтрация не оставляет следов: вырезание — zero-information erase (нет placeholder'а, нет комментария, нет whitespace-gap'а).
- Moderator+ **не** имеет backdoor-доступа к `[private]` — никакого audit audience.
- Per-post и per-room override'ы действуют только внутри множества тех, кто уже имеет room read access; расширения никогда не обходят внешний гейт query-слоя.
- `author_edit` audience требует, чтобы endpoint-уровень авторизации предварительно подтвердил авторство зрителя — ответственность за проверку лежит на endpoint, не на рендерере.

Полный контракт — в [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md).

---

## Чеклист перед релизом

- [ ] Все auth endpoints имеют rate limiting
- [ ] CSP header настроен корректно
- [ ] HSTS включен (production)
- [ ] Секреты не хардкодятся (из env vars)
- [ ] Security audit log работает
- [ ] Brute-force protection протестирован

---

## Ссылки

- [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) — как реализовано
- [AUTHORIZATION.md](../architecture/AUTHORIZATION.md) — роли и права
- [BBCODE_RENDERING.md](../architecture/BBCODE_RENDERING.md) — фильтрация privacy-тегов
- [CODE_STYLE.md](./CODE_STYLE.md) — валидация
