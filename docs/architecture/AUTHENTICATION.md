# Аутентификация и авторизация DM3

> **Метаданные:**
> - Документ: AUTHENTICATION.md
> - Версия: 2.0
> - Дата создания: 2026-01-27
> - Последнее обновление: 2026-01-27
> - Связанные документы:
>   - [OPENIDDICT_IMPLEMENTATION.md](../../OPENIDDICT_IMPLEMENTATION.md) (архивный)
>   - [OPENIDDICT_QUICKSTART.md](../../OPENIDDICT_QUICKSTART.md) (архивный)

---

## Содержание

- [Обзор системы аутентификации](#обзор-системы-аутентификации)
- [Архитектура](#архитектура)
- [Legacy аутентификация (X-Dm-Auth-Token)](#legacy-аутентификация-x-dm-auth-token)
- [OAuth 2.0 / OpenIddict](#oauth-20--openiddict)
- [Dual-Mode аутентификация](#dual-mode-аутентификация)
- [Хеширование паролей](#хеширование-паролей)
- [Социальная аутентификация](#социальная-аутентификация)
- [Авторизация (Intention Pattern)](#авторизация-intention-pattern)
- [Безопасность](#безопасность)

---

## Обзор системы аутентификации

DM3 поддерживает **два режима аутентификации**:

1. **Legacy Token Authentication** (X-Dm-Auth-Token) - оригинальная система с симметричным шифрованием
2. **OAuth 2.0 / OpenIddict** - современная стандартизованная аутентификация

Оба режима работают одновременно и используют общую базу пользователей и сессий.

### Преимущества двойного режима

- **Обратная совместимость**: существующие клиенты продолжают работать
- **Плавная миграция**: новые клиенты используют OAuth 2.0
- **Гибкость**: разные типы клиентов могут выбирать подходящий метод
- **Стандарты**: OAuth 2.0 открывает возможности для интеграций

---

## Архитектура

```
┌─────────────────────────────────────────────────────────────────┐
│                         КЛИЕНТ                                  │
│                                                                 │
│  ┌──────────────────┐              ┌─────────────────────┐     │
│  │ Legacy Client    │              │ Modern Client       │     │
│  │ X-Dm-Auth-Token  │              │ OAuth 2.0 Bearer    │     │
│  └────────┬─────────┘              └──────────┬──────────┘     │
└───────────┼────────────────────────────────────┼────────────────┘
            │                                    │
            │                                    │
            ▼                                    ▼
┌─────────────────────────────────────────────────────────────────┐
│                  ASP.NET CORE MIDDLEWARE                        │
│                                                                 │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              Policy Scheme Selector                      │  │
│  │  (автоматический выбор handler по типу токена)           │  │
│  └────────┬─────────────────────────────┬───────────────────┘  │
│           │                             │                       │
│           ▼                             ▼                       │
│  ┌────────────────────┐      ┌────────────────────────┐        │
│  │ Legacy Auth        │      │ OpenIddict             │        │
│  │ Handler            │      │ Validation             │        │
│  └────────┬───────────┘      └───────────┬────────────┘        │
└───────────┼──────────────────────────────┼─────────────────────┘
            │                              │
            │    ┌─────────────────────┐   │
            └───▶│ IAuthenticationService│◀─┘
                 │ (общий сервис)      │
                 └──────────┬──────────┘
                            │
                ┌───────────┴───────────┐
                │                       │
                ▼                       ▼
         ┌────────────┐          ┌────────────┐
         │ PostgreSQL │          │  MongoDB   │
         │ (Users)    │          │ (Sessions) │
         └────────────┘          └────────────┘
```

---

## Legacy аутентификация (X-Dm-Auth-Token)

### Обзор

Оригинальная система аутентификации DM3 на основе симметричного шифрования.

### Поток аутентификации

```
1. Пользователь вводит login/password
2. POST /v1/account/login
3. Backend:
   → Поиск пользователя в PostgreSQL
   → Проверка: активирован? не забанен? пароль верный?
   → Проверка: нужен ли rehash (opportunistic rehashing)
   → Создание сессии в MongoDB.UserSessions
   → Шифрование {userId, sessionId} → токен (TripleDES)
4. Возвращает токен в заголовке x-dm-auth-token
5. Frontend сохраняет в localStorage
```

### Проверка токена

```
1. Frontend: токен в заголовке X-Dm-Auth-Token
2. LegacyTokenAuthenticationHandler:
   → Расшифровывает токен → userId, sessionId
   → Проверяет сессию в MongoDB
   → Загружает пользователя из PostgreSQL
   → Создаёт ClaimsPrincipal
   → Устанавливает identity в HttpContext
3. Контроллер получает доступ к текущему пользователю
```

### Формат токена

```csharp
public class AuthToken
{
    public Guid UserId { get; set; }
    public Guid SessionId { get; set; }
}

// Шифрование: TripleDES с ключами из конфигурации
// Формат: Base64(Encrypt(JSON(AuthToken)))
```

### Хранение сессий

**MongoDB коллекция: UserSessions**

```csharp
public class UserSession
{
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ExpiresAt { get; set; }
    public string UserAgent { get; set; }
    public string IpAddress { get; set; }
}
```

### Конфигурация

**appsettings.json:**
```json
{
  "Security": {
    "Salts": [
      "первый-ключ",
      "второй-ключ",
      "третий-ключ"
    ]
  }
}
```

### Преимущества

- ✅ Простая реализация
- ✅ Не требует дополнительных БД таблиц
- ✅ Быстрая проверка токена

### Недостатки

- ❌ Не стандартизирована
- ❌ Сложно интегрировать с внешними системами
- ❌ Требует custom client код
- ❌ Нет встроенной поддержки refresh tokens

---

## OAuth 2.0 / OpenIddict

### Обзор

Современная стандартизованная аутентификация на основе [OpenIddict](https://github.com/openiddict/openiddict-core).

### Реализация

**Файл: `src/DM.Web.API/Startup.cs`**

```csharp
services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<DmDbContext>();
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token")
               .SetUserinfoEndpointUris("/connect/userinfo");

        options.AllowPasswordFlow()
               .AllowRefreshTokenFlow();

        options.AcceptAnonymousClients();

        if (env.IsDevelopment())
        {
            options.AddDevelopmentEncryptionCertificate()
                   .AddDevelopmentSigningCertificate();
        }
        else
        {
            var certPath = Configuration["OpenIddict:CertificatePath"];
            var certPassword = Configuration["OpenIddict:CertificatePassword"];
            var cert = new X509Certificate2(certPath, certPassword);

            options.AddEncryptionCertificate(cert)
                   .AddSigningCertificate(cert);
        }

        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough()
               .EnableUserinfoEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });
```

### Endpoints

#### 1. Token Endpoint

**`POST /connect/token`**

**Файл:** `src/DM.Web.API/Controllers/v1/Connect/TokenController.cs`

##### Password Grant Flow

Получение access token по логину/паролю:

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=password&username=alice&password=Test123!
```

**Ответ:**
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600,
  "refresh_token": "C98BAE37F30345E98BA0..."
}
```

**Логика:**
```csharp
// 1. Проверка credentials через IAuthenticationService
var authUser = await _authenticationService.Authenticate(
    new Login { Login = username, Password = password },
    cancellationToken);

// 2. Создание ClaimsPrincipal
var identity = new ClaimsIdentity(
    TokenValidationParameters.DefaultAuthenticationType,
    Claims.Name,
    Claims.Role);

identity.AddClaim(new Claim(Claims.Subject, authUser.UserId.ToString()));
identity.AddClaim(new Claim(Claims.Name, authUser.Login));
identity.AddClaim(new Claim(Claims.Role, authUser.Role));
identity.AddClaim(new Claim("username", authUser.Login));
identity.AddClaim(new Claim("session_id", authUser.SessionId));

var principal = new ClaimsPrincipal(identity);

// 3. Выдача токенов
return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
```

##### Refresh Token Flow

Обновление access token по refresh token:

```http
POST /connect/token
Content-Type: application/x-www-form-urlencoded

grant_type=refresh_token&refresh_token=C98BAE37F30345E98BA0...
```

**Ответ:**
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...",
  "token_type": "Bearer",
  "expires_in": 3600
}
```

**Логика:**
```csharp
// 1. Проверка существования пользователя
var userId = Guid.Parse(principal.GetClaim(Claims.Subject));
var user = await _userRepository.GetUserAsync(userId, cancellationToken);

if (user == null)
{
    return Forbid(
        authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
        properties: new AuthenticationProperties(new Dictionary<string, string>
        {
            [OpenIddictServerAspNetCoreConstants.Properties.Error] = Errors.InvalidGrant,
            [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                "The refresh token is no longer valid."
        }));
}

// 2. Выдача нового access token (refresh token не обновляется)
return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
```

#### 2. Userinfo Endpoint

**`GET /connect/userinfo`**

**Файл:** `src/DM.Web.API/Controllers/v1/Connect/UserinfoController.cs`

Получение информации о текущем пользователе:

```http
GET /connect/userinfo
Authorization: Bearer eyJhbGciOiJSUzI1NiIsInR5cCI6IkpXVCJ9...
```

**Ответ:**
```json
{
  "sub": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "name": "alice",
  "username": "alice",
  "role": "Player",
  "session_id": "7c9e6679-7425-40de-944b-e07fc1f90ae7"
}
```

**Логика:**
```csharp
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
[HttpGet("~/connect/userinfo"), HttpPost("~/connect/userinfo")]
[ProducesResponseType(StatusCodes.Status200OK)]
public IActionResult GetUserInfo()
{
    var claims = User.Claims.ToDictionary(c => c.Type, c => c.Value);
    return Ok(claims);
}
```

### Claims

**Стандартные OpenID Connect claims:**

| Claim | Тип | Описание |
|-------|-----|----------|
| `sub` | string (GUID) | Subject - уникальный ID пользователя |
| `name` | string | Отображаемое имя пользователя |
| `username` | string | Логин для входа |
| `role` | string | Роль пользователя (Player, Moderator, Administrator) |
| `session_id` | string (GUID) | ID сессии |

### Database Tables

OpenIddict создаёт таблицы для хранения:

- **OpenIddictApplications** - зарегистрированные приложения (клиенты)
- **OpenIddictAuthorizations** - разрешения
- **OpenIddictScopes** - области доступа
- **OpenIddictTokens** - выданные токены

**Миграция:**
```bash
cd src/DM.Services.DataAccess
dotnet ef migrations add AddOpenIddict --startup-project ../DM.Web.API
dotnet ef database update --startup-project ../DM.Web.API
```

### Сертификаты

#### Development

Автоматически генерируются при старте:

```csharp
options.AddDevelopmentEncryptionCertificate()
       .AddDevelopmentSigningCertificate();
```

#### Production

Используйте настоящие сертификаты:

```csharp
var certPath = Configuration["OpenIddict:CertificatePath"];
var certPassword = Configuration["OpenIddict:CertificatePassword"];
var cert = new X509Certificate2(certPath, certPassword);

options.AddEncryptionCertificate(cert)
       .AddSigningCertificate(cert);
```

**appsettings.Production.json:**
```json
{
  "OpenIddict": {
    "CertificatePath": "/app/certs/openiddict.pfx",
    "CertificatePassword": "${OPENIDDICT_CERT_PASSWORD}"
  }
}
```

### Rate Limiting

Token endpoint защищён rate limiting:

```csharp
[EnableRateLimiting("fixed-by-ip")]  // 5 запросов в минуту
public class TokenController : Controller
{
    // ...
}
```

### Преимущества

- ✅ Стандартизированный протокол (RFC 6749, RFC 7519)
- ✅ Поддержка refresh tokens
- ✅ JWT токены (stateless)
- ✅ Готовность к интеграциям
- ✅ Безопасность (industry best practices)
- ✅ Масштабируемость

---

## Dual-Mode аутентификация

### Policy Scheme

**Файл:** `src/DM.Web.API/Startup.cs`

```csharp
services.AddAuthentication(options =>
{
    options.DefaultScheme = "DualMode";
    options.DefaultChallengeScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
})
.AddPolicyScheme("DualMode", "Dual Mode", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // Если есть Authorization: Bearer → OpenIddict
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (authHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) == true)
        {
            return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
        }

        // Если есть X-Dm-Auth-Token → Legacy
        if (context.Request.Headers.ContainsKey("X-Dm-Auth-Token"))
        {
            return "LegacyToken";
        }

        // По умолчанию → OpenIddict
        return OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    };
})
.AddScheme<AuthenticationSchemeOptions, LegacyTokenAuthenticationHandler>(
    "LegacyToken", options => { });
```

### LegacyTokenAuthenticationHandler

**Файл:** `src/DM.Web.API/Authentication/LegacyTokenAuthenticationHandler.cs`

```csharp
public class LegacyTokenAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly IAuthenticationService _authenticationService;

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        // 1. Извлечь токен из заголовка
        if (!Request.Headers.TryGetValue("X-Dm-Auth-Token", out var tokenValue))
        {
            return AuthenticateResult.NoResult();
        }

        try
        {
            // 2. Аутентификация через существующий сервис
            var token = tokenValue.ToString();
            var authUser = await _authenticationService.Authenticate(token);

            // 3. Создать ClaimsPrincipal
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, authUser.UserId.ToString()),
                new Claim(ClaimTypes.Name, authUser.Login),
                new Claim(ClaimTypes.Role, authUser.Role)
            };

            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);

            var ticket = new AuthenticationTicket(principal, Scheme.Name);
            return AuthenticateResult.Success(ticket);
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail(ex.Message);
        }
    }
}
```

### Использование в контроллерах

**Автоматический выбор:**
```csharp
[Authorize]  // Работает с обоими методами
public class GameController : Controller
{
    // Пользователь аутентифицирован через Bearer ИЛИ X-Dm-Auth-Token
}
```

**Явный выбор OpenIddict:**
```csharp
[Authorize(AuthenticationSchemes = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)]
public class UserinfoController : Controller
{
    // ТОЛЬКО OAuth 2.0 Bearer токены
}
```

---

## Хеширование паролей

### Алгоритм: PBKDF2 с SHA256

**Файл:** `src/DM.Services.Authentication/Implementation/Security/HashProvider.cs`

```csharp
public class HashProvider : IHashProvider
{
    private const int SaltSize = 75;          // bytes
    private const int HashSize = 75;          // bytes
    private const int Iterations = 600_000;   // OWASP 2025 рекомендация
    private const int Version = 3;

    public string Hash(string password)
    {
        // 1. Генерация случайной соли
        var salt = new byte[SaltSize];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(salt);

        // 2. Хеширование
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            Iterations,
            HashAlgorithmName.SHA256);
        var hash = pbkdf2.GetBytes(HashSize);

        // 3. Объединение версии, соли и хеша
        var result = new byte[1 + SaltSize + HashSize];
        result[0] = (byte)Version;
        Buffer.BlockCopy(salt, 0, result, 1, SaltSize);
        Buffer.BlockCopy(hash, 0, result, 1 + SaltSize, HashSize);

        return Convert.ToBase64String(result);
    }

    public bool Verify(string password, string passwordHash)
    {
        var bytes = Convert.FromBase64String(passwordHash);

        // 1. Извлечение версии
        var version = bytes[0];

        // 2. Извлечение соли
        var salt = new byte[SaltSize];
        Buffer.BlockCopy(bytes, 1, salt, 0, SaltSize);

        // 3. Извлечение оригинального хеша
        var storedHash = new byte[HashSize];
        Buffer.BlockCopy(bytes, 1 + SaltSize, storedHash, 0, HashSize);

        // 4. Вычисление хеша для проверки
        var iterations = GetIterationsForVersion(version);
        using var pbkdf2 = new Rfc2898DeriveBytes(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256);
        var computedHash = pbkdf2.GetBytes(HashSize);

        // 5. Constant-time сравнение
        return CryptographicOperations.FixedTimeEquals(storedHash, computedHash);
    }

    private int GetIterationsForVersion(int version)
    {
        return version switch
        {
            1 => 10_000,
            2 => 100_000,
            3 => 600_000,
            _ => throw new ArgumentException($"Unknown hash version: {version}")
        };
    }
}
```

### Opportunistic Rehashing

При входе пользователя проверяется версия хеша. Если она устарела - автоматически обновляется:

```csharp
// Файл: src/DM.Services.Authentication/Implementation/AuthenticationService.cs

public async Task<AuthenticatedUser> Authenticate(Login login)
{
    var user = await _repository.GetUserByLogin(login.Login);

    // Проверка пароля
    if (!_hashProvider.Verify(login.Password, user.PasswordHash))
    {
        throw new InvalidCredentialsException();
    }

    // Opportunistic rehashing
    if (_hashProvider.NeedsRehash(user.PasswordHash))
    {
        var newHash = _hashProvider.Hash(login.Password);
        await _repository.UpdatePasswordHash(user.UserId, newHash);
    }

    // Создание сессии
    var session = await _repository.CreateSession(user.UserId);

    return new AuthenticatedUser
    {
        UserId = user.UserId,
        Login = user.Login,
        Role = user.Role,
        SessionId = session.SessionId
    };
}
```

### История версий

| Версия | Итерации | Период использования | Безопасность |
|--------|----------|---------------------|--------------|
| 1 | 10,000 | 2020-2023 | ⚠️ Слабая |
| 2 | 100,000 | 2023-2025 | 🟡 Средняя |
| 3 | 600,000 | 2025+ | ✅ Сильная (OWASP 2025) |

### Рекомендации OWASP

**OWASP Password Storage Cheat Sheet (2025):**
- PBKDF2-SHA256: минимум 600,000 итераций
- Argon2id: рекомендуется для новых проектов
- Bcrypt: минимум 13 rounds (устаревает)

**Источник:** [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)

---

## Социальная аутентификация

### Discord OAuth

**Реализовано:** Вход через Discord

#### Настройка

1. Создайте приложение в [Discord Developer Portal](https://discord.com/developers/applications)
2. Добавьте Redirect URL: `https://your-domain.com/v1/external-auth/discord/callback`
3. Скопируйте Client ID и Client Secret

**appsettings.json:**
```json
{
  "ExternalAuth": {
    "Discord": {
      "ClientId": "YOUR_CLIENT_ID",
      "ClientSecret": "YOUR_CLIENT_SECRET",
      "RedirectUri": "https://your-domain.com/v1/external-auth/discord/callback"
    }
  }
}
```

#### Endpoints

**1. Инициация OAuth flow**

```http
GET /v1/external-auth/discord
```

Перенаправляет на Discord authorization page.

**2. Callback**

```http
GET /v1/external-auth/discord/callback?code=AUTHORIZATION_CODE
```

Обменивает code на access token, получает данные пользователя из Discord API, создаёт/связывает аккаунт.

#### Связывание аккаунтов

**Файл:** `src/DM.Services.Community/BusinessProcesses/Account/ExternalAuth/ExternalAuthService.cs`

```csharp
public async Task<AuthenticatedUser> AuthenticateViaDiscord(string code)
{
    // 1. Обмен code на access token
    var tokenResponse = await _discordClient.ExchangeCodeForToken(code);

    // 2. Получение данных пользователя
    var discordUser = await _discordClient.GetUser(tokenResponse.AccessToken);

    // 3. Поиск существующего аккаунта
    var user = await _repository.FindUserByDiscordId(discordUser.Id);

    if (user == null)
    {
        // 4a. Создание нового пользователя
        user = await _factory.CreateFromDiscord(discordUser);
        await _repository.Create(user);
    }
    else
    {
        // 4b. Обновление данных существующего
        await _repository.UpdateDiscordData(user.UserId, discordUser);
    }

    // 5. Создание сессии
    var session = await _repository.CreateSession(user.UserId);

    return new AuthenticatedUser
    {
        UserId = user.UserId,
        Login = user.Login,
        Role = user.Role,
        SessionId = session.SessionId
    };
}
```

### Планируемые провайдеры

**Roadmap (v1.5 - v2.0):**
- Google OAuth
- VK OAuth
- GitHub OAuth
- Telegram Auth

---

## Авторизация (Intention Pattern)

### Обзор

DM3 использует **Intention Pattern** для авторизации - декларативный подход к проверке прав.

### Архитектура

```
┌─────────────────────────────────────────────────────────────────┐
│                      Business Service                           │
│                                                                 │
│  intentionManager.ThrowIfForbidden(TopicIntention.Edit, topic) │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                     IntentionManager                            │
│                                                                 │
│  IsAllowed(intention, entity) → bool                            │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│                  TopicIntentionResolver                         │
│                                                                 │
│  public bool IsAllowed(TopicIntention intention, Topic topic)   │
│  {                                                              │
│      return intention switch                                    │
│      {                                                          │
│          TopicIntention.Create => user.IsAuthenticated,         │
│          TopicIntention.Edit => user.IsAuthor || user.IsAdmin,  │
│          TopicIntention.Delete => user.IsModerator,             │
│          _ => false                                             │
│      };                                                         │
│  }                                                              │
└─────────────────────────────────────────────────────────────────┘
```

### Пример использования

#### 1. Определение Intentions

```csharp
public enum TopicIntention
{
    Create,
    Edit,
    Delete,
    Close,
    Pin,
    Lock
}
```

#### 2. Реализация Resolver

```csharp
public class TopicIntentionResolver : IIntentionResolver<TopicIntention, ForumTopic>
{
    private readonly IIdentityProvider _identityProvider;

    public TopicIntentionResolver(IIdentityProvider identityProvider)
    {
        _identityProvider = identityProvider;
    }

    public bool IsAllowed(TopicIntention intention, ForumTopic topic)
    {
        var user = _identityProvider.User;

        return intention switch
        {
            TopicIntention.Create => user.IsAuthenticated,

            TopicIntention.Edit =>
                user.UserId == topic.AuthorId ||
                user.Role >= UserRole.Administrator,

            TopicIntention.Delete =>
                user.Role >= UserRole.SeniorModerator,

            TopicIntention.Close =>
                user.UserId == topic.AuthorId ||
                user.Role >= UserRole.Administrator,

            TopicIntention.Pin =>
                user.Role >= UserRole.Administrator,

            TopicIntention.Lock =>
                user.Role >= UserRole.Administrator,

            _ => false
        };
    }
}
```

#### 3. Использование в Service

```csharp
public class TopicUpdatingService : ITopicUpdatingService
{
    private readonly IIntentionManager _intentionManager;
    private readonly ITopicRepository _repository;

    public async Task<Topic> Update(Guid topicId, UpdateTopic dto)
    {
        // 1. Загрузить entity
        var topic = await _repository.Get(topicId);

        // 2. Проверить права (выбросит ForbiddenException если нет прав)
        _intentionManager.ThrowIfForbidden(TopicIntention.Edit, topic);

        // 3. Выполнить бизнес-логику
        topic.Title = dto.Title;
        topic.Text = dto.Text;
        topic.ModifiedAt = DateTimeOffset.UtcNow;

        await _repository.Update(topic);

        return topic;
    }
}
```

### Регистрация в DI

```csharp
// Файл: src/DM.Services.Forum/ForumModule.cs

public class ForumModule : Module
{
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<TopicIntentionResolver>()
            .As<IIntentionResolver<TopicIntention, ForumTopic>>()
            .InstancePerLifetimeScope();

        // IntentionManager регистрируется автоматически
    }
}
```

### Обработка ошибок

```csharp
// Если нет прав → выбрасывается ForbiddenException
try
{
    await _topicService.Update(topicId, dto);
}
catch (ForbiddenException ex)
{
    // Middleware автоматически преобразует в 403 Forbidden
    // {
    //   "type": "https://tools.ietf.org/html/rfc7231#section-6.5.3",
    //   "title": "Forbidden",
    //   "status": 403,
    //   "detail": "You don't have permission to edit this topic"
    // }
}
```

### Преимущества

- ✅ **Декларативность**: явное объявление прав
- ✅ **Централизация**: вся логика авторизации в одном месте
- ✅ **Тестируемость**: легко покрыть тестами
- ✅ **Читаемость**: понятный switch expression
- ✅ **Безопасность**: fail-secure (по умолчанию запрещено)

---

## Безопасность

### Best Practices

#### 1. Хранение секретов

**❌ НЕ ХРАНИТЕ в коде:**
```csharp
// ПЛОХО!
var password = "hardcoded-password";
```

**✅ Используйте конфигурацию:**
```json
{
  "Security": {
    "Salts": ["${SALT_1}", "${SALT_2}", "${SALT_3}"]
  },
  "OpenIddict": {
    "CertificatePassword": "${OPENIDDICT_CERT_PASSWORD}"
  }
}
```

**✅ Или User Secrets в development:**
```bash
dotnet user-secrets set "Security:Salts:0" "first-key"
dotnet user-secrets set "Security:Salts:1" "second-key"
dotnet user-secrets set "Security:Salts:2" "third-key"
```

#### 2. HTTPS Only

```csharp
// Файл: src/DM.Web.API/Startup.cs

if (!env.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

**appsettings.Production.json:**
```json
{
  "Kestrel": {
    "Endpoints": {
      "Https": {
        "Url": "https://*:443",
        "Certificate": {
          "Path": "/app/certs/server.pfx",
          "Password": "${SERVER_CERT_PASSWORD}"
        }
      }
    }
  }
}
```

#### 3. Rate Limiting

```csharp
// Файл: src/DM.Web.API/Startup.cs

services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("fixed-by-ip", options =>
    {
        options.PermitLimit = 5;
        options.Window = TimeSpan.FromMinutes(1);
        options.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        options.QueueLimit = 0;
    });
});

// Применение к контроллерам
[EnableRateLimiting("fixed-by-ip")]
public class TokenController : Controller { }
```

#### 4. CORS

```csharp
services.AddCors(options =>
{
    options.AddDefaultPolicy(builder =>
    {
        builder.WithOrigins(Configuration.GetSection("IntegrationSettings:CorsUrls")
                .Get<string[]>())
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});
```

**appsettings.json:**
```json
{
  "IntegrationSettings": {
    "CorsUrls": [
      "http://localhost:5173",
      "https://dm3.ru"
    ]
  }
}
```

#### 5. Безопасные заголовки

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

    await next();
});
```

### Аудит безопасности

**См. также:**
- [docs/SECURITY.md](../SECURITY.md) - политика безопасности
- [docs/PROJECT_STANDARDS.md](../PROJECT_STANDARDS.md) - стандарты безопасного кодирования

### Известные уязвимости

**Статус:** Нет известных критических уязвимостей

**Регулярные проверки:**
- Dependabot alerts (GitHub)
- OWASP Dependency Check
- SonarQube анализ

---

## Миграция с Legacy на OAuth 2.0

### Стратегия

1. **Фаза 1: Поддержка обоих режимов** ✅ (текущая)
   - Оба метода работают параллельно
   - Новые клиенты используют OAuth 2.0
   - Старые клиенты продолжают работать

2. **Фаза 2: Deprecation Legacy** (v1.5)
   - Логирование использования legacy токенов
   - Предупреждения в API responses
   - Документация миграции для клиентов

3. **Фаза 3: Удаление Legacy** (v2.0)
   - Отключение X-Dm-Auth-Token
   - Только OAuth 2.0

### Для клиентских приложений

#### Legacy (текущий метод)

```javascript
// 1. Login
const response = await fetch('/v1/account/login', {
  method: 'POST',
  body: JSON.stringify({ login: 'alice', password: 'Test123!' })
});

const token = response.headers.get('x-dm-auth-token');
localStorage.setItem('token', token);

// 2. Использование
fetch('/v1/games', {
  headers: {
    'X-Dm-Auth-Token': localStorage.getItem('token')
  }
});
```

#### OAuth 2.0 (новый метод)

```javascript
// 1. Login
const response = await fetch('/connect/token', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/x-www-form-urlencoded'
  },
  body: 'grant_type=password&username=alice&password=Test123!'
});

const data = await response.json();
localStorage.setItem('access_token', data.access_token);
localStorage.setItem('refresh_token', data.refresh_token);

// 2. Использование
fetch('/v1/games', {
  headers: {
    'Authorization': `Bearer ${localStorage.getItem('access_token')}`
  }
});

// 3. Refresh
const refreshResponse = await fetch('/connect/token', {
  method: 'POST',
  headers: {
    'Content-Type': 'application/x-www-form-urlencoded'
  },
  body: `grant_type=refresh_token&refresh_token=${localStorage.getItem('refresh_token')}`
});

const newData = await refreshResponse.json();
localStorage.setItem('access_token', newData.access_token);
```

---

## Troubleshooting

### Проблема: "Invalid token"

**Симптомы:** 401 Unauthorized

**Причины:**
1. Токен истёк
2. Сессия удалена из MongoDB
3. Пользователь деактивирован/забанен
4. Неверный формат токена

**Решение:**
- Legacy: получить новый токен через /v1/account/login
- OAuth 2.0: использовать refresh token

### Проблема: "Certificate not found" (Production)

**Симптомы:** Приложение не стартует

**Причины:**
- Не указан путь к сертификату в конфигурации
- Файл сертификата не существует
- Неверный пароль сертификата

**Решение:**
```bash
# Проверить наличие файла
ls -la /app/certs/openiddict.pfx

# Проверить переменные окружения
echo $OPENIDDICT_CERT_PASSWORD

# Проверить права доступа
chmod 600 /app/certs/openiddict.pfx
```

### Проблема: "CORS error"

**Симптомы:** Frontend не может сделать запрос к API

**Решение:**
Добавить frontend URL в конфигурацию:

```json
{
  "IntegrationSettings": {
    "CorsUrls": [
      "http://localhost:5173",
      "https://your-domain.com"
    ]
  }
}
```

---

## Полезные ссылки

### Стандарты

- [RFC 6749 - OAuth 2.0](https://datatracker.ietf.org/doc/html/rfc6749)
- [RFC 7519 - JWT](https://datatracker.ietf.org/doc/html/rfc7519)
- [RFC 8252 - OAuth 2.0 for Native Apps](https://datatracker.ietf.org/doc/html/rfc8252)
- [OpenID Connect Core 1.0](https://openid.net/specs/openid-connect-core-1_0.html)

### Библиотеки

- [OpenIddict Documentation](https://documentation.openiddict.com/)
- [ASP.NET Core Authentication](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/)
- [ASP.NET Core Authorization](https://learn.microsoft.com/en-us/aspnet/core/security/authorization/)

### Безопасность

- [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html)
- [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html)
- [NIST Digital Identity Guidelines](https://pages.nist.gov/800-63-3/)

---

## Связанная документация

- [docs/SECURITY.md](../SECURITY.md) - политика безопасности
- [docs/PROJECT_STANDARDS.md](../PROJECT_STANDARDS.md) - стандарты разработки
- [docs/architecture/OVERVIEW.md](./OVERVIEW.md) - общая архитектура
- [OPENIDDICT_IMPLEMENTATION.md](../../OPENIDDICT_IMPLEMENTATION.md) - архивная документация OpenIddict
- [OPENIDDICT_QUICKSTART.md](../../OPENIDDICT_QUICKSTART.md) - архивное быстрое руководство

---

> **Последнее обновление:** 2026-01-27
> **Версия:** 2.0
> **Maintainer:** @quilin
