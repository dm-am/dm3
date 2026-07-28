# Требования безопасности DM3

> **User Story:** "Какие требования к безопасности? Какой чеклист?"

---

## Требования к хешированию паролей

| Параметр | Значение | Обоснование |
|----------|----------|-------------|
| Алгоритм | **Argon2id** | OWASP #1 рекомендация 2025, memory-hard |
| Memory | 19 MiB (m=19456) | Защита от GPU-атак |
| Iterations | t=2 | OWASP рекомендация |
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
| Brute-force | Rate limiting + lockout (15 попыток → 30 мин) |
| Timing | Constant-time comparison (FixedTimeEquals) |
| Token forgery | AES-GCM auth tag |
| Clickjacking | X-Frame-Options: DENY |
| MIME sniffing | X-Content-Type-Options: nosniff |
| SSRF | Блокировка private IP в BBCode URL |
| Подмена адреса клиента | X-Forwarded-* обрабатывает только middleware, и только для сетей из конфигурации |
| Утечка внутренних деталей | Необработанное исключение отдает постоянный заголовок и токен корреляции, текст — только в лог |

> **Почему SameSite=Lax, а не Strict?** Lax — OWASP рекомендация для 90% приложений.
> Strict блокирует cookies при переходах из email/внешних ссылок.
> Lax необходим для mirror transfer. Defense-in-depth: CSRF middleware дополнительно защищает.

> **Адрес клиента.** Читать `X-Forwarded-For` из кода запрещено: заголовок пишет вызывающая
> сторона, а прокси лишь дописывает в него свой пир. Достоверен только результат
> `UseForwardedHeaders`, то есть `Connection.RemoteIpAddress`. Пустой список доверенных
> сетей означает, что заголовок не обрабатывается вовсе, и это значение по умолчанию.
>
> Доверенная сеть заполняется только там, где прокси — единственный вход. Диапазон,
> в который попадают и обычные клиенты, возвращает исходную дыру: подделать адрес
> сможет любой из них. Если порт приложения опубликован напрямую, доверенных сетей
> у такого развертывания нет вообще.

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
- [ ] Не раскрывать существование пользователей (одинаковые ответы) — либо внести в исключения ниже
- [ ] Логирование security-событий

---

## Записанное исключение: перечисление аккаунтов

Три surface отвечают на вопрос «зарегистрирован ли этот адрес», и это осознанный
компромисс, а не недосмотр:

| Surface | Что раскрывает | Зачем |
|---------|----------------|-------|
| `GET /v1/account/check-email` | почта занята / свободна / ждет активации | форма регистрации обязана сказать, что адрес занят |
| `GET /v1/account/check-username` | имя занято / зарезервировано | то же для имени |
| `POST /v1/account/recovery` | аккаунта нет / выслан сброс / выслана активация | форма восстановления обязана отличать опечатку в адресе от отправленного письма |

**Почему не закрываем.** Закрыть можно только все три сразу: пока проверка занятости
отвечает при регистрации, схлопывание ответа восстановления не убирает перечисление,
а только лишает пользователя сообщения об опечатке. Полное закрытие означает
регистрацию, которая не объясняет, почему не проходит. Для сайта с публичными
профилями и никнеймами цена по удобству выше пользы от скрытия факта, который
все равно виден.

**Что обязательно взамен.** Rate limiting на всех трех (он замедляет массовое
перечисление, но не предотвращает его — так и писать, не выдавать за защиту) и
запись каждого раскрывающего ответа в лог: событие, адрес клиента, исход. Сам
проверяемый идентификатор в лог не пишется — у лог-хранилища нет retention, и
запись превратила бы каждое сканирование в сохраненный список адресов.

**Что остается закрытым.** Вход не различает неверный пароль и отсутствующий
аккаунт: ответы совпадают побайтово. Это не исключение, и ослаблять его нельзя.

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
| Длительность | 365 дней с "запомнить меня", 8760 часов без него — фактически одинаково, см. [AUTHENTICATION.md](../architecture/AUTHENTICATION.md) |
| Автообновление | каждые 7 дней |
| Storage | MongoDB (UserSessions) |

---

## NIST SP 800-63-4 Compliance

### Реализовано

| Требование | Статус |
|------------|--------|
| HIBP проверка паролей | ✓ k-anonymity API |
| Предупреждение коротких паролей | ✓ 15+ символов на фронтенде |
| Device info в сессиях | ✓ IP, User-Agent, время |
| Таймауты сессий | ✓ 1 год (обычная и "запомнить меня") |
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
