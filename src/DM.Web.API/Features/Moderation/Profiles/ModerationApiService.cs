using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;
using Microsoft.Extensions.Options;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;
using Microsoft.EntityFrameworkCore;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <inheritdoc />
internal class ModerationApiService : IModerationApiService
{
    private readonly DmDbContext _dbContext;
    private readonly DmMongoClient _mongoClient;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserService _userService;
    private readonly IIntentionManager _intentionManager;
    private readonly ISecurityManager _securityManager;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMapper _mapper;
    private readonly IPollRepository _pollRepository;
    private readonly IPublicIdService _publicIdService;
    private readonly IImageProcessingService _imageProcessing;
    private readonly Amazon.S3.IAmazonS3 _s3Client;
    private readonly CdnConfiguration _cdnConfig;

    /// <summary>
    /// Creates a new instance of <see cref="ModerationApiService"/>
    /// </summary>
    public ModerationApiService(
        DmDbContext dbContext,
        DmMongoClient mongoClient,
        IIdentityProvider identityProvider,
        IUserService userService,
        IIntentionManager intentionManager,
        ISecurityManager securityManager,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IMapper mapper,
        IPollRepository pollRepository,
        IPublicIdService publicIdService,
        IImageProcessingService imageProcessing,
        Amazon.S3.IAmazonS3 s3Client,
        IOptions<CdnConfiguration> cdnOptions)
    {
        _dbContext = dbContext;
        _mongoClient = mongoClient;
        _identityProvider = identityProvider;
        _userService = userService;
        _intentionManager = intentionManager;
        _securityManager = securityManager;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _mapper = mapper;
        _pollRepository = pollRepository;
        _publicIdService = publicIdService;
        _imageProcessing = imageProcessing;
        _s3Client = s3Client;
        _cdnConfig = cdnOptions.Value;
    }

    /// <summary>
    /// Прогон seed-картинки через реальный image-pipeline:
    /// magic-byte → EXIF-strip → resize → WebP thumbnails → S3 PUT всех 3
    /// объектов с Cache-Control: immutable. Возвращает Upload entity, готовую
    /// к Add() в DbContext.
    /// </summary>
    private async Task<DM.Infrastructure.Persistence.Entities.Shared.Upload> SeedAvatarFromBytesAsync(
        byte[] imageBytes,
        string declaredContentType,
        string sourceFileName,
        UploadType type,
        Guid uploadId,
        Guid userId,
        Guid? entityId,
        DateTimeOffset now)
    {
        // Тот же pipeline, что у пользовательских upload'ов: magic-byte
        // валидация, EXIF strip, downscale до 1024 px. Один source-файл —
        // imgproxy сделает thumbnails на лету при serving.
        ProcessedImage processed;
        await using (var input = new MemoryStream(imageBytes, writable: false))
        {
            processed = await _imageProcessing.ProcessAsync(input, declaredContentType);
        }

        var folder = type switch
        {
            UploadType.UserAvatar => "avatars",
            UploadType.CharacterAvatar => "characters",
            _ => "misc",
        };
        var objectKey = string.IsNullOrEmpty(_cdnConfig.Folder)
            ? $"{folder}/{userId:N}_{uploadId:N}{processed.Extension}"
            : $"{_cdnConfig.Folder}/{folder}/{userId:N}_{uploadId:N}{processed.Extension}";

        await PutSeedObjectAsync(objectKey, processed.Bytes, processed.ContentType);

        var upload = new DM.Infrastructure.Persistence.Entities.Shared.Upload
        {
            UploadId = uploadId,
            CreatedUtc = now,
            ConfirmedUtc = now,
            UserId = userId,
            Type = type,
            Status = UploadStatus.Confirmed,
            Original = true,
            ContentType = processed.ContentType,
            SizeBytes = processed.Bytes.LongLength,
            ObjectKey = objectKey,
            FilePath = BuildPublicUrl(objectKey),
            FileName = sourceFileName,
            IsRemoved = false,
        };
        switch (type)
        {
            case UploadType.UserAvatar: upload.TargetUserId = entityId; break;
            case UploadType.CharacterAvatar: upload.TargetCharacterId = entityId; break;
            case UploadType.PostAttachment: upload.TargetPostId = entityId; break;
        }
        return upload;
    }

    private async Task PutSeedObjectAsync(string objectKey, byte[] bytes, string contentType)
    {
        await _s3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = _cdnConfig.BucketName,
            Key = objectKey,
            InputStream = new MemoryStream(bytes, writable: false),
            ContentType = contentType,
            Headers = { CacheControl = "public, max-age=31536000, immutable" },
        });
    }

    private string BuildPublicUrl(string objectKey) =>
        new UriBuilder(new Uri(_cdnConfig.PublicUrl))
        {
            Path = $"{_cdnConfig.BucketName}/{objectKey}",
        }.ToString();

    /// <inheritdoc />
    public async Task SetRole(UserRole role)
    {
        var userId = _identityProvider.Current.User.UserId;
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user != null)
        {
            user.Role = role;
            // Invariant: honorary is mutually exclusive with active staff
            // roles. Moving INTO staff clears the title; moving OUT of staff
            // doesn't auto-grant honorary (that requires an explicit action).
            if (IsStaffRole(role))
            {
                user.IsHonorary = false;
            }
            await _dbContext.SaveChangesAsync();
        }
    }

    private static bool IsStaffRole(UserRole role) =>
        role is UserRole.Admin
            or UserRole.SeniorModerator
            or UserRole.Moderator
            or UserRole.Mentor;

    /// <summary>
    /// Прочитать UTF-8-текст из embedded-ресурса сборки. Бросает, если
    /// ресурс с таким именем не зарегистрирован — это setup-баг (забыли
    /// <c>&lt;EmbeddedResource&gt;</c> в csproj), он должен ронять seed
    /// громко, а не тихо записывать пустую строку в Info.
    /// </summary>
    private static async Task<string> LoadEmbeddedTextAsync(string resourceName)
    {
        var assembly = typeof(ModerationApiService).Assembly;
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found — check DM.Web.API.csproj <EmbeddedResource> entry.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TestAccountInfo>> GetAllUsers()
    {
        // Note: This endpoint is development-only (controller enforces this)
        // Password field is no longer populated for security
        var users = await _dbContext.Users
            .OrderByDescending(u => u.Role)
            .ThenBy(u => u.Username)
            .Select(u => new TestAccountInfo
            {
                Username = u.Username,
                Password = null,
                Role = u.Role
            })
            .ToListAsync();

        return users;
    }

    /// <inheritdoc />
    public async Task<Envelope<UserProfile>> ModerateUserProfile(string login, ModerateProfile profile)
    {
        var user = await _userService.GetAsync(login);
        _intentionManager.ThrowIfForbidden(UserIntention.Moderate, user);

        var updateUser = new UpdateUser
        {
            Username = login,
            Info = profile.Info ?? string.Empty
        };

        var updatedUser = await _userService.UpdateAsync(updateUser);
        return new Envelope<UserProfile>(_mapper.Map<UserProfile>(updatedUser));
    }

    /// <inheritdoc />
    public async Task<SeedResult> SeedTestUsers()
    {
        const string defaultPassword = "Test123!";

        // Test accounts to create based on the testing plan
        // All users start with 0 posts (QuantityRating=0), so all are "newbie" status
        // Note: All records in Users table are fully activated. For pending activation testing, use PendingRegistration.
        //
        // Username policy (see docs/architecture/USERNAME_POLICY.md):
        // - Length: 2-20 characters
        // - Allowed: a-z A-Z а-я А-Я еЕ 0-9 _ - . space
        var testAccounts = new[]
        {
            // === All roles (one per role) ===
            new { Login = "SolohinLex", Email = "admin@test.local", Role = UserRole.Admin, IsHonorary = false },
            new { Login = "TestSeniorMod", Email = "seniormod@test.local", Role = UserRole.SeniorModerator, IsHonorary = false },
            new { Login = "TestModerator", Email = "mod@test.local", Role = UserRole.Moderator, IsHonorary = false },
            new { Login = "TestMentor", Email = "mentor@test.local", Role = UserRole.Mentor, IsHonorary = false },
            new { Login = "TestUser", Email = "user@test.local", Role = UserRole.RegularUser, IsHonorary = false },

            // === Edge cases: length ===
            new { Login = "Ян", Email = "yan@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // min length (2), cyrillic
            new { Login = "LongestLoginPossible", Email = "longest@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // max length (20)

            // === Edge cases: special characters ===
            new { Login = "Player_One", Email = "player1@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // underscore
            new { Login = "Player-Two", Email = "player2@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // hyphen
            new { Login = "Player.Three", Email = "player3@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // dot
            new { Login = "Player Four", Email = "player4@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // space

            // === Edge cases: cyrillic ===
            new { Login = "Игрок", Email = "igrok@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // cyrillic only
            new { Login = "Игрок_Один", Email = "igrok1@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // cyrillic + underscore
            new { Login = "Тест Елки", Email = "yolka@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // cyrillic + space + Е

            // === Special states ===
            new { Login = "TestHonorary", Email = "honorary@test.local", Role = UserRole.RegularUser, IsHonorary = true }, // honorary goblin

            // === Rating test cases ===
            new { Login = "RatingDisabled", Email = "rating-off@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // rating disabled (n/a)
            new { Login = "RatingPositive", Email = "rating-pos@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // positive rating
            new { Login = "RatingNegative", Email = "rating-neg@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // negative rating
            new { Login = "RatingZero", Email = "rating-zero@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // zero rating
            new { Login = "Experienced", Email = "experienced@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // 150 posts, not newbie

            // === Activity test cases ===
            new { Login = "OnlyReader", Email = "reader@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // reads games but never plays (0 gamesPlaying)
        };

        var result = new SeedResult();

        // Get existing logins to skip
        var existingLogins = await _dbContext.Users
            .Where(u => testAccounts.Select(a => a.Login.ToLower()).Contains(u.Username.ToLower()))
            .Select(u => u.Username.ToLower())
            .ToListAsync();

        var now = _dateTimeProvider.Now;

        foreach (var account in testAccounts)
        {
            if (existingLogins.Contains(account.Login.ToLower()))
            {
                result.Skipped++;
                result.SkippedUsernames.Add(account.Login);
                continue;
            }

            // Generate password hash
            var (hash, salt) = _securityManager.GeneratePassword(defaultPassword);

            // Set special rating values for test users
            var (ratingDisabled, qualityRating, quantityRating) = account.Login switch
            {
                "RatingDisabled" => (true, 0, 50),
                "RatingPositive" => (false, 25, 80),
                "RatingNegative" => (false, -10, 40),
                "RatingZero" => (false, 0, 30),
                "Experienced" => (false, 50, 150), // Not newbie (100+ posts)
                _ => (false, 0, 0)
            };

            // Set last activity for online/offline testing
            var lastActivity = account.Login switch
            {
                "RatingDisabled" => now.AddHours(-2), // offline (>10 min)
                "RatingNegative" => now.AddDays(-7), // offline long ago
                _ => now // online
            };

            // Vary registration dates for realistic testing (staff registered earlier, newbies later)
            var registeredUtc = account.Login switch
            {
                "SolohinLex" => now.AddYears(-11).AddDays(-Random.Shared.Next(0, 180)), // 11+ years (хватает на платину «Выслуга лет», T4 = 3650 дней)
                "TestSeniorMod" => now.AddYears(-4).AddDays(-Random.Shared.Next(0, 180)), // 4+ years ago
                "TestModerator" => now.AddYears(-3).AddDays(-Random.Shared.Next(0, 180)), // 3+ years ago
                "TestMentor" => now.AddYears(-2).AddDays(-Random.Shared.Next(0, 180)), // 2+ years ago
                "TestHonorary" => now.AddYears(-6).AddDays(-Random.Shared.Next(0, 180)), // 6+ years ago (veteran)
                "Experienced" => now.AddYears(-2).AddDays(-Random.Shared.Next(0, 365)), // 2+ years ago
                "OnlyReader" => now.AddMonths(-2).AddDays(-Random.Shared.Next(0, 30)), // Recent
                _ => now.AddMonths(-Random.Shared.Next(1, 24)).AddDays(-Random.Shared.Next(0, 30)) // 1-24 months ago
            };

            var user = new DbUser
            {
                UserId = _guidFactory.Create(),
                Username = account.Login,
                Email = account.Email.ToLowerInvariant(),
                CreatedUtc = registeredUtc,
                LastActivityUtc = lastActivity,
                Role = account.Role,
                AccessPolicy = AccessPolicy.NotSpecified,
                Salt = salt,
                PasswordHash = hash,
                PasswordHashVersion = 4, // Argon2id
                IsRemoved = false,
                IsHonorary = account.IsHonorary,
                RatingDisabled = ratingDisabled,
                QualityRating = qualityRating,
                QuantityRating = quantityRating,
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
            };

            _dbContext.Users.Add(user);
            result.Created++;
            result.CreatedUsernames.Add(account.Login);
        }

        // Create pending registration for testing PendingActivation flow
        var pendingEmail = "inactive@test.local";
        var pendingExists = await _dbContext.PendingRegistrations
            .AnyAsync(p => p.Email == pendingEmail);

        if (!pendingExists)
        {
            var (hash, salt) = _securityManager.GeneratePassword(defaultPassword);
            var pending = new DM.Infrastructure.Persistence.Entities.Account.PendingRegistration
            {
                PendingRegistrationId = _guidFactory.Create(),
                TokenId = _guidFactory.Create(),
                Email = pendingEmail,
                PasswordHash = hash,
                Salt = salt,
                PasswordHashVersion = 4, // Argon2id
                CreatedUtc = now,
                TokenCreatedUtc = now,
                AcceptedRules = true
            };
            _dbContext.PendingRegistrations.Add(pending);
            result.Created++;
            result.CreatedUsernames.Add("(pending) inactive@test.local");
        }
        else
        {
            result.Skipped++;
            result.SkippedUsernames.Add("(pending) inactive@test.local");
        }

        if (result.Created > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<ComprehensiveSeedResult> SeedComprehensiveData()
    {
        var result = new ComprehensiveSeedResult();
        var now = _dateTimeProvider.Now;

        // Get all users for seeding
        var users = await _dbContext.Users
            .Where(u => !u.IsRemoved && u.Role != UserRole.System)
            .OrderByDescending(u => u.Role)
            .ToListAsync();

        if (users.Count < 5)
        {
            result.Details.Add("Not enough users. Run SeedTestUsers first.");
            return result;
        }

        // Categorize users by role
        var admin = users.FirstOrDefault(u => u.Role == UserRole.Admin);
        var seniorMod = users.FirstOrDefault(u => u.Role == UserRole.SeniorModerator);
        var moderator = users.FirstOrDefault(u => u.Role == UserRole.Moderator);
        var mentor = users.FirstOrDefault(u => u.Role == UserRole.Mentor);
        var regularUsers = users.Where(u => u.Role == UserRole.RegularUser).ToList();

        if (admin == null || seniorMod == null || regularUsers.Count < 3)
        {
            result.Details.Add("Missing required roles. Need Admin, SeniorMod, and at least 3 regular users.");
            return result;
        }

        // ═══════════════════════════════════════════════════════════════════
        // 1. UPDATE USER PROFILES
        // ═══════════════════════════════════════════════════════════════════
        await UpdateUserProfiles(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 2. CREATE FORUM TOPICS AND COMMENTS
        // ═══════════════════════════════════════════════════════════════════
        await CreateForumContent(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 2b. ASSIGN BOARD MODERATORS
        // ═══════════════════════════════════════════════════════════════════
        await AssignBoardModerators(users, result);

        // ═══════════════════════════════════════════════════════════════════
        // 3. CREATE GAMES WITH ALL VARIATIONS
        // ═══════════════════════════════════════════════════════════════════
        var gameIds = await CreateGames(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 4. CREATE BLOGS
        // ═══════════════════════════════════════════════════════════════════
        await CreateBlogs(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 5. CREATE GLOBAL CHAT AND MESSAGES
        // ═══════════════════════════════════════════════════════════════════
        await CreateGlobalChatMessages(users, now, result);
        await CreateGlobalChatEvent(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 6. CREATE REVIEWS
        // ═══════════════════════════════════════════════════════════════════
        await CreateReviews(users, gameIds, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 7. CREATE USER SUBSCRIPTIONS
        // ═══════════════════════════════════════════════════════════════════
        await CreateUserSubscriptions(users, result);

        // ═══════════════════════════════════════════════════════════════════
        // 8. CREATE POLLS (MongoDB)
        // ═══════════════════════════════════════════════════════════════════
        await CreatePolls(users, now, result);

        await _dbContext.SaveChangesAsync();

        // ═══════════════════════════════════════════════════════════════════
        // 9. CREATE LIKES FOR ALL CONTENT
        // ═══════════════════════════════════════════════════════════════════
        await CreateLikes(users, result);

        // ═══════════════════════════════════════════════════════════════════
        // 10. UPDATE LastCommentId REFERENCES
        // ═══════════════════════════════════════════════════════════════════
        await UpdateLastCommentReferences();

        // ═══════════════════════════════════════════════════════════════════
        // 11. UPDATE POPULARITY SCORES (for sidebar sorting)
        // ═══════════════════════════════════════════════════════════════════
        await UpdatePopularityScores(now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 12. SOLOHIN-LEX SPECIFIC: username history + avatar
        // ═══════════════════════════════════════════════════════════════════
        await SetupSolohinLexProfile(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 13. RECOMPUTE USER RATINGS FROM ACTUAL DATA
        // QualityRating/QuantityRating must reflect actual PostReviews and
        // Posts — the hardcoded values from UpdateUserProfiles are just
        // placeholders, the real numbers come from the joins below.
        // ═══════════════════════════════════════════════════════════════════
        await RecomputeUserRatings(result);

        // ═══════════════════════════════════════════════════════════════════
        // 14. BOOST SOLOHIN-LEX METRICS FOR FULL ACHIEVEMENT TIER COVERAGE
        // Поднимаем счетчики, чтобы на профиле SolohinLex были все 5 видов
        // плашек: locked / bronze / silver / gold / platinum. Платина уже
        // есть от 11-летней регистрации; gold/silver требуют поднять
        // QuantityRating и QualityRating (все остальное уже на bronze /
        // locked из реальных данных).
        // ═══════════════════════════════════════════════════════════════════
        await BoostSolohinLexMetrics(result);

        // ═══════════════════════════════════════════════════════════════════
        // 15. SEED SOLOHIN-LEX BANS FOR «РЕЗИНОВАЯ УТОЧКА» DEMO
        // Цепочка BansReceived требует фактических Ban-записей в БД (а не
        // денормализованного счетчика), поэтому инсертим 10 штук — gold tier,
        // gold/silver/bronze тиры заработаны, platinum (T4=30) остается
        // locked. Параллельно с гейм-постами/рейтингом дает полную палитру
        // tier-плашек на странице достижений.
        // ═══════════════════════════════════════════════════════════════════
        await SeedSolohinLexBans(users, now, result);

        // ═══════════════════════════════════════════════════════════════════
        // 16. SEED SOLOHIN-LEX DROPS FOR «ДРОПЫ» DEMO
        // Цепочка GameDrops читает Characters.Status=Retired+IsPlayerLeft,
        // поэтому надо инсертить именно персонажей. 10 штук → gold tier,
        // platinum (T4=30) locked.
        // ═══════════════════════════════════════════════════════════════════
        await SeedSolohinLexDrops(users, now, result);

        result.Details.Add($"Comprehensive seed completed at {now:yyyy-MM-dd HH:mm:ss}");
        return result;
    }

    /// <summary>
    /// Создает 10 персонажей-«дропов» для SolohinLex — Retired со
    /// <see cref="DM.Infrastructure.Persistence.Entities.Game.Characters.Character.IsPlayerLeft"/>=true
    /// в существующих играх. Идемпотентно: если уже 10+ дропов, ничего не делает.
    /// Берем произвольные игры (не созданные SolohinLex-ом как мастер, чтобы
    /// не нарушать логику), без проверки уникальности «один игрок — одна
    /// активная роль в игре»: статус Retired исключает конкуренцию с Active.
    /// </summary>
    private async Task SeedSolohinLexDrops(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        if (solohin == null)
        {
            result.Details.Add("SolohinLex drops skipped: target user not found");
            return;
        }

        var existing = await _dbContext.Set<Character>()
            .Where(c => c.AuthorId == solohin.UserId
                && c.Status == CharacterStatus.Retired
                && c.IsPlayerLeft
                && !c.IsRemoved)
            .CountAsync();
        if (existing >= 10)
        {
            result.Details.Add($"SolohinLex already has {existing} drops, skipping drop seed");
            return;
        }

        const int targetCount = 10;
        var toCreate = targetCount - existing;

        // Берем существующие игры из ChangeTracker (созданные в шаге 6) —
        // удобнее, чем повторный SELECT; и эти игры точно валидны.
        var availableGames = _dbContext.ChangeTracker.Entries<DbGame>()
            .Select(e => e.Entity)
            .Where(g => !g.IsRemoved && g.MasterId != solohin.UserId)
            .Take(toCreate * 2)
            .ToList();

        if (availableGames.Count < toCreate)
        {
            // Fallback: подбираем из БД (на случай если ChangeTracker пуст,
            // например после рестарта между seed-шагами).
            availableGames = await _dbContext.Set<DbGame>()
                .Where(g => !g.IsRemoved && g.MasterId != solohin.UserId)
                .Take(toCreate * 2)
                .ToListAsync();
        }

        if (availableGames.Count == 0)
        {
            result.Details.Add("SolohinLex drops skipped: no eligible games");
            return;
        }

        var characterNames = new[]
        {
            "Алхимик", "Лучник", "Странствующий бард", "Бывший рыцарь",
            "Чародей-отшельник", "Авантюрист", "Наемник", "Картограф",
            "Бывший монах", "Путник",
        };
        var races = new[] { "человек", "эльф", "гном", "полуэльф", "тифлинг" };
        var classes = new[] { "воин", "маг", "следопыт", "бард", "плут" };

        for (var i = 0; i < toCreate; i++)
        {
            var game = availableGames[i % availableGames.Count];
            var createdAgo = (existing + i + 1) * 45;
            _dbContext.Set<Character>().Add(new Character
            {
                CharacterId = _guidFactory.Create(),
                GameId = game.GameId,
                AuthorId = solohin.UserId,
                Status = CharacterStatus.Retired,
                IsDead = false,
                IsPlayerLeft = true,
                IsPlayerExiled = false,
                CreatedUtc = now.AddDays(-createdAgo),
                Name = characterNames[(existing + i) % characterNames.Length],
                Race = races[(existing + i) % races.Length],
                Class = classes[(existing + i) % classes.Length],
                Story = "Покинул игру по личным обстоятельствам.",
                IsNpc = false,
                AccessPolicy = CharacterAccessPolicy.NoAccess,
                IsRemoved = false,
            });
        }
        await _dbContext.SaveChangesAsync();
        result.Details.Add($"SolohinLex drops seeded: {toCreate} created (total {targetCount}) for «дропы» demo");
    }

    /// <summary>
    /// Сидим 10 банов для SolohinLex (target=SolohinLex, author=TestModerator)
    /// — три тира цепочки «резиновая уточка» зарабатываются (BANS_1, BANS_3,
    /// BANS_10), четвертый (BANS_30) остается locked. Баны прошедшие (Ended
    /// в прошлом), <c>IsVoluntary=true</c>, AccessRestrictionPolicy не задается —
    /// исторические записи, не активные ограничения. Идемпотентно: при
    /// повторном seed не создает дубли.
    /// </summary>
    private async Task SeedSolohinLexBans(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        var author = users.FirstOrDefault(u => u.Username == "TestModerator")
                   ?? users.FirstOrDefault(u => u.Username == "TestSeniorMod");
        if (solohin == null || author == null)
        {
            result.Details.Add("SolohinLex bans skipped: target or author user not found");
            return;
        }

        var existing = await _dbContext.Set<Ban>()
            .Where(b => b.TargetUserId == solohin.UserId)
            .CountAsync();
        if (existing >= 10)
        {
            result.Details.Add($"SolohinLex already has {existing} bans, skipping ban seed");
            return;
        }

        const int targetCount = 10;
        var toCreate = targetCount - existing;
        var reasons = new[]
        {
            "Спам в глобальном чате",
            "Оскорбление участников",
            "Нарушение правил конкурса",
            "Флуд в форумной теме",
            "Подозрительная активность",
            "Эксплуатация багов",
            "Нарушение правил игры",
            "Многократные жалобы",
            "Нарушение этикета",
            "Тестовый бан (демо)",
        };
        for (var i = 0; i < toCreate; i++)
        {
            var ago = (existing + i + 1) * 30; // равномерно по последним месяцам
            _dbContext.Set<Ban>().Add(new Ban
            {
                BanId = Guid.NewGuid(),
                TargetUserId = solohin.UserId,
                AuthorId = author.UserId,
                StartedUtc = now.AddDays(-ago),
                EndedUtc = now.AddDays(-ago + 7),
                Comment = reasons[(existing + i) % reasons.Length],
                AccessRestrictionPolicy = AccessPolicy.NotSpecified,
                IsVoluntary = true,
                IsRemoved = false,
            });
        }
        await _dbContext.SaveChangesAsync();
        result.Details.Add($"SolohinLex bans seeded: {toCreate} created (total {targetCount}) for «резиновая уточка» demo");
    }

    /// <summary>
    /// Поднимаем QuantityRating и QualityRating SolohinLex'а так, чтобы он
    /// демонстрировал все 5 состояний tier-плашек на странице достижений.
    /// Запускается ПОСЛЕ <see cref="RecomputeUserRatings"/>, иначе пересчет
    /// затрет наши значения.
    /// </summary>
    private async Task BoostSolohinLexMetrics(ComprehensiveSeedResult result)
    {
        // Game posts: 2200 → gold (T1=100, T2=500, T3=2000 взяты; T4=5000 — нет).
        // Rating:      350  → silver (T1=100, T2=250 взяты; T3=500, T4=1000 — нет).
        await _dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE "Users"
            SET "QuantityRating" = 2200, "QualityRating" = 350
            WHERE "Username" = 'SolohinLex';
        """);
        result.Details.Add("SolohinLex metrics boosted for full tier coverage (posts=2200, rating=350)");
    }

    private async Task RecomputeUserRatings(ComprehensiveSeedResult result)
    {
        await _dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE "Users" u
            SET "QualityRating" = COALESCE(s.sum_signs, 0)
            FROM (
                SELECT "Users"."UserId" AS user_id,
                       (SELECT COALESCE(SUM(r."SignValue"), 0)
                        FROM "PostReviews" r
                        JOIN "Posts" p ON p."PostId" = r."PostId"
                        WHERE r."IsRemoved" = false
                          AND p."IsRemoved" = false
                          AND p."AuthorId" = "Users"."UserId") AS sum_signs
                FROM "Users"
            ) s
            WHERE u."UserId" = s.user_id;
        """);

        await _dbContext.Database.ExecuteSqlRawAsync("""
            UPDATE "Users" u
            SET "QuantityRating" = COALESCE(s.cnt, 0)
            FROM (
                SELECT "Users"."UserId" AS user_id,
                       (SELECT COUNT(*)
                        FROM "Posts" p
                        WHERE p."IsRemoved" = false
                          AND p."AuthorId" = "Users"."UserId") AS cnt
                FROM "Users"
            ) s
            WHERE u."UserId" = s.user_id;
        """);

        result.Details.Add("User ratings recomputed from actual PostReviews and Posts");
    }

    /// <summary>
    /// SolohinLex (admin): backfill username change history and upload an avatar
    /// so the test admin profile has the same look-and-feel as a real veteran user.
    /// </summary>
    private async Task SetupSolohinLexProfile(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var solohin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        if (solohin == null)
        {
            result.Details.Add("SolohinLex not found, skipping profile setup");
            return;
        }

        // --- Username history ---
        // Always rebuild so re-seeds produce a deterministic history.
        var existingHistory = await _dbContext.UsernameHistories
            .Where(h => h.UserId == solohin.UserId)
            .ToListAsync();
        if (existingHistory.Count > 0)
        {
            _dbContext.UsernameHistories.RemoveRange(existingHistory);
        }

        // Three historical renames, oldest first: Lex (2020) → Solohin (2022) → AlexSolohin (2024) → SolohinLex (current)
        var historyEntries = new (string Old, string New, DateTimeOffset When)[]
        {
            ("Lex",          "Solohin",      now.AddYears(-4)),
            ("Solohin",      "AlexSolohin",  now.AddYears(-2)),
            ("AlexSolohin",  "SolohinLex",   now.AddMonths(-6)),
        };
        foreach (var (oldName, newName, when) in historyEntries)
        {
            _dbContext.UsernameHistories.Add(new DbUsernameHistory
            {
                UsernameHistoryId = _guidFactory.Create(),
                UserId = solohin.UserId,
                OldUsername = oldName,
                NewUsername = newName,
                ChangedUtc = when,
                ApprovedByUserId = solohin.UserId,
            });
        }

        // --- "О себе" (Info, BBCode) ---
        // Idempotent: always assign so re-seeds produce a known text.
        // BBCode источник лежит в Assets/Seed/SolohinLex.bbcode и
        // встроен в сборку как EmbeddedResource — единственная копия
        // на весь проект (SSOT). Раньше дублировался в raw string
        // здесь + scripts/solohin-info.bbcode + scripts/update-solohin-info.sql;
        // оба внешних места убраны, ручной psql-апдейт больше не нужен.
        solohin.Info = await LoadEmbeddedTextAsync("DM.Web.API.Assets.Seed.SolohinLex.bbcode");

        // --- Contacts ---
        // Same idempotency rule as username history: wipe and rebuild so re-seeds
        // converge on a known state regardless of prior runs.
        var existingContacts = await _dbContext.Set<DbUserContact>()
            .Where(c => c.UserId == solohin.UserId)
            .ToListAsync();
        if (existingContacts.Count > 0)
        {
            _dbContext.Set<DbUserContact>().RemoveRange(existingContacts);
        }
        var contactEntries = new (string Type, string Value)[]
        {
            ("Telegram", "@solohin_lex"),
            ("Discord",  "solohinlex"),
        };
        for (var i = 0; i < contactEntries.Length; i++)
        {
            var (type, value) = contactEntries[i];
            _dbContext.Set<DbUserContact>().Add(new DbUserContact
            {
                UserContactId = _guidFactory.Create(),
                UserId = solohin.UserId,
                ContactType = type,
                ContactValue = value,
                SortOrder = i,
            });
        }

        // --- Endorsements (Рекомендации) ---
        // Wipe-and-rebuild so re-seeds are deterministic. Endorsers are
        // looked up by username — if a username doesn't exist in this seed
        // run, that one entry is silently skipped.
        var existingEndorsements = await _dbContext.UserEndorsements
            .Where(e => e.TargetUserId == solohin.UserId)
            .ToListAsync();
        if (existingEndorsements.Count > 0)
        {
            _dbContext.UserEndorsements.RemoveRange(existingEndorsements);
        }
        var endorsementEntries = new (string Username, int DaysAgo, string Text)[]
        {
            ("TestSeniorMod", 180, "Веду игры с Лексом уже несколько лет. Адекватный мастер, посты пишет регулярно, конфликты разруливает спокойно. Рекомендую как мастера и как игрока."),
            ("TestModerator", 90,  "Один из тех, кто реально читает правила перед тем как жаловаться. Помогает с модерацией постов и тегов, не флудит в чате."),
            ("TestMentor",    60,  "Играл у него в Звездном Крейсере. Мастер с богатым внутренним миром — описания читаются как роман, NPC живые. Если попадетесь на его игру — соглашайтесь."),
            ("Experienced",   30,  "Лекс из тех редких людей, которые админят сайт и [b]не зазнаются[/b]. Доступен в Discord, отвечает быстро."),
            ("TestHonorary",  14,  "Знаком с ним еще с DM2. Принципиально не банит за политику, разбирается с каждым случаем индивидуально. Уважаю."),
        };
        foreach (var (authorUsername, daysAgo, text) in endorsementEntries)
        {
            var author = users.FirstOrDefault(u => u.Username == authorUsername);
            if (author == null) continue;
            _dbContext.UserEndorsements.Add(new UserEndorsement
            {
                UserEndorsementId = _guidFactory.Create(),
                AuthorId = author.UserId,
                TargetUserId = solohin.UserId,
                CreatedUtc = now.AddDays(-daysAgo),
                Text = text,
                IsRemoved = false,
            });
        }

        // --- Awards ---
        // Демо-история SolohinLex'а в конкурсах: рассказывает прогрессию
        // 2022 → 2024 (от середины турнирной таблицы до Гран-При). Правила:
        //   - В одной серии — максимум одно место (1/2/3 взаимоисключающие).
        //   - Спец-награды (Народное / Критик / Угадайка) выдаются отдельно
        //     по решению жюри/мини-игры и могут сочетаться с местом.
        //   - Дата выдачи привязана к (сезон, год) серии, а не к now,
        //     чтобы порядок сортировки был естественным.
        // Wipe-and-rebuild, как остальные демо-блоки.
        var existingAwards = await _dbContext.UserAwards
            .Where(a => a.UserId == solohin.UserId)
            .ToListAsync();
        if (existingAwards.Count > 0)
        {
            _dbContext.UserAwards.RemoveRange(existingAwards);
        }
        var lit23 = new Guid("00000000-0000-0000-0004-000000000001"); // 23-й литературный конкурс 2024
        var lit22 = new Guid("00000000-0000-0000-0004-000000000002"); // 22-й литературный конкурс 2023
        var lit20 = new Guid("00000000-0000-0000-0004-000000000004"); // 20-й литературный конкурс 2022
        var art2  = new Guid("00000000-0000-0000-0004-000000000005"); // 2-й арт конкурс 2024
        var art1  = new Guid("00000000-0000-0000-0004-000000000006"); // 1-й арт конкурс 2023
        var contestFirst   = new Guid("00000000-0000-0000-0001-000000000001");
        var contestSecond  = new Guid("00000000-0000-0000-0001-000000000002");
        var contestThird   = new Guid("00000000-0000-0000-0001-000000000003");
        var popularVote    = new Guid("00000000-0000-0000-0001-000000000004");
        var bestCritic     = new Guid("00000000-0000-0000-0001-000000000005");
        var guesser        = new Guid("00000000-0000-0000-0001-000000000006");
        // Хронология SolohinLex'а — лит-карьера 2022-2024 + участие в арт-конкурсах.
        // WorkUrl — placeholder-топик с самой работой (для мест + народного
        // признания). best_critic / guesser не привязаны к конкретной работе.
        const string sampleWork = "https://dm.am/forum/topic/sample-work-";
        var demoAwards = new (Guid SeriesId, Guid AwardTypeId, DateTimeOffset At, string? WorkUrl)[]
        {
            (lit20, contestThird,  new DateTimeOffset(2022, 9,  1, 12, 0, 0, TimeSpan.Zero), sampleWork + "lit20-3"),
            (lit20, guesser,       new DateTimeOffset(2022, 9,  1, 12, 0, 0, TimeSpan.Zero), null),
            (lit22, contestSecond, new DateTimeOffset(2023, 3,  1, 12, 0, 0, TimeSpan.Zero), sampleWork + "lit22-2"),
            (art1,  bestCritic,    new DateTimeOffset(2023, 9,  1, 12, 0, 0, TimeSpan.Zero), null),
            (lit23, contestFirst,  new DateTimeOffset(2024, 3,  1, 12, 0, 0, TimeSpan.Zero), sampleWork + "lit23-1"),
            (lit23, popularVote,   new DateTimeOffset(2024, 3,  1, 12, 0, 0, TimeSpan.Zero), sampleWork + "lit23-1"),
            (art2,  contestSecond, new DateTimeOffset(2024, 9,  1, 12, 0, 0, TimeSpan.Zero), sampleWork + "art2-2"),
        };
        foreach (var (seriesId, awardTypeId, at, workUrl) in demoAwards)
        {
            _dbContext.UserAwards.Add(new DM.Infrastructure.Persistence.Entities.Community.UserAward
            {
                UserAwardId = _guidFactory.Create(),
                UserId = solohin.UserId,
                AwardTypeId = awardTypeId,
                ContestSeriesId = seriesId,
                WorkUrl = workUrl,
                AwardedUtc = at,
                AwardedByUserId = solohin.UserId, // self-grant в сидере; в проде — admin/seniormod
                IsRemoved = false,
            });
        }
        result.Details.Add($"SolohinLex awards: rebuilt {demoAwards.Length} across 5 contest series (2022-2024)");

        // --- Avatar upload ---
        // Idempotent: skip if SolohinLex already has an avatar wired up.
        if (solohin.AvatarUploadId == null)
        {
            try
            {
                var assembly = typeof(ModerationApiService).Assembly;
                const string resourceName = "DM.Web.API.Assets.Seed.SolohinLex.jpg";

                await using var resourceStream = assembly.GetManifestResourceStream(resourceName);
                if (resourceStream != null)
                {
                    using var ms = new MemoryStream();
                    await resourceStream.CopyToAsync(ms);
                    var imageBytes = ms.ToArray();

                    var uploadId = _guidFactory.Create();
                    var upload = await SeedAvatarFromBytesAsync(
                        imageBytes,
                        declaredContentType: "image/jpeg",
                        sourceFileName: "SolohinLex.jpg",
                        type: UploadType.UserAvatar,
                        uploadId: uploadId,
                        userId: solohin.UserId,
                        entityId: solohin.UserId,
                        now: now);
                    _dbContext.Set<DM.Infrastructure.Persistence.Entities.Shared.Upload>().Add(upload);
                    solohin.AvatarUploadId = uploadId;
                }
                else
                {
                    result.Details.Add("SolohinLex avatar resource not found (Assets/Seed/SolohinLex.jpg), skipping avatar");
                }
            }
            catch (Exception ex)
            {
                result.Details.Add($"SolohinLex avatar upload failed: {ex.Message}");
            }
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add(
            $"SolohinLex profile set up: {historyEntries.Length} username history entries, " +
            $"{contactEntries.Length} contacts, {endorsementEntries.Length} endorsements, " +
            $"info=yes, avatar={(solohin.AvatarUploadId != null ? "yes" : "no")}");
    }

    private async Task UpdateLastCommentReferences()
    {
        // NOTE: Games and Publications comments are skipped because Comment.EntityId has FK to Topics only
        // Only Topics have comments in the current schema

        // Update Chats with messages (Global Chat)
        var globalChatId = Chat.GlobalChatId;
        var globalChat = await _dbContext.Set<Chat>().FirstOrDefaultAsync(c => c.ChatId == globalChatId);
        if (globalChat != null && globalChat.LastMessageId == null)
        {
            var lastMessage = await _dbContext.Set<Message>()
                .Where(m => m.ChatId == globalChatId)
                .OrderByDescending(m => m.CreatedUtc)
                .FirstOrDefaultAsync();

            if (lastMessage != null)
            {
                globalChat.LastMessageId = lastMessage.MessageId;
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task CreateLikes(List<DbUser> users, ComprehensiveSeedResult result)
    {
        // Check if likes already exist
        var existingLikesCount = await _dbContext.Set<Like>().CountAsync();
        if (existingLikesCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Likes already exist ({existingLikesCount}), skipping");
            return;
        }

        var likesCreated = 0;

        // Helper to add random likes to an entity
        void AddLikesForEntity(Guid entityId, LikeEntityType entityType, Guid? authorId)
        {
            // Each entity gets 0-5 likes from random users (excluding author)
            var likeCount = Random.Shared.Next(0, 6);
            var eligibleUsers = authorId.HasValue
                ? users.Where(u => u.UserId != authorId.Value).ToList()
                : users;

            var likers = eligibleUsers
                .OrderBy(_ => Random.Shared.Next())
                .Take(likeCount)
                .ToList();

            foreach (var liker in likers)
            {
                _dbContext.Set<Like>().Add(new Like
                {
                    LikeId = _guidFactory.Create(),
                    EntityId = entityId,
                    EntityType = entityType,
                    UserId = liker.UserId,
                    IsRemoved = false
                });
                likesCreated++;
            }
        }

        // 1. Likes for forum topics
        var topics = await _dbContext.Set<Topic>()
            .Where(t => !t.IsRemoved)
            .Select(t => new { t.TopicId, t.AuthorId })
            .ToListAsync();
        foreach (var topic in topics)
        {
            AddLikesForEntity(topic.TopicId, LikeEntityType.Topic, topic.AuthorId);
        }

        // 2. Likes for forum/publication comments
        var comments = await _dbContext.Set<DbComment>()
            .Where(c => !c.IsRemoved)
            .Select(c => new { c.CommentId, c.AuthorId })
            .ToListAsync();
        foreach (var comment in comments)
        {
            AddLikesForEntity(comment.CommentId, LikeEntityType.Comment, comment.AuthorId);
        }

        // 3. Likes for publications
        var publications = await _dbContext.Set<Publication>()
            .Where(p => !p.IsRemoved && p.IsPublished)
            .Select(p => new { p.PublicationId, p.AuthorId })
            .ToListAsync();
        foreach (var publication in publications)
        {
            AddLikesForEntity(publication.PublicationId, LikeEntityType.Publication, publication.AuthorId);
        }

        // 4. Likes for global chat messages
        var globalChatId = Chat.GlobalChatId;
        var messages = await _dbContext.Set<Message>()
            .Where(m => !m.IsRemoved && m.ChatId == globalChatId)
            .Select(m => new { m.MessageId, m.UserId })
            .ToListAsync();
        foreach (var message in messages)
        {
            AddLikesForEntity(message.MessageId, LikeEntityType.Message, message.UserId);
        }

        // 5. Likes for post reviews (only entity that supports likes)
        var ratedPostReviews = await _dbContext.PostReviews
            .Where(r => !r.IsRemoved)
            .Select(r => new { r.PostReviewId, r.AuthorId })
            .ToListAsync();
        foreach (var review in ratedPostReviews)
        {
            AddLikesForEntity(review.PostReviewId, LikeEntityType.PostReview, review.AuthorId);
        }

        await _dbContext.SaveChangesAsync();

        result.LikesCreated = likesCreated;
        result.Details.Add($"Created {likesCreated} likes");
    }

    private async Task UpdatePopularityScores(DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var activeThreshold = now - TimeSpan.FromDays(30);

        // Update game popularity scores (active players + readers)
        var gameIds = await _dbContext.Set<DbGame>()
            .Where(g => !g.IsRemoved && g.Status != ModuleStatus.Draft)
            .Select(g => g.GameId)
            .ToListAsync();

        if (gameIds.Count > 0)
        {
            var playerCounts = await _dbContext.Set<Character>()
                .Include(c => c.Author)
                .Where(c => gameIds.Contains(c.GameId) &&
                           c.Status == CharacterStatus.Active &&
                           !c.IsNpc &&
                           c.AuthorId.HasValue &&
                           c.Author != null &&
                           c.Author.LastActivityUtc.HasValue &&
                           c.Author.LastActivityUtc.Value > activeThreshold)
                .GroupBy(c => c.GameId)
                .Select(g => new { GameId = g.Key, Count = g.Select(c => c.AuthorId!.Value).Distinct().Count() })
                .ToDictionaryAsync(x => x.GameId, x => x.Count);

            var gameReaderCounts = await _dbContext.Set<Subscription>()
                .Where(s => s.TargetType == SubscriptionTargetType.Game &&
                           gameIds.Contains(s.TargetId) &&
                           s.Subscriber.LastActivityUtc.HasValue &&
                           s.Subscriber.LastActivityUtc.Value > activeThreshold)
                .GroupBy(s => s.TargetId)
                .Select(g => new { GameId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.GameId, x => x.Count);

            var games = await _dbContext.Set<DbGame>().Where(g => gameIds.Contains(g.GameId)).ToListAsync();
            foreach (var game in games)
            {
                playerCounts.TryGetValue(game.GameId, out var playerCount);
                gameReaderCounts.TryGetValue(game.GameId, out var readerCount);
                game.PopularityScore = playerCount + readerCount;
                game.PopularityScoreUpdatedUtc = now;
            }
        }

        // Update blog popularity scores (active readers)
        var blogIds = await _dbContext.Set<DbBlog>()
            .Where(b => !b.IsRemoved)
            .Select(b => b.BlogId)
            .ToListAsync();

        if (blogIds.Count > 0)
        {
            var blogReaderCounts = await _dbContext.Set<Subscription>()
                .Where(s => s.TargetType == SubscriptionTargetType.Blog &&
                           blogIds.Contains(s.TargetId) &&
                           s.Subscriber.LastActivityUtc.HasValue &&
                           s.Subscriber.LastActivityUtc.Value > activeThreshold)
                .GroupBy(s => s.TargetId)
                .Select(g => new { BlogId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.BlogId, x => x.Count);

            var blogs = await _dbContext.Set<DbBlog>().Where(b => blogIds.Contains(b.BlogId)).ToListAsync();
            foreach (var blog in blogs)
            {
                blogReaderCounts.TryGetValue(blog.BlogId, out var readerCount);
                blog.PopularityScore = readerCount;
                blog.PopularityScoreUpdatedUtc = now;
            }
        }

        await _dbContext.SaveChangesAsync();
        result.Details.Add($"Updated popularity scores for {gameIds.Count} games and {blogIds.Count} blogs");
    }

    private Task UpdateUserProfiles(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var profiles = new (string Username, string Name, string Status, string Location, string Info, Gender Gender, bool IsHonorary, int QualityRating, int QuantityRating)[]
        {
            ("SolohinLex", "Алексей Солохин", "Слежу за порядком", "Москва", "Администратор сайта с многолетним опытом. Отвечаю за техническую часть и модерацию.", Gender.Male, false, 500, 1500),
            ("TestSeniorMod", "Старший Модератор", "На страже правил", "Санкт-Петербург", "Помогаю поддерживать дружелюбную атмосферу на сайте. Обращайтесь с вопросами!", Gender.Female, false, 350, 800),
            ("TestModerator", "Модератор Форума", "Читаю все", "Новосибирск", "Модерирую форум и помогаю новичкам освоиться.", Gender.Male, false, 200, 400),
            ("TestMentor", "Опытный Наставник", "Учу мастерству", "Екатеринбург", "Ментор для начинающих мастеров. Провожу игры уже 10 лет.", Gender.Male, false, 450, 1200),
            ("TestUser", "Активный Игрок", "Ищу новые приключения", "Казань", "Люблю фэнтези и sci-fi. Играю за воинов и магов.", Gender.Male, false, 150, 300),
            ("TestHonorary", "Почетный Гоблин", "Ветеран сообщества", "Нижний Новгород", "Один из первых участников сайта. Почетный гоблин с 2015 года.", Gender.Male, true, 600, 2000),
            ("Ян", "Ян", "Минималист", "Владивосток", "Краткость — сестра таланта.", Gender.Male, false, 50, 80),
            ("Player_One", "Игрок Первый", "Ready Player One", "Краснодар", "Геймер и ролевик. Люблю D&D 5e и Pathfinder.", Gender.Male, false, 120, 250),
            ("Player-Two", "Игрок Второй", "Второй не значит худший", "Самара", "Мастер интриг и политических игр.", Gender.Female, false, 180, 350),
            ("Player.Three", "Игрок Третий", "Точка — это стиль", "Ростов-на-Дону", "Специализируюсь на horror-играх и мистике.", Gender.Unknown, false, 90, 150),
            ("Player Four", "Игрок Четвертый", "Пробелы разрешены", "Воронеж", "Новичок, но учусь быстро!", Gender.Male, false, 30, 45),
            ("Игрок", "Русский Игрок", "Только кириллица", "Омск", "Предпочитаю русскоязычные игры.", Gender.Male, false, 100, 200),
            ("Игрок_Один", "Первый Русский", "Кириллица и символы", "Челябинск", "Люблю славянское фэнтези.", Gender.Female, false, 75, 120),
            ("Тест Елки", "Тестовая Елка", "С буквой Е", "Уфа", "Проверяю поддержку буквы Е в системе.", Gender.Unknown, false, 25, 40),
            ("LongestLoginPossible", "Длинное Имя", "Максимальная длина", "Пермь", "У меня самый длинный логин на сайте!", Gender.Male, false, 60, 100),
            ("OnlyReader", "Только Читатель", "Читаю, не играю", "Тула", "Люблю читать игры, но сам не участвую.", Gender.Male, false, 15, 50),
        };

        foreach (var profile in profiles)
        {
            var user = users.FirstOrDefault(u => u.Username == profile.Username);
            if (user != null && string.IsNullOrEmpty(user.Name))
            {
                user.Name = profile.Name;
                user.Status = profile.Status;
                user.Location = profile.Location;
                user.Info = profile.Info;
                user.Gender = profile.Gender;
                // Invariant: honorary is mutually exclusive with active
                // staff roles. Force false for current staff regardless of
                // what the profile literal said (see IsStaffRole above).
                user.IsHonorary = profile.IsHonorary && !IsStaffRole(user.Role);
                user.QualityRating = profile.QualityRating;
                user.QuantityRating = profile.QuantityRating;
                user.LastActivityUtc = now.AddMinutes(-Random.Shared.Next(1, 1440));
            }
        }

        result.Details.Add("User profiles updated");
        return Task.CompletedTask;
    }

    private async Task CreateForumContent(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // Well-known IDs for system topics created in migration (must be excluded from count)
        var systemTopicIds = new[]
        {
            Guid.Parse("00000000-0000-0000-0000-000000000001"), // "Отзывы о ДМ"
            Guid.Parse("00000000-0000-0000-0000-000000000100"), // "Обсуждение действий администрации"
        };

        // Check if user-created topics already exist (exclude system topics)
        var existingTopicsCount = await _dbContext.Set<Topic>()
            .CountAsync(t => !systemTopicIds.Contains(t.TopicId));
        if (existingTopicsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Forum topics already exist ({existingTopicsCount}), skipping");
            return;
        }

        var boards = await _dbContext.Set<Board>().OrderBy(b => b.Order).ToListAsync();
        if (boards.Count == 0)
        {
            result.Details.Add("No boards found, skipping forum content");
            return;
        }

        // Board-specific topic templates (keyed by board title)
        var boardTopics = new Dictionary<string, (string Title, string Text)[]>
        {
            ["Общий"] = new[]
            {
                ("Добро пожаловать на форум!", "Здесь вы можете обсудить все, что связано с текстовыми ролевыми играми. Соблюдайте правила и уважайте друг друга!"),
                ("Правила форума", "Основные правила: 1) Уважайте собеседников. 2) Не флудите. 3) Пишите по теме. 4) Не рекламируйте."),
                // Note: "Обсуждение действий администрации" is seeded in InitialCreate migration
                ("История банов — январь 2026", "Отчет о нарушителях за прошлый месяц. Всего выдано 3 предупреждения и 1 бан за спам."),
            },
            ["Игровые системы"] = new[]
            {
                ("Обсуждение системы D&D 5e", "Кто что думает о последних дополнениях? Мне нравится новый подкласс для варвара."),
                ("GURPS для новичков — с чего начать?", "Хочу попробовать GURPS, но система кажется сложной. Посоветуйте, какие книги читать в первую очередь?"),
                ("Сравнение Pathfinder 1e и 2e", "Играл долго в первую редакцию, теперь думаю перейти на вторую. Кто уже перешел — как впечатления?"),
            },
            ["Поиск мастера и игроков"] = new[]
            {
                ("Ищу игру в жанре фэнтези", "Привет всем! Ищу игру в классическом фэнтези-сеттинге. Предпочитаю D&D или словески. Опыт 5+ лет."),
                ("[Набор] Кампания по Forgotten Realms", "Набираю 4-5 игроков в длительную кампанию. Система D&D 5e. Начало — через 2 недели."),
                ("Ищу мастера для one-shot horror", "Хотим с друзьями (3 человека) сыграть короткую хоррор-игру. Система любая."),
            },
            ["Котел идей"] = new[]
            {
                ("Идея: Стимпанк в викторианской Англии", "Думаю запустить игру в стимпанк-сеттинге. Есть наработки по лору и системе. Интересно ли это кому-нибудь?"),
                ("Концепт: Космическая станция на краю галактики", "Хочу создать sci-fi игру про жизнь на изолированной станции. Ищу фидбек по идее."),
            },
            ["Конкурсы"] = new[]
            {
                ("Конкурс рассказов — весна 2026", "Объявляем ежегодный конкурс рассказов! Тема: 'Новые горизонты'. Дедлайн: 1 апреля."),
                ("Итоги конкурса персонажей", "Подводим итоги! Победитель — персонаж 'Эльвира Теневая' от Player_One. Поздравляем!"),
            },
            ["Под столом"] = new[]
            {
                ("Музыка для атмосферы игр", "Делитесь плейлистами! Для фэнтези использую саундтреки из Ведьмака и Скайрима."),
                ("Мемы DM — подборка за месяц", "Собрал лучшие мемы из наших игр. Осторожно, много смешного!"),
                ("Интервью после полуночи #42", "Новый выпуск! На этот раз гость — TestMentor, ветеран с 10-летним стажем."),
            },
            ["Неролевые игры"] = new[]
            {
                ("Игра в ассоциации", "Правила простые: пишем слово, ассоциирующееся с предыдущим. Начинаю: ДРАКОН"),
                ("Угадай персонажа", "Загадываю персонажа из популярной игры. Задавайте вопросы, на которые можно ответить да/нет."),
            },
            ["Улучшение сайта"] = new[]
            {
                ("Предложение: темная тема сайта", "Было бы здорово добавить темную тему. Глаза устают от светлого фона при ночном чтении."),
                ("Просьба: уведомления в Telegram", "Хотелось бы получать уведомления о новых постах в Telegram. Есть ли такие планы?"),
            },
            ["Ошибки"] = new[]
            {
                ("Ошибка при загрузке аватара", "При попытке загрузить аватар выдает ошибку 500. Файл PNG, размер 200x200. Кто-нибудь сталкивался?"),
                ("Не работает поиск по играм", "При вводе названия в поиск ничего не находится, хотя игра точно существует."),
            },
            ["Для новичков"] = new[]
            {
                ("Помогите разобраться с интерфейсом", "Новичок на сайте, не могу понять, как создать персонажа. Где кнопка?"),
                ("Как найти игру для начинающих?", "Только зарегистрировался, опыта нет. Как найти игру, где примут новичка?"),
                ("FAQ для новых пользователей", "Собрал ответы на частые вопросы. Читайте, прежде чем создавать тему!"),
            },
            ["Новости проекта"] = new[]
            {
                ("Запуск DM3 — новая версия сайта!", """
Рады представить полностью переписанную версию сайта DM3!

[b]Что нового:[/b]
• Полностью новый дизайн — современный, чистый, адаптивный
• Улучшенный редактор постов с поддержкой BBCode и предпросмотром
• Система фильтров игр — ищите по жанрам, тегам, статусу набора
• Оптимизированная производительность — страницы загружаются в 3 раза быстрее
• Темная тема для ночных сов

[b]Для мастеров:[/b]
• Новая панель управления игрой
• Улучшенное управление персонажами и комнатами
• Система приглашений игроков

Мы работали над этим обновлением больше года и надеемся, что вам понравится! Если найдете баги — пишите в раздел "Ошибки".
"""),
                ("Обновление март 2026: фильтры и теги", """
Большое обновление системы поиска игр!

[b]Теги игр[/b]
Теперь каждая игра может иметь теги — жанры, сеттинги, особенности. Мастера могут добавлять до 10 тегов к своей игре. Облако тегов в сайдбаре показывает популярные теги.

[b]Расширенные фильтры[/b]
• Фильтр по статусу: активные, на наборе, завершенные
• Фильтр по тегам: включить или исключить
• Сортировка: по активности, популярности, дате создания
• Поиск по названию и описанию

[b]Как пользоваться:[/b]
1. Зайдите на страницу "Игры"
2. Используйте панель фильтров слева
3. Выбирайте теги кликом
4. Результаты обновляются мгновенно

Теперь найти идеальную игру для себя стало намного проще. Не забудьте добавить теги к своим играм!
"""),
                ("Планы на весну 2026", """
Делимся планами на ближайшие месяцы!

[b]Апрель 2026[/b]
• Мобильная версия сайта — полностью адаптивный дизайн для телефонов
• Push-уведомления в браузере о новых постах

[b]Май 2026[/b]
• Интеграция с Discord — бот для уведомлений о событиях в играх
• Система достижений для игроков и мастеров

[b]Июнь 2026[/b]
• Улучшенный блок — расширенные возможности форматирования
• Галерея изображений для игр

[b]В разработке:[/b]
• Система рекомендаций игр на основе ваших предпочтений
• Календарь событий для игр
• Экспорт истории игры в PDF

Следите за новостями! Если у вас есть предложения — пишите в раздел "Улучшение сайта".
"""),
            },
        };

        var commentTemplates = new[]
        {
            "Отличная тема, поддерживаю!",
            "Согласен с автором на 100%.",
            "Интересная мысль, но я бы добавил...",
            "Спасибо за информацию!",
            "У меня был похожий опыт.",
            "Не согласен, но уважаю мнение.",
            "Кто-нибудь уже пробовал это?",
            "Жду продолжения обсуждения.",
            "+1 к этому предложению",
            "Хорошая инициатива!",
        };

        // Get max TopicNumber per board to avoid conflicts with existing topics (like system topic)
        var maxTopicNumbers = await _dbContext.Set<Topic>()
            .GroupBy(t => t.BoardId)
            .Select(g => new { BoardId = g.Key, MaxNumber = g.Max(t => t.TopicNumber) })
            .ToDictionaryAsync(x => x.BoardId, x => x.MaxNumber);

        foreach (var board in boards)
        {
            // Get topics for this board, or use generic ones
            var topics = boardTopics.GetValueOrDefault(board.Title) ?? new[]
            {
                ("Обсуждение", "Общая тема для обсуждения."),
                ("Вопросы и ответы", "Задавайте вопросы здесь."),
            };

            // Check if regular users can post
            var canPost = (board.CreateTopicPolicy & (BoardAccessPolicy.RegularUser | BoardAccessPolicy.Guest)) != BoardAccessPolicy.None;

            // Start numbering after any existing topics in this board
            var startNumber = maxTopicNumbers.GetValueOrDefault(board.BoardId, 0);

            for (var i = 0; i < topics.Length; i++)
            {
                var template = topics[i];
                var author = canPost
                    ? users[Random.Shared.Next(users.Count)]
                    : users.First(u => u.Role >= UserRole.Mentor);

                var topicId = _guidFactory.Create();

                // News topics: first 2 are recent (within last week), rest are older
                var topicAge = board.Title == "Новости проекта" && i < 2
                    ? Random.Shared.Next(1, 7) // 1-6 days ago for recent news
                    : Random.Shared.Next(7, 30); // 7-29 days ago for older content

                var topic = new Topic
                {
                    TopicId = topicId,
                    BoardId = board.BoardId,
                    AuthorId = author.UserId,
                    TopicNumber = startNumber + i + 1, // Unique within board, starting after existing topics
                    CreatedUtc = now.AddDays(-topicAge),
                    Title = template.Title,
                    Text = template.Text,
                    IsAttached = i == 0 && board.Title == "Новости проекта", // Pin first topic in News
                    IsClosed = false,
                    CommentCount = 0,
                    IsRemoved = false
                };

                _dbContext.Set<Topic>().Add(topic);
                result.TopicsCreated++;

                // Add comments (track them to update LastCommentId later)
                // FAQ topic gets 50 comments for pagination testing
                var commentsToCreate = template.Title == "FAQ для новых пользователей" ? 50 : Random.Shared.Next(3, 8);
                DbComment? lastComment = null;

                // Extended comment templates for FAQ topic
                var faqCommentTemplates = new[]
                {
                    "Отличный FAQ! Очень помогло разобраться с основами.",
                    "Подскажите, как создать персонажа? Не могу найти кнопку.",
                    "Спасибо за подробные инструкции по регистрации.",
                    "А где можно посмотреть список активных игр?",
                    "Как связаться с мастером игры напрямую?",
                    "Отличное сообщество! Всем рекомендую.",
                    "Не понял момент про рейтинг. Можете объяснить подробнее?",
                    "Когда откроют новый раздел форума?",
                    "Прочитал правила, все понятно. Спасибо!",
                    "Как изменить аватар? Не нашел настройку.",
                    "Подскажите хорошую игру для новичка.",
                    "Есть ли ограничения по количеству персонажей?",
                    "Как удалить свой комментарий?",
                    "Отличная платформа для ролевых игр!",
                    "Не работает поиск, это баг или фича?",
                    "Когда будет мобильная версия?",
                    "Спасибо за быстрый ответ в предыдущем вопросе.",
                    "Как вступить в закрытую игру?",
                    "Можно ли создать свой форум?",
                    "Где посмотреть историю изменений?",
                    "Отличный дизайн сайта! Кто делал?",
                    "Как настроить уведомления?",
                    "Есть ли Discord сервер сообщества?",
                    "Подскажите, как форматировать текст.",
                    "Можно ли использовать картинки в постах?",
                    "Как сменить никнейм?",
                    "Где находится раздел с правилами?",
                    "Как пожаловаться на нарушение?",
                    "Отличная идея с рейтингом постов!",
                    "Темная тема — это супер!",
                    "Как посмотреть свою статистику?",
                    "Подскажите, как работает система тегов.",
                    "Можно ли экспортировать свои посты?",
                    "Как добавить друга в игру?",
                    "Что означает значок рядом с ником?",
                    "Спасибо за FAQ, очень полезно!",
                    "Как отредактировать старый пост?",
                    "Есть ли лимит на длину сообщения?",
                    "Когда обновят интерфейс?",
                    "Как создать опрос в теме?",
                    "Подскажите правила оформления постов.",
                    "Можно ли привязать Telegram?",
                    "Как работает система модерации?",
                    "Отличное сообщество, всем привет!",
                    "Где найти архив старых игр?",
                    "Как восстановить удаленный пост?",
                    "Спасибо администрации за работу!",
                    "Есть ли мобильное приложение?",
                    "Как поменять тему оформления?",
                    "Очень удобный интерфейс, молодцы!",
                };

                for (var j = 0; j < commentsToCreate; j++)
                {
                    var commentAuthor = users[Random.Shared.Next(users.Count)];
                    var commentText = template.Title == "FAQ для новых пользователей"
                        ? faqCommentTemplates[j % faqCommentTemplates.Length]
                        : commentTemplates[Random.Shared.Next(commentTemplates.Length)];

                    var comment = new DbComment
                    {
                        CommentId = _guidFactory.Create(),
                        EntityId = topic.TopicId,
                        AuthorId = commentAuthor.UserId,
                        CreatedUtc = topic.CreatedUtc.AddHours(j * 12 + Random.Shared.Next(1, 12)),
                        Text = commentText,
                        IsRemoved = false
                    };

                    _dbContext.Set<DbComment>().Add(comment);
                    result.CommentsCreated++;
                    topic.CommentCount++;
                    lastComment = comment;
                }

                // Update board stats
                board.TopicsCount++;
                board.CommentsCount += topic.CommentCount;
            }
        }

        // Save topics and comments first to avoid circular dependency
        await _dbContext.SaveChangesAsync();

        // Now update LastCommentId references for topics that have comments
        var topicsWithComments = await _dbContext.Set<Topic>()
            .Where(t => t.CommentCount > 0 && t.LastCommentId == null)
            .ToListAsync();

        foreach (var topic in topicsWithComments)
        {
            var lastComment = await _dbContext.Set<DbComment>()
                .Where(c => c.EntityId == topic.TopicId)
                .OrderByDescending(c => c.CreatedUtc)
                .FirstOrDefaultAsync();

            if (lastComment != null)
            {
                topic.LastCommentId = lastComment.CommentId;

                // Update board
                var board = boards.FirstOrDefault(b => b.BoardId == topic.BoardId);
                if (board != null)
                {
                    board.LastCommentId = lastComment.CommentId;
                    board.LastCommentTopicId = topic.TopicId;
                    board.LastCommentTopicTitle = topic.Title;
                    board.LastCommentTopicNumber = topic.TopicNumber;
                    board.LastCommentUtc = lastComment.CreatedUtc;
                    board.LastCommentAuthorId = lastComment.AuthorId;
                }
            }
        }

        result.Details.Add($"Created {result.TopicsCreated} topics with {result.CommentsCreated} comments");
    }

    private async Task AssignBoardModerators(List<DbUser> users, ComprehensiveSeedResult result)
    {
        // Check if moderators already assigned
        var existingModeratorsCount = await _dbContext.Set<BoardModerator>().CountAsync();
        if (existingModeratorsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Board moderators already assigned ({existingModeratorsCount}), skipping");
            return;
        }

        var boards = await _dbContext.Set<Board>().ToListAsync();
        if (boards.Count == 0)
        {
            result.Details.Add("No boards found, skipping moderator assignment");
            return;
        }

        // Get users who can be moderators (Moderator+ role)
        var moderatorUsers = users
            .Where(u => u.Role >= UserRole.Moderator)
            .ToList();

        if (moderatorUsers.Count == 0)
        {
            result.Details.Add("No moderator users found, skipping board moderator assignment");
            return;
        }

        // Board moderator assignments (realistic distribution)
        // All boards should have at least one moderator assigned
        var assignments = new Dictionary<string, string[]>
        {
            ["Общий"] = new[] { "TestModerator", "TestSeniorMod" },
            ["Игровые системы"] = new[] { "TestModerator" },
            ["Поиск мастера и игроков"] = new[] { "TestModerator", "TestSeniorMod" },
            ["Котел идей"] = new[] { "TestModerator" },
            ["Конкурсы"] = new[] { "TestSeniorMod" },
            ["Под столом"] = new[] { "TestModerator", "TestMentor" },
            ["Неролевые игры"] = new[] { "TestModerator" },
            ["Улучшение сайта"] = new[] { "TestSeniorMod", "SolohinLex" },
            ["Ошибки"] = new[] { "TestSeniorMod" },
            ["Для новичков"] = new[] { "TestMentor" },
            ["Новости проекта"] = new[] { "SolohinLex" },
        };

        foreach (var (boardTitle, usernames) in assignments)
        {
            var board = boards.FirstOrDefault(b => b.Title == boardTitle);
            if (board == null) continue;

            foreach (var username in usernames)
            {
                var user = users.FirstOrDefault(u =>
                    string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
                if (user == null) continue;

                // Check if already assigned
                var exists = await _dbContext.Set<BoardModerator>()
                    .AnyAsync(bm => bm.BoardId == board.BoardId && bm.UserId == user.UserId);
                if (exists) continue;

                _dbContext.Set<BoardModerator>().Add(new BoardModerator
                {
                    BoardModeratorId = _guidFactory.Create(),
                    BoardId = board.BoardId,
                    UserId = user.UserId
                });
                result.BoardModeratorsAssigned++;
            }
        }

        result.Details.Add($"Assigned {result.BoardModeratorsAssigned} board moderators");
    }

    private async Task<List<Guid>> CreateGames(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var gameIds = new List<Guid>();

        // Get existing seed games (excluding migration test games)
        var existingGames = await _dbContext.Set<DbGame>()
            .Include(g => g.Characters)
            .Include(g => g.Rooms)
            .Where(g => !g.Title.StartsWith("Тест:"))
            .ToListAsync();

        var existingTitles = existingGames.Select(g => g.Title).ToHashSet();

        var mentor = users.First(u => u.Role == UserRole.Mentor);
        var experiencedUsers = users.Where(u => u.QuantityRating >= 100).ToList();
        var newbieUsers = users.Where(u => u.QuantityRating < 100).ToList();

        if (experiencedUsers.Count == 0)
        {
            experiencedUsers = users.Take(3).ToList();
        }

        // Well-known tag GUIDs (from InitialCreate migration)
        // Helper for compact tag ID generation
        static Guid T(int id) => Guid.Parse($"00000000-0000-0000-0000-{id:x12}");

        // Система (01-19)
        var (tagBlackBirdPie, tagDnD, tagDnD5e, tagD100, tagDawnOfWorlds) = (T(0x01), T(0x02), T(0x03), T(0x04), T(0x05));
        var (tagFallout, tagFate, tagFudge, tagGURPS, tagInterlock) = (T(0x06), T(0x08), T(0x09), T(0x0a), T(0x0b));
        var (tagPathfinder1e, tagPathfinder2e, tagPbtA, tagRisus, tagSavageWorlds) = (T(0x0d), T(0x0e), T(0x0f), T(0x10), T(0x11));
        var (tagStarfinder1e, tagStarfinder2e, tagWarhammer, tagWoD, tagAuthor) = (T(0x12), T(0x13), T(0x14), T(0x15), T(0x16));
        var (tagMafia, tagSloveski, tagEraVodolea) = (T(0x17), T(0x18), T(0x19));

        // Жанр (1a-2b)
        var (tagAltHistory, tagAction, tagDetective, tagZombie, tagHistorical) = (T(0x1a), T(0x1b), T(0x1c), T(0x1d), T(0x1e));
        var (tagCyberpunk, tagComedy, tagKosmoopera, tagMystic, tagModern) = (T(0x1f), T(0x20), T(0x21), T(0x22), T(0x23));
        var (tagPostApoc, tagPsychedelic, tagSteampunk, tagThriller, tagTrash) = (T(0x24), T(0x25), T(0x26), T(0x27), T(0x28));
        var (tagHorror, tagSciFi, tagFantasy) = (T(0x29), T(0x2a), T(0x2b));

        // Формат игры (2c-32)
        var (tagDungeonCrawl, tagPvP, tagSurvival, tagSandbox, tagStrategy) = (T(0x2c), T(0x2d), T(0x2e), T(0x2f), T(0x30));
        var (tagPlotDriven, tagTactics) = (T(0x31), T(0x32));

        // Формат постов (33-34), Темп (35-36, 3e), Ограничения (37-3b), Новички (3c-3d), Деликатный (3f-41)
        var (tagShortPost, tagLiterary, tagSlowPace, tagFastPace, tagDrySeasons) = (T(0x33), T(0x34), T(0x35), T(0x36), T(0x3e));
        var (tagNoSwearing, tagNoViolence, tagGrammarNazi, tagPrivateGroup, tagMessengerRequired) = (T(0x37), T(0x38), T(0x39), T(0x3a), T(0x3b));
        var (tagForNewbies, tagNewbieMaster, tagErotica, tagShockContent, tagSensitiveTopics) = (T(0x3c), T(0x3d), T(0x3f), T(0x40), T(0x41));

        // Local aliases for repeated enum values (reduces verbosity by ~60%)
        var (Active, Draft, Closed) = (ModuleStatus.Active, ModuleStatus.Draft, ModuleStatus.Closed);
        var (Private, Public) = (DraftVisibility.Private, DraftVisibility.Public);
        var (NoReason, Finished, Frozen) = (ClosedReason.None, ClosedReason.Finished, ClosedReason.Frozen);
        var (Approved, Awaiting) = (PremoderationStatus.Approved, PremoderationStatus.AwaitingApproval);

        var gameTemplates = new[]
        {
            // Active with recruitment (15) - more than sidebar limit of 10
            new { Title = "Хроники Забытых Королевств", System = "D&D 5e", Setting = "Forgotten Realms", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagPlotDriven, tagLiterary, tagSlowPace, tagDungeonCrawl, tagAction } },
            new { Title = "Врата Бездны", System = "Pathfinder 2e", Setting = "Golarion", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagPathfinder2e, tagFantasy, tagPlotDriven, tagTactics, tagAction, tagLiterary, tagSlowPace } },
            new { Title = "Последний Рубеж", System = "Savage Worlds", Setting = "Deadlands", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagSavageWorlds, tagForNewbies, tagFantasy, tagAltHistory, tagHorror, tagAction, tagShortPost, tagFastPace, tagMystic } },
            new { Title = "Путь Самурая", System = "Legend of the Five Rings", Setting = "Rokugan", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagHistorical, tagLiterary, tagSlowPace, tagNoSwearing } },
            new { Title = "Королевство Теней", System = "D&D 5e", Setting = "Ravenloft", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagForNewbies, tagHorror, tagMystic, tagPlotDriven, tagLiterary } },
            new { Title = "Колонисты Марса", System = "Stars Without Number", Setting = "Solar System 2350", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagStarfinder1e, tagSciFi, tagKosmoopera, tagSurvival, tagPlotDriven, tagShortPost, tagFastPace } },
            new { Title = "Пески Времени", System = "D&D 5e", Setting = "Al-Qadim", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagHistorical, tagPlotDriven, tagDungeonCrawl, tagLiterary, tagMystic } },
            new { Title = "Зов Ктулху", System = "Call of Cthulhu", Setting = "1920s Arkham", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagD100, tagPlotDriven, tagLiterary, tagHorror, tagMystic, tagDetective, tagHistorical, tagSlowPace, tagGrammarNazi } },
            new { Title = "Механикум", System = "Warhammer 40k", Setting = "Dark Millennium", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagWarhammer, tagSciFi, tagPlotDriven, tagAction, tagTactics, tagLiterary, tagSlowPace } },
            new { Title = "Эльдорадо", System = "Savage Worlds", Setting = "Age of Sail", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagSavageWorlds, tagForNewbies, tagHistorical, tagAction, tagSandbox, tagShortPost, tagFastPace, tagComedy } },
            new { Title = "Наследие Драконов", System = "Pathfinder 2e", Setting = "Homebrew", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagPathfinder2e, tagFantasy, tagPlotDriven, tagAction, tagDungeonCrawl, tagLiterary, tagSlowPace } },
            new { Title = "Стальные Небеса", System = "Stars Without Number", Setting = "Cyberpunk Future", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagStarfinder2e, tagSciFi, tagCyberpunk, tagAction, tagPlotDriven, tagShortPost, tagFastPace, tagThriller } },
            new { Title = "Туманы Авалона", System = "Fate Core", Setting = "Arthurian", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagFate, tagFantasy, tagLiterary, tagMystic, tagHistorical, tagPlotDriven, tagSlowPace, tagGrammarNazi, tagNoViolence } },
            new { Title = "Проклятие Фараона", System = "GURPS", Setting = "Ancient Egypt", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagGURPS, tagPlotDriven, tagHistorical, tagMystic, tagDetective, tagLiterary, tagSlowPace } },
            new { Title = "Охотники за Тенями", System = "World of Darkness", Setting = "Modern Nights", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = true, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagWoD, tagLiterary, tagForNewbies, tagModern, tagHorror, tagMystic, tagAction, tagPlotDriven } },
            // Active without recruitment (15)
            new { Title = "Тени Киберпанка", System = "Cyberpunk RED", Setting = "Night City 2077", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagInterlock, tagCyberpunk, tagSciFi, tagLiterary, tagAction, tagPlotDriven, tagThriller, tagSlowPace, tagMessengerRequired } },
            new { Title = "Империя Звезд", System = "Stars Without Number", Setting = "Far Future", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagStarfinder2e, tagSciFi, tagKosmoopera, tagPlotDriven, tagStrategy, tagLiterary, tagSlowPace, tagDrySeasons } },
            new { Title = "Клинки и Колдовство", System = "D&D 5e", Setting = "Dark Sun", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagLiterary, tagSurvival, tagPostApoc, tagAction, tagSlowPace } },
            new { Title = "Сага о Викингах", System = "Fate Core", Setting = "Mythic Scandinavia", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagFate, tagFantasy, tagLiterary, tagHistorical, tagMystic, tagPlotDriven, tagAction, tagSlowPace } },
            new { Title = "Пираты Карибского Моря", System = "7th Sea", Setting = "Theah", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagHistorical, tagAction, tagComedy, tagSandbox, tagShortPost } },
            new { Title = "Город Грехов", System = "World of Darkness", Setting = "Modern Gothic", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagWoD, tagLiterary, tagModern, tagHorror, tagMystic, tagThriller, tagPlotDriven, tagSlowPace, tagPrivateGroup, tagSensitiveTopics } },
            new { Title = "Война Гильдий", System = "D&D 5e", Setting = "Ravnica", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagPvP, tagStrategy, tagPlotDriven, tagTactics, tagSlowPace } },
            new { Title = "Метро 2033", System = "GURPS", Setting = "Post-Apocalypse Moscow", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagGURPS, tagPostApoc, tagHorror, tagSurvival, tagAction, tagPlotDriven, tagSlowPace, tagThriller } },
            new { Title = "Ведьмак: Дикая Охота", System = "Словеска", Setting = "The Witcher", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagSloveski, tagFantasy, tagLiterary, tagAction, tagMystic, tagPlotDriven, tagDetective, tagSlowPace } },
            new { Title = "Странники Пустоши", System = "Savage Worlds", Setting = "Fallout", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagFallout, tagSavageWorlds, tagPostApoc, tagSurvival, tagAction, tagSandbox, tagShortPost, tagFastPace } },
            new { Title = "Рыцари Круглого Стола", System = "Pendragon", Setting = "Camelot", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagHistorical, tagLiterary, tagMystic, tagSlowPace, tagGrammarNazi } },
            new { Title = "Космические Волки", System = "Stars Without Number", Setting = "Military Sci-Fi", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagStarfinder1e, tagSciFi, tagKosmoopera, tagAction, tagTactics, tagPlotDriven, tagSlowPace } },
            new { Title = "Бегущий по Лезвию", System = "Cyberpunk RED", Setting = "Neo Tokyo", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagInterlock, tagCyberpunk, tagSciFi, tagAction, tagDetective, tagPlotDriven, tagThriller, tagLiterary } },
            new { Title = "Темное Средневековье", System = "D&D 5e", Setting = "Dark Ages Europe", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagHistorical, tagHorror, tagMystic, tagPlotDriven, tagLiterary } },
            new { Title = "Час Волка", System = "World of Darkness", Setting = "Werewolf", Status = Active, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagWoD, tagLiterary, tagPlotDriven, tagModern, tagHorror, tagAction, tagMystic, tagSlowPace } },
            // Draft (experienced master) - PUBLIC (3)
            new { Title = "Проект: Звездные Врата", System = "Savage Worlds", Setting = "Sci-Fi", Status = Draft, DraftVisibility = Public, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagSavageWorlds, tagSciFi, tagKosmoopera, tagAction, tagPlotDriven, tagShortPost } },
            new { Title = "Тайны Древних", System = "Call of Cthulhu", Setting = "1920s", Status = Draft, DraftVisibility = Public, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagD100, tagPlotDriven, tagLiterary, tagHorror, tagMystic, tagDetective, tagHistorical } },
            new { Title = "Затерянный Континент", System = "D&D 5e", Setting = "Lost World", Status = Draft, DraftVisibility = Public, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagSandbox, tagAction, tagDungeonCrawl } },
            // Draft (newbie master - needs premoderation) - PRIVATE (2)
            new { Title = "Моя первая игра", System = "Словеска", Setting = "Фэнтези", Status = Draft, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Awaiting, Tags = new[] { tagSloveski, tagFantasy, tagForNewbies, tagNewbieMaster, tagPlotDriven, tagShortPost, tagNoSwearing } },
            new { Title = "Приключения начинаются", System = "D&D 5e", Setting = "Homebrew", Status = Draft, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Awaiting, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagForNewbies, tagNewbieMaster, tagDungeonCrawl, tagShortPost } },
            // Closed - Finished (15)
            new { Title = "Легенда о Драконе", System = "D&D 3.5", Setting = "Homebrew", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagDnD, tagFantasy, tagPlotDriven, tagDungeonCrawl, tagAction, tagLiterary, tagSlowPace } },
            new { Title = "Падение Империи", System = "GURPS", Setting = "Roman Empire", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagGURPS, tagPlotDriven, tagLiterary, tagHistorical, tagAltHistory, tagStrategy, tagSlowPace, tagGrammarNazi } },
            new { Title = "Огни Неона", System = "Cyberpunk 2020", Setting = "Night City", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagInterlock, tagCyberpunk, tagSciFi, tagAction, tagPlotDriven, tagThriller, tagLiterary, tagSlowPace } },
            new { Title = "Эпоха Легенд", System = "D&D 5e", Setting = "Forgotten Realms", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagLiterary, tagPlotDriven, tagDungeonCrawl, tagAction, tagSlowPace } },
            new { Title = "Звездный Крейсер", System = "Stars Without Number", Setting = "Space Opera", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagStarfinder1e, tagSciFi, tagKosmoopera, tagPlotDriven, tagAction, tagStrategy, tagLiterary, tagSlowPace } },
            new { Title = "Темная Башня", System = "Fate Core", Setting = "Post-Apocalypse Fantasy", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagFate, tagFantasy, tagPostApoc, tagPlotDriven, tagMystic, tagHorror, tagLiterary, tagSlowPace } },
            new { Title = "Песнь Льда и Пламени", System = "A Song of Ice and Fire RPG", Setting = "Westeros", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagLiterary, tagPlotDriven, tagStrategy, tagPvP, tagSlowPace, tagGrammarNazi } },
            new { Title = "Властелин Колец", System = "The One Ring", Setting = "Middle-earth", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagPlotDriven, tagLiterary, tagAction, tagMystic, tagSlowPace } },
            new { Title = "Первая Колония", System = "Stars Without Number", Setting = "Alien Planet", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagStarfinder2e, tagSciFi, tagKosmoopera, tagSurvival, tagPlotDriven, tagHorror, tagSlowPace } },
            new { Title = "Тень Мордора", System = "D&D 5e", Setting = "Middle-earth", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagAction, tagPlotDriven, tagDungeonCrawl, tagLiterary } },
            new { Title = "Кровь и Вино", System = "Словеска", Setting = "The Witcher", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagSloveski, tagFantasy, tagLiterary, tagAction, tagMystic, tagDetective, tagPlotDriven, tagSlowPace } },
            new { Title = "Рассвет Империи", System = "GURPS", Setting = "Alternate History", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagGURPS, tagPlotDriven, tagAltHistory, tagHistorical, tagStrategy, tagLiterary, tagSlowPace } },
            new { Title = "Последняя Надежда", System = "Savage Worlds", Setting = "Zombie Apocalypse", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagSavageWorlds, tagPostApoc, tagZombie, tagSurvival, tagHorror, tagAction, tagShortPost, tagFastPace } },
            new { Title = "Хроники Нарнии", System = "Fate Core", Setting = "Narnia", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagFate, tagFantasy, tagForNewbies, tagPlotDriven, tagMystic, tagNoSwearing, tagNoViolence } },
            new { Title = "Город Ангелов", System = "World of Darkness", Setting = "Los Angeles", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagWoD, tagLiterary, tagPlotDriven, tagModern, tagHorror, tagMystic, tagThriller, tagSlowPace } },
            // Closed - Frozen (5)
            new { Title = "Заброшенный Мир", System = "GURPS", Setting = "Post-Apocalypse", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Frozen, Premod = Approved, Tags = new[] { tagGURPS, tagPostApoc, tagSurvival, tagPlotDriven, tagHorror, tagAction, tagDrySeasons } },
            new { Title = "Эхо Войны", System = "Savage Worlds", Setting = "WW2", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Frozen, Premod = Approved, Tags = new[] { tagSavageWorlds, tagPlotDriven, tagHistorical, tagAction, tagTactics, tagShortPost, tagDrySeasons } },
            new { Title = "Забытые Руины", System = "D&D 5e", Setting = "Homebrew", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Frozen, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagDungeonCrawl, tagAction, tagPlotDriven, tagDrySeasons } },
            new { Title = "Тихий Омут", System = "Call of Cthulhu", Setting = "Lovecraft Country", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Frozen, Premod = Approved, Tags = new[] { tagD100, tagPlotDriven, tagHorror, tagMystic, tagDetective, tagLiterary, tagDrySeasons } },
            new { Title = "Остров Сокровищ", System = "7th Sea", Setting = "Caribbean", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Frozen, Premod = Approved, Tags = new[] { tagAuthor, tagFantasy, tagHistorical, tagAction, tagSandbox, tagComedy, tagDrySeasons } },
            // Closed - None (3)
            new { Title = "Потерянные Хроники", System = "D&D 5e", Setting = "Homebrew", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagDnD5e, tagDnD, tagFantasy, tagPlotDriven, tagDungeonCrawl, tagAction, tagSlowPace } },
            new { Title = "Закат Эпохи", System = "Pathfinder 1e", Setting = "Golarion", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagPathfinder1e, tagFantasy, tagPlotDriven, tagAction, tagTactics, tagLiterary, tagSlowPace } },
            new { Title = "Конец Света", System = "GURPS", Setting = "Apocalypse", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = NoReason, Premod = Approved, Tags = new[] { tagGURPS, tagPostApoc, tagSurvival, tagHorror, tagAction, tagPlotDriven, tagSlowPace } },
            // Additional games for remaining tags (10)
            new { Title = "Сотворение Миров", System = "Dawn of Worlds", Setting = "Homebrew Universe", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagDawnOfWorlds, tagFantasy, tagStrategy, tagSandbox, tagLiterary, tagSlowPace } },
            new { Title = "Апокалипсис: День Ноль", System = "Apocalypse World", Setting = "Post-Apocalypse", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagPbtA, tagPostApoc, tagSurvival, tagPlotDriven, tagAction, tagShortPost, tagFastPace } },
            new { Title = "Клуб Анонимных Убийц", System = "Мафия", Setting = "Modern Noir", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagMafia, tagModern, tagDetective, tagThriller, tagPvP, tagShortPost, tagFastPace } },
            new { Title = "Безумные Приключения", System = "Risus", Setting = "Сomedy Fantasy", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagRisus, tagFantasy, tagComedy, tagTrash, tagAction, tagShortPost, tagFastPace } },
            new { Title = "Эра Водолея: Пробуждение", System = "Эра Водолея", Setting = "Post-Soviet Fantasy", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagEraVodolea, tagFantasy, tagMystic, tagModern, tagPlotDriven, tagLiterary, tagSlowPace } },
            new { Title = "FUDGE: Универсум", System = "FUDGE", Setting = "Multigenre", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagFudge, tagFantasy, tagSciFi, tagSandbox, tagPlotDriven, tagShortPost, tagFastPace } },
            new { Title = "Паровая Империя", System = "Savage Worlds", Setting = "Victorian Steampunk", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagSavageWorlds, tagSteampunk, tagAltHistory, tagDetective, tagAction, tagPlotDriven, tagLiterary } },
            new { Title = "За Гранью Реальности", System = "FUDGE", Setting = "Dreamscape", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagFudge, tagPsychedelic, tagMystic, tagHorror, tagPlotDriven, tagLiterary, tagSlowPace } },
            new { Title = "Black Bird: Сказки Старого Города", System = "Black Bird Pie", Setting = "Urban Fantasy", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagBlackBirdPie, tagFantasy, tagMystic, tagModern, tagForNewbies, tagShortPost, tagFastPace } },
            new { Title = "Темные Страсти", System = "World of Darkness", Setting = "Gothic Romance", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagWoD, tagHorror, tagMystic, tagPlotDriven, tagLiterary, tagSlowPace, tagErotica, tagPrivateGroup } },
            new { Title = "Кровавый Карнавал", System = "Словеска", Setting = "Extreme Horror", Status = Closed, DraftVisibility = Private, IsRecruitmentOpen = false, ClosedReason = Finished, Premod = Approved, Tags = new[] { tagSloveski, tagHorror, tagTrash, tagPlotDriven, tagLiterary, tagSlowPace, tagShockContent, tagPrivateGroup } },
        };

        // Variation configs MUST match templates order 1:1 (58 total)
        // Templates: 15 recruiting, 15 active, 3 draft public, 2 draft private, 15 finished, 5 frozen, 3 closed
        var gameVariations = new[]
        {
            // Active with recruitment (15) - spread 0-30
            (readers: 28, pcLimit: 8, activeChars: 3, comments: 12, posts: 25),
            (readers: 22, pcLimit: 6, activeChars: 2, comments: 10, posts: 20),
            (readers: 18, pcLimit: 5, activeChars: 2, comments: 8, posts: 18),
            (readers: 15, pcLimit: 4, activeChars: 2, comments: 7, posts: 15),
            (readers: 12, pcLimit: 6, activeChars: 2, comments: 6, posts: 14),
            (readers: 10, pcLimit: 5, activeChars: 1, comments: 5, posts: 12),
            (readers: 8, pcLimit: 4, activeChars: 1, comments: 4, posts: 10),
            (readers: 6, pcLimit: 6, activeChars: 1, comments: 4, posts: 9),
            (readers: 5, pcLimit: 5, activeChars: 1, comments: 3, posts: 8),
            (readers: 4, pcLimit: 4, activeChars: 1, comments: 3, posts: 7),
            (readers: 3, pcLimit: 6, activeChars: 1, comments: 2, posts: 6),
            (readers: 2, pcLimit: 5, activeChars: 1, comments: 2, posts: 5),
            (readers: 1, pcLimit: 4, activeChars: 1, comments: 2, posts: 5),
            (readers: 1, pcLimit: 6, activeChars: 1, comments: 1, posts: 4),
            (readers: 0, pcLimit: 5, activeChars: 1, comments: 1, posts: 3),
            // Active without recruitment (15) - spread 0-30
            (readers: 30, pcLimit: 6, activeChars: 6, comments: 45, posts: 180),
            (readers: 25, pcLimit: 5, activeChars: 5, comments: 38, posts: 140),
            (readers: 20, pcLimit: 5, activeChars: 5, comments: 32, posts: 120),
            (readers: 16, pcLimit: 4, activeChars: 4, comments: 28, posts: 100),
            (readers: 13, pcLimit: 5, activeChars: 4, comments: 25, posts: 90),
            (readers: 10, pcLimit: (int?)null, activeChars: 4, comments: 22, posts: 80),
            (readers: 8, pcLimit: 4, activeChars: 3, comments: 18, posts: 70),
            (readers: 7, pcLimit: 5, activeChars: 3, comments: 16, posts: 65),
            (readers: 5, pcLimit: 4, activeChars: 3, comments: 14, posts: 55),
            (readers: 4, pcLimit: 5, activeChars: 3, comments: 12, posts: 50),
            (readers: 3, pcLimit: 4, activeChars: 2, comments: 10, posts: 45),
            (readers: 2, pcLimit: 5, activeChars: 2, comments: 9, posts: 40),
            (readers: 2, pcLimit: 4, activeChars: 2, comments: 8, posts: 35),
            (readers: 1, pcLimit: 5, activeChars: 2, comments: 7, posts: 30),
            (readers: 0, pcLimit: 4, activeChars: 2, comments: 6, posts: 25),
            // Drafts public (3)
            (readers: 0, pcLimit: 6, activeChars: 0, comments: 0, posts: 0),
            (readers: 0, pcLimit: (int?)null, activeChars: 0, comments: 0, posts: 0),
            (readers: 0, pcLimit: 5, activeChars: 0, comments: 0, posts: 0),
            // Drafts private/AwaitingApproval (2)
            (readers: 0, pcLimit: 4, activeChars: 0, comments: 0, posts: 0),
            (readers: 0, pcLimit: 6, activeChars: 0, comments: 0, posts: 0),
            // Closed - Finished (15) - spread 0-30
            // First finished game: 3 chars from loop + 1 Maximilian added separately = 4 total
            (readers: 30, pcLimit: 6, activeChars: 3, comments: 85, posts: 420),
            (readers: 25, pcLimit: 5, activeChars: 3, comments: 70, posts: 350),
            (readers: 20, pcLimit: 5, activeChars: 3, comments: 55, posts: 280),
            (readers: 16, pcLimit: 4, activeChars: 3, comments: 45, posts: 220),
            (readers: 13, pcLimit: 5, activeChars: 2, comments: 40, posts: 190),
            (readers: 10, pcLimit: 4, activeChars: 2, comments: 35, posts: 160),
            (readers: 8, pcLimit: 5, activeChars: 2, comments: 30, posts: 140),
            (readers: 6, pcLimit: 4, activeChars: 2, comments: 28, posts: 130),
            (readers: 5, pcLimit: 5, activeChars: 2, comments: 25, posts: 120),
            (readers: 4, pcLimit: 4, activeChars: 2, comments: 22, posts: 110),
            (readers: 3, pcLimit: 5, activeChars: 2, comments: 20, posts: 100),
            (readers: 2, pcLimit: 4, activeChars: 2, comments: 18, posts: 90),
            (readers: 2, pcLimit: 5, activeChars: 2, comments: 15, posts: 80),
            (readers: 1, pcLimit: 4, activeChars: 2, comments: 12, posts: 70),
            (readers: 0, pcLimit: 5, activeChars: 2, comments: 10, posts: 60),
            // Closed - Frozen (5) - spread 0-15
            (readers: 14, pcLimit: 6, activeChars: 0, comments: 12, posts: 65),
            (readers: 8, pcLimit: 4, activeChars: 0, comments: 8, posts: 40),
            (readers: 4, pcLimit: 5, activeChars: 0, comments: 5, posts: 25),
            (readers: 2, pcLimit: 4, activeChars: 0, comments: 4, posts: 20),
            (readers: 0, pcLimit: 5, activeChars: 0, comments: 3, posts: 15),
            // Closed - None (3) - spread 0-8
            (readers: 7, pcLimit: (int?)null, activeChars: 0, comments: 8, posts: 40),
            (readers: 3, pcLimit: 4, activeChars: 0, comments: 5, posts: 25),
            (readers: 0, pcLimit: 5, activeChars: 0, comments: 3, posts: 15),
            // Additional games for remaining tags (11) - varied spread
            (readers: 12, pcLimit: (int?)null, activeChars: 0, comments: 10, posts: 30), // Сотворение Миров
            (readers: 6, pcLimit: 4, activeChars: 0, comments: 6, posts: 20), // Апокалипсис: День Ноль
            (readers: 18, pcLimit: (int?)null, activeChars: 0, comments: 15, posts: 0), // Клуб Анонимных Убийц (Mafia)
            (readers: 8, pcLimit: 5, activeChars: 0, comments: 8, posts: 35), // Безумные Приключения
            (readers: 4, pcLimit: 4, activeChars: 0, comments: 5, posts: 25), // Эра Водолея: Пробуждение
            (readers: 2, pcLimit: 5, activeChars: 0, comments: 4, posts: 20), // FUDGE: Универсум
            (readers: 15, pcLimit: 5, activeChars: 0, comments: 12, posts: 60), // Паровая Империя
            (readers: 3, pcLimit: 4, activeChars: 0, comments: 5, posts: 25), // За Гранью Реальности
            (readers: 5, pcLimit: 4, activeChars: 0, comments: 6, posts: 30), // Black Bird
            (readers: 1, pcLimit: 4, activeChars: 0, comments: 3, posts: 15), // Темные Страсти
            (readers: 0, pcLimit: 3, activeChars: 0, comments: 2, posts: 12), // Кровавый Карнавал
        };

        var createdLargePost = false; // Track if we've used the large Diopside post
        var createdDiopsideCharacter = false; // Track if we've created the Diopside character

        for (var gi = 0; gi < gameTemplates.Length; gi++)
        {
            var template = gameTemplates[gi];

            // Skip if game with this title already exists
            if (existingTitles.Contains(template.Title))
            {
                var existingGame = existingGames.First(g => g.Title == template.Title);
                gameIds.Add(existingGame.GameId);
                continue;
            }

            var variation = gameVariations[gi % gameVariations.Length];
            var isNewbieMaster = template.Premod == PremoderationStatus.AwaitingApproval;
            var master = isNewbieMaster && newbieUsers.Count > 0
                ? newbieUsers[Random.Shared.Next(newbieUsers.Count)]
                : experiencedUsers[gi % experiencedUsers.Count];

            // Generate realistic dates based on game status
            // Draft: recent (1-30 days ago)
            // Active recruiting: medium age (30-90 days), activated recently
            // Active established: older (60-180 days), running for a while
            // Closed Finished: old (180-365 days), ran their full course
            // Closed Frozen: medium-old (90-300 days), abandoned mid-way
            DateTimeOffset gameCreatedUtc;
            DateTimeOffset? gameActivatedUtc;
            DateTimeOffset? gameClosedUtc;

            if (template.Status == ModuleStatus.Draft)
            {
                // Drafts are recent - people working on them
                gameCreatedUtc = now.AddDays(-Random.Shared.Next(1, 30));
                gameActivatedUtc = null;
                gameClosedUtc = null;
            }
            else if (template.Status == ModuleStatus.Active)
            {
                if (gi < 4)
                {
                    // First 4 active games are "new" (activated within 7 days)
                    var daysAgoCreated = Random.Shared.Next(7, 30);
                    gameCreatedUtc = now.AddDays(-daysAgoCreated);
                    gameActivatedUtc = now.AddDays(-(gi + 1)); // 1-4 days ago
                }
                else if (template.IsRecruitmentOpen)
                {
                    // Recruiting games: recently started, looking for players
                    var daysAgoCreated = Random.Shared.Next(30, 90);
                    gameCreatedUtc = now.AddDays(-daysAgoCreated);
                    var activationDelay = Random.Shared.Next(3, Math.Min(15, daysAgoCreated - 1));
                    gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
                }
                else
                {
                    // Established games: running for a while
                    var daysAgoCreated = Random.Shared.Next(60, 180);
                    gameCreatedUtc = now.AddDays(-daysAgoCreated);
                    var activationDelay = Random.Shared.Next(5, Math.Min(30, daysAgoCreated - 1));
                    gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
                }
                gameClosedUtc = null;
            }
            else // Closed
            {
                if (template.ClosedReason == ClosedReason.Finished)
                {
                    // Finished games: old, ran their full course
                    var daysAgoCreated = Random.Shared.Next(180, 365);
                    gameCreatedUtc = now.AddDays(-daysAgoCreated);
                    var activationDelay = Random.Shared.Next(5, 20);
                    gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
                    // Closed recently (within last 90 days)
                    var daysAgoSinceActivated = (int)(now - gameActivatedUtc.Value).TotalDays;
                    var minDaysRunning = Math.Min(60, daysAgoSinceActivated - 1);
                    var closedDaysAgo = Random.Shared.Next(1, Math.Max(2, Math.Min(90, daysAgoSinceActivated - minDaysRunning)));
                    gameClosedUtc = now.AddDays(-closedDaysAgo);
                }
                else // Frozen
                {
                    // Frozen games: abandoned mid-way
                    var daysAgoCreated = Random.Shared.Next(90, 300);
                    gameCreatedUtc = now.AddDays(-daysAgoCreated);
                    var activationDelay = Random.Shared.Next(3, 15);
                    gameActivatedUtc = gameCreatedUtc.AddDays(activationDelay);
                    // Frozen some time ago (30-180 days)
                    var daysAgoSinceActivated = (int)(now - gameActivatedUtc.Value).TotalDays;
                    var closedDaysAgo = Random.Shared.Next(30, Math.Max(31, Math.Min(180, daysAgoSinceActivated - 10)));
                    gameClosedUtc = now.AddDays(-closedDaysAgo);
                }
            }

            // Calculate recruitment start date (if recruiting, started after activation)
            DateTimeOffset? recruitmentStartedUtc = null;
            if (template.IsRecruitmentOpen && gameActivatedUtc.HasValue)
            {
                var daysSinceActivation = (int)(now - gameActivatedUtc.Value).TotalDays;
                var recruitmentStartDaysAgo = Random.Shared.Next(1, Math.Max(2, Math.Min(30, daysSinceActivation)));
                recruitmentStartedUtc = now.AddDays(-recruitmentStartDaysAgo);
            }

            var game = new DbGame
            {
                GameId = _guidFactory.Create(),
                CreatedUtc = gameCreatedUtc,
                ActivatedUtc = gameActivatedUtc,
                Status = template.Status,
                PremoderationStatus = template.Premod,
                ClosedReason = template.ClosedReason,
                DraftVisibility = template.DraftVisibility,
                IsRecruitmentOpen = template.IsRecruitmentOpen,
                RecruitmentCount = template.IsRecruitmentOpen
                    ? (gi % 3 == 1 ? 2 : gi % 5 == 0 ? 3 : 1) // Mix of initial (1) and subsequent (2-3) recruitment
                    : 0,
                RecruitmentPcLimit = variation.pcLimit,
                RecruitmentStartedUtc = recruitmentStartedUtc,
                ClosedUtc = gameClosedUtc,
                MasterId = master.UserId,
                MentorId = isNewbieMaster ? mentor.UserId : null,
                Title = template.Title,
                SystemName = template.System,
                NarrativeSetting = template.Setting,
                Info = $"Добро пожаловать в игру '{template.Title}'! Система: {template.System}, сеттинг: {template.Setting}. Здесь вас ждут захватывающие приключения и интересные персонажи.",
                HideTemper = false,
                HideSkills = false,
                HideInventory = false,
                HideStory = false,
                DisableAlignment = template.System == "Cyberpunk RED",
                HideDiceResult = false,
                ShowPrivateMessages = false,
                HidePostStats = false,
                CommentsAccessMode = CommentsAccessMode.Public,
                CommentCount = 0,
                IsRemoved = false,
                // Temporary placeholder - will be updated after SaveChanges
                PublicId = $"t{_guidFactory.Create():N}"[..10]
            };

            _dbContext.Set<DbGame>().Add(game);
            gameIds.Add(game.GameId);
            result.GamesCreated++;

            // Add tags to game
            foreach (var tagId in template.Tags)
            {
                _dbContext.Set<GameTag>().Add(new GameTag
                {
                    GameTagId = _guidFactory.Create(),
                    GameId = game.GameId,
                    TagId = tagId
                });
            }

            // Skip detailed content for drafts
            if (template.Status == ModuleStatus.Draft) continue;

            // Add assistants (realistic distribution: ~20% have 1, ~5% have 2)
            // Using modulo for deterministic distribution: every 5th game gets 1 assistant, every 20th gets 2
            var availableForAssistant = users.Where(u => u.UserId != master.UserId && u.QuantityRating >= 50).ToList();
            var assistantCount = gi % 20 == 0 && availableForAssistant.Count >= 2 ? 2
                : gi % 5 == 0 && availableForAssistant.Count >= 1 ? 1
                : 0;
            if (assistantCount > 0)
            {
                var selectedAssistants = availableForAssistant.OrderBy(_ => Random.Shared.Next()).Take(assistantCount).ToList();
                foreach (var assistant in selectedAssistants)
                {
                    _dbContext.Set<GameAssistant>().Add(new GameAssistant
                    {
                        GameAssistantId = _guidFactory.Create(),
                        GameId = game.GameId,
                        UserId = assistant.UserId,
                        JoinedUtc = game.CreatedUtc.AddDays(1 + selectedAssistants.IndexOf(assistant))
                    });
                }
            }

            // Room names derived from the game setting so the seed reads as
            // a real tabletop session, not a messenger app. Generic names
            // like "Общий чат" / "Главная локация" were explicitly rejected
            // — they break immersion when browsing the seeded game list.
            // Falls back to the TRPG-neutral "За ширмой" pool when the
            // setting has no bespoke entry.
            var (mainRoomTitle, chatRoomTitle, secretRoomTitle) = template.Setting switch
            {
                "Forgotten Realms" => ("Таверна 'Полумесяц'", "Кулуары авантюристов", "Тайный алтарь"),
                "Golarion" => ("Постоялый двор", "Беседка мастера", "Тайная библиотека"),
                "Ravenloft" => ("Замок Равенлофт", "Кабинет у камина", "Крипта"),
                "Deadlands" => ("Салун 'Кровавая Мэри'", "Крыльцо салуна", "Заброшенная шахта"),
                "Rokugan" => ("Чайная комната", "Сад камней", "Тайная келья"),
                "1920s Arkham" => ("Гостиная профессора", "Читальный зал", "Запретный архив"),
                "Dark Millennium" => ("Рубка 'Справедливости'", "Казарма экипажа", "Криптекс"),
                "Night City 2077" => ("Бар 'Афтерлайф'", "Переулок NC", "Темная клиника"),
                "Mythic Scandinavia" => ("Длинный дом", "Костер йотунов", "Руны предков"),
                "Theah" => ("Капитанская каюта", "Нижняя палуба", "Тайник капитана"),
                "The Witcher" => ("Трактир 'Серебряный медведь'", "Лагерь у костра", "Подземелье знахаря"),
                "Fallout" => ("Убежище", "Радиорубка", "Тайный склад"),
                "Post-Apocalypse Moscow" => ("Станция 'ВДНХ'", "Костер в туннеле", "Забытый бункер"),
                "Modern Nights" => ("Клуб 'Эль Дорадо'", "Задний двор", "Тайное убежище"),
                "Modern Gothic" => ("Особняк у кладбища", "Галерея портретов", "Подвал хозяина"),
                "Camelot" => ("Большой зал Камелота", "Часовня Грааля", "Тайная палата короля"),
                "Arthurian" => ("Скрипторий", "Дубрава друидов", "Обитель отшельника"),
                "Ancient Egypt" => ("Храм Ра", "Двор пирамиды", "Саркофаг"),
                "Dark Sun" => ("Оазис Балик", "Тень скалы", "Пещера джинна"),
                "Al-Qadim" => ("Базар Хуззузы", "Сад визиря", "Потайной проход"),
                "Solar System 2350" => ("Рубка 'Ареса'", "Кают-компания", "Грузовой трюм"),
                "Far Future" => ("Мостик крейсера", "Обсервационная палуба", "Трюм"),
                "Military Sci-Fi" => ("Рубка десантного бота", "Кают-компания", "Оружейная"),
                "Cyberpunk Future" => ("Бар 'Неон'", "Задворки сети", "Серверная комната"),
                "Ravnica" => ("Зал гильдии", "Уличный рынок", "Подвалы Ордрувьяра"),
                _ => ("Главная сцена", "За ширмой", "Тайная комната"),
            };

            // Create rooms (without linking - links will be set after SaveChanges)
            var mainRoom = new Room
            {
                RoomId = _guidFactory.Create(),
                GameId = game.GameId,
                Title = mainRoomTitle,
                AccessType = RoomAccessType.Open,
                Type = RoomType.Default,
                RoomNumber = 1,
                OrderNumber = 1,
                ViewPrivateText = false,
                ViewDiceResults = true,
                DiceEnabled = true,
                IsRemoved = false
            };
            _dbContext.Set<Room>().Add(mainRoom);

            var chatRoom = new Room
            {
                RoomId = _guidFactory.Create(),
                GameId = game.GameId,
                Title = chatRoomTitle,
                AccessType = RoomAccessType.Open,
                Type = RoomType.Chat,
                RoomNumber = 2,
                OrderNumber = 2,
                ViewPrivateText = true,
                ViewDiceResults = true,
                DiceEnabled = false,
                IsRemoved = false
            };
            _dbContext.Set<Room>().Add(chatRoom);

            // Private room
            var privateRoom = new Room
            {
                RoomId = _guidFactory.Create(),
                GameId = game.GameId,
                Title = secretRoomTitle,
                AccessType = RoomAccessType.Private,
                Type = RoomType.Default,
                RoomNumber = 3,
                OrderNumber = 3,
                ViewPrivateText = false,
                ViewDiceResults = true,
                DiceEnabled = true,
                IsRemoved = false
            };
            _dbContext.Set<Room>().Add(privateRoom);

            // Create characters - use variation.activeChars for active character count
            var characterNames = new[] { "Арагорн Следопыт", "Эльвира Чародейка", "Горим Железный Кулак", "Лиара Тенебраум", "Кассандра Видящая", "Торин Дубощит", "Леголас Зеленый Лист", "Гимли сын Глоина" };
            var races = new[] { "Человек", "Эльф", "Дварф", "Полуэльф", "Тифлинг", "Гном", "Полуорк", "Драконорожденный" };
            var classes = new[] { "Следопыт", "Маг", "Воин", "Плут", "Жрец", "Паладин", "Бард", "Варвар" };

            var playersForGame = users.Where(u => u.UserId != master.UserId).OrderBy(_ => Random.Shared.Next()).Take(variation.activeChars + 2).ToList();
            var createdCharacters = new List<Character>();

            // Create active characters based on variation
            for (var ci = 0; ci < Math.Min(variation.activeChars, playersForGame.Count); ci++)
            {
                var player = playersForGame[ci];

                // Create Diopside character for first active char of first finished game (for large post test)
                var isDiopsideChar = !createdDiopsideCharacter && ci == 0
                    && template.ClosedReason == ClosedReason.Finished;
                if (isDiopsideChar) createdDiopsideCharacter = true;

                var character = new Character
                {
                    CharacterId = _guidFactory.Create(),
                    GameId = game.GameId,
                    AuthorId = player.UserId,
                    Status = CharacterStatus.Active,
                    IsDead = false,
                    IsPlayerLeft = false,
                    IsPlayerExiled = false,
                    CreatedUtc = game.CreatedUtc.AddDays(Random.Shared.Next(1, 7)),
                    Name = isDiopsideChar ? "Диопсид" : characterNames[ci % characterNames.Length],
                    Race = isDiopsideChar ? "Кристаллическая сущность" : races[ci % races.Length],
                    Class = isDiopsideChar ? "Аберрация" : classes[ci % classes.Length],
                    Alignment = template.System != "Cyberpunk RED" ? (Alignment?)(ci % 9) : null,
                    Appearance = isDiopsideChar
                        ? "Полупрозрачное существо из живого кристалла. Отростки вдоль позвоночника мерцают приглушенным светом. Тело переливается оттенками зеленого и голубого."
                        : "Высокий, крепкого телосложения, с проницательным взглядом.",
                    Temper = isDiopsideChar
                        ? "Древний и терпеливый. Мыслит категориями эпох, но способен к неожиданному любопытству."
                        : "Решительный и отважный, но иногда слишком упрямый.",
                    Story = isDiopsideChar
                        ? "Осколок кристаллического мира, упавший через разрыв между измерениями. Столетия одиночества обострили восприятие, но оставили тоску по утраченной гармонии."
                        : "Родился в маленькой деревне, с детства мечтал о приключениях...",
                    Skills = isDiopsideChar
                        ? "Телепатия, резонанс с магией, поглощение эссенции, кристаллическая регенерация."
                        : "Владение мечом, выживание в дикой местности, следопытство.",
                    Inventory = isDiopsideChar
                        ? "Нет материальных вещей — только воспоминания о родном мире."
                        : "Меч, лук, 20 стрел, рюкзак с припасами.",
                    IsNpc = false,
                    AccessPolicy = CharacterAccessPolicy.NoAccess,
                    IsRemoved = false
                };

                _dbContext.Set<Character>().Add(character);
                createdCharacters.Add(character);
                result.CharactersCreated++;

                // Add avatar for Diopside character — реальный pipeline:
                // EXIF-strip + WebP _m/_s thumbnails, все лежит в MinIO.
                if (isDiopsideChar)
                {
                    var bytes = ReadEmbeddedSeedBytes("DM.Web.API.Assets.Seed.diopside.jpg");
                    var upload = await SeedAvatarFromBytesAsync(
                        bytes,
                        declaredContentType: "image/jpeg",
                        sourceFileName: "diopside.jpg",
                        type: UploadType.CharacterAvatar,
                        uploadId: _guidFactory.Create(),
                        userId: player.UserId,
                        entityId: character.CharacterId,
                        now: character.CreatedUtc);
                    _dbContext.Set<DM.Infrastructure.Persistence.Entities.Shared.Upload>().Add(upload);
                }

                // Add room access for active characters
                _dbContext.Set<RoomAccess>().Add(new RoomAccess
                {
                    AccessId = _guidFactory.Create(),
                    RoomId = privateRoom.RoomId,
                    CharacterId = character.CharacterId,
                    ReaderUserId = null
                });
            }

            // Add a few non-active characters for variety (if we have more players)
            var nonActiveStatuses = new[] { CharacterStatus.UnderReview, CharacterStatus.Declined, CharacterStatus.Retired };
            for (var ci = variation.activeChars; ci < playersForGame.Count && ci < variation.activeChars + 2; ci++)
            {
                var player = playersForGame[ci];
                var status = nonActiveStatuses[(ci - variation.activeChars) % nonActiveStatuses.Length];
                var character = new Character
                {
                    CharacterId = _guidFactory.Create(),
                    GameId = game.GameId,
                    AuthorId = player.UserId,
                    Status = status,
                    IsDead = status == CharacterStatus.Retired,
                    IsPlayerLeft = false,
                    IsPlayerExiled = false,
                    CreatedUtc = game.CreatedUtc.AddDays(Random.Shared.Next(1, 7)),
                    Name = characterNames[ci % characterNames.Length],
                    Race = races[ci % races.Length],
                    Class = classes[ci % classes.Length],
                    Alignment = template.System != "Cyberpunk RED" ? (Alignment?)(ci % 9) : null,
                    Appearance = "Среднего роста, ничем не примечательный.",
                    Temper = "Спокойный и рассудительный.",
                    Story = "История еще пишется...",
                    Skills = "Базовые навыки выживания.",
                    Inventory = "Простая одежда, кошелек с монетами.",
                    IsNpc = false,
                    AccessPolicy = CharacterAccessPolicy.NoAccess,
                    IsRemoved = false
                };

                _dbContext.Set<Character>().Add(character);
                createdCharacters.Add(character);
                result.CharactersCreated++;
            }

            // Create NPC
            var npc = new Character
            {
                CharacterId = _guidFactory.Create(),
                GameId = game.GameId,
                AuthorId = null,
                Status = CharacterStatus.Active,
                CreatedUtc = game.CreatedUtc,
                Name = "Таинственный Незнакомец",
                Race = "Неизвестно",
                Class = "Неизвестно",
                Appearance = "Фигура, скрытая тенью.",
                IsNpc = true,
                AccessPolicy = CharacterAccessPolicy.NoAccess,
                IsRemoved = false
            };
            _dbContext.Set<Character>().Add(npc);
            createdCharacters.Add(npc);
            result.CharactersCreated++;

            // Create posts in rooms - use variation count
            var activeCharacters = createdCharacters.Where(c => c.Status == CharacterStatus.Active && !c.IsNpc).ToList();
            var allCharactersForPosts = createdCharacters.Where(c => c.Status == CharacterStatus.Active).ToList(); // Include NPC
            // Large post for testing content expansion on homepage (~2000 chars)
            // Character: Diopside - a crystalline creature from D&D
            const string largePostText = """
                Кристаллы вдоль позвоночника резонировали с магией этого места. Диопсид замер, позволяя своим многочисленным отросткам ощутить потоки силы, пронизывающие древние стены. Столетия ожидания в темных глубинах не прошли даром — его восприятие обострилось до предела.

                «Они близко», — мысль эхом прокатилась по кристаллической структуре его сознания.

                Существо бесшумно переместилось в тень ниши, позволяя полупрозрачному телу слиться с темнотой. Только тусклое мерцание выдавало его присутствие — да и то лишь тем, кто знал, куда смотреть.

                Группа авантюристов вошла в зал, не подозревая, что их изучают. Диопсид мог бы атаковать сразу — разорвать их на части щупальцами, впитать их эссенцию. Но нет. Эти смертные несли что-то интересное.

                Человеческая женщина в мантии держала посох с камнем на навершии. Камень пульсировал знакомой энергией. Осколок. Осколок того самого кристалла, который когда-то был частью родного мира Диопсида.

                Воспоминания нахлынули волной: бескрайние кристаллические поля под фиолетовым небом, симфония резонанса миллионов собратьев, вечный покой и гармония. А потом — разрыв, падение через пустоту, одиночество в этом чужом мире.

                Существо приняло решение.

                Диопсид отделился от тени, его отростки развернулись в жесте, который смертные могли интерпретировать как миролюбивый. Авантюристы отпрянули, хватаясь за оружие. Но Диопсид уже транслировал образы напрямую в их разумы: предложение сотрудничества, обмена. Знания этого мира в обмен на осколок дома.

                Первой поняла волшебница. Ее глаза расширились — не от страха, а от удивления.

                «Ты... ты разумен?» — ее голос дрожал.

                Диопсид позволил своим кристаллам зазвучать в подобии смеха. Разумен? Он помнил эпохи, когда предки этих существ еще не спустились с деревьев. Но объяснять все это было бы слишком долго.

                Вместо этого он сформировал простой образ: рукопожатие.
                """;

            var postTexts = new[]
            {
                "Я осторожно оглядываюсь по сторонам, держа руку на рукояти меча. Что-то здесь не так...",
                "Произношу заклинание обнаружения магии, пытаясь понять природу этого места.",
                "Проверяю следы на земле. Кто-то здесь был совсем недавно.",
                "Подхожу к двери и прислушиваюсь. За ней слышны приглушенные голоса.",
                "Готовлю щит и занимаю оборонительную позицию.",
                "Внимательно осматриваю комнату в поисках скрытых проходов.",
                "Пытаюсь вспомнить, что я знаю об этом месте из старых легенд.",
                "Достаю факел и освещаю темный угол помещения.",
                "Жестом показываю спутникам, чтобы они были настороже.",
                "Прислоняюсь к стене и перевожу дух после долгого пути.",
                "Изучаю странные символы на стенах - похоже на древний язык.",
                "Проверяю свои запасы и пересчитываю оставшиеся стрелы.",
            };
            var masterTexts = new[]
            {
                "Таинственная фигура выходит из тени. 'Вы пришли за ответами? Возможно, я могу помочь... за определенную цену.'",
                "Внезапно раздается громкий скрежет - каменная дверь начинает медленно опускаться!",
                "Свет факелов мерцает, и на мгновение вам кажется, что тени на стенах двигаются сами по себе.",
                "Издалека доносится приглушенный рев - что-то большое бродит в этих коридорах.",
                "На полу вы замечаете свежие следы крови, ведущие вглубь подземелья.",
            };

            var postsToCreate = variation.posts;
            if (postsToCreate > 0 && allCharactersForPosts.Count > 0)
            {
                var baseTime = game.ActivatedUtc!.Value;
                // Spread posts evenly between activation and now (or closed date)
                var endTime = game.ClosedUtc ?? now;
                var totalHoursAvailable = (int)(endTime - baseTime).TotalHours;
                var hoursPerPost = postsToCreate > 0 ? Math.Max(1, totalHoursAvailable / postsToCreate) : 1;

                for (var pi = 0; pi < postsToCreate; pi++)
                {
                    var character = allCharactersForPosts[pi % allCharactersForPosts.Count];
                    var isMasterPost = character.IsNpc;
                    // Calculate post time, ensuring it doesn't exceed end time
                    var postOffsetHours = Math.Min(pi * hoursPerPost + Random.Shared.Next(0, hoursPerPost), totalHoursAvailable - 1);
                    // Use large Diopside post for first post of first finished game
                    var useLargePost = !createdLargePost && pi == 0
                        && template.ClosedReason == ClosedReason.Finished && !isMasterPost;
                    if (useLargePost) createdLargePost = true;

                    var post = new Post
                    {
                        PostId = _guidFactory.Create(),
                        RoomId = mainRoom.RoomId,
                        CharacterId = character.CharacterId,
                        AuthorId = character.AuthorId ?? master.UserId,
                        CreatedUtc = baseTime.AddHours(postOffsetHours),
                        GameText = useLargePost
                            ? largePostText
                            : isMasterPost
                                ? masterTexts[Random.Shared.Next(masterTexts.Length)]
                                : postTexts[Random.Shared.Next(postTexts.Length)],
                        MetagameText = useLargePost
                            ? "Пробный пост для тестирования отображения на главной. Вроде норм получилось!"
                            : Random.Shared.Next(4) == 0 ? "Интересный поворот!" : null,
                        IsRemoved = false
                    };
                    _dbContext.Set<Post>().Add(post);
                    result.PostsCreated++;

                    // Add dice rolls for the large Diopside post
                    if (useLargePost)
                    {
                        var diceCollection = _mongoClient.GetCollection<DiceRoll>();
                        var diceRolls = new[]
                        {
                            new DiceRoll
                            {
                                Id = _guidFactory.Create(),
                                PostId = post.PostId,
                                CreatedUtc = post.CreatedUtc.UtcDateTime,
                                DiceCount = 1,
                                EdgesCount = 20,
                                Bonus = 7,
                                comment = "Восприятие",
                                Result = [new RollResult { Value = 18, IsCritical = false, IsExploded = false }]
                            },
                            new DiceRoll
                            {
                                Id = _guidFactory.Create(),
                                PostId = post.PostId,
                                CreatedUtc = post.CreatedUtc.UtcDateTime.AddSeconds(1),
                                DiceCount = 1,
                                EdgesCount = 20,
                                Bonus = 5,
                                comment = "Убеждение",
                                Result = [new RollResult { Value = 14, IsCritical = false, IsExploded = false }]
                            }
                        };
                        await diceCollection.InsertManyAsync(diceRolls);
                    }

                    // Add edit history for ~10% of posts (including the large Diopside post)
                    if (useLargePost || Random.Shared.Next(10) == 0)
                    {
                        _dbContext.Set<PostEdit>().Add(new PostEdit
                        {
                            PostEditId = _guidFactory.Create(),
                            PostId = post.PostId,
                            EditorUserId = post.AuthorId,
                            EditedUtc = post.CreatedUtc.AddMinutes(Random.Shared.Next(5, 60))
                        });
                    }

                    // Increment author's QuantityRating
                    var authorId = character.AuthorId ?? master.UserId;
                    var author = users.First(u => u.UserId == authorId);
                    author.QuantityRating++;
                }
            }

            // Add readers (subscriptions) - use variation count
            var potentialReaders = users.Where(u => u.UserId != master.UserId && !playersForGame.Contains(u)).ToList();
            var readersToAdd = Math.Min(variation.readers, potentialReaders.Count);
            var readers = potentialReaders.OrderBy(_ => Random.Shared.Next()).Take(readersToAdd).ToList();
            foreach (var reader in readers)
            {
                _dbContext.Set<Subscription>().Add(new Subscription
                {
                    SubscriptionId = _guidFactory.Create(),
                    SubscriberId = reader.UserId,
                    TargetType = SubscriptionTargetType.Game,
                    TargetId = game.GameId,
                    Settings = SubscriptionSettings.None,
                    CreatedUtc = game.CreatedUtc.AddDays(Random.Shared.Next(1, 14))
                });
            }

            // Add game comments
            var gameCommentTexts = new[]
            {
                "Отличная игра, мастер молодец!",
                "Сюжет очень интересный, жду продолжения.",
                "Спасибо за атмосферу, чувствуется погружение.",
                "Персонажи прописаны очень хорошо.",
                "Хорошая динамика, не скучно.",
            };

            // Use variation for comment count
            var gameCommentsToCreate = variation.comments;
            var commentBaseTime = game.ActivatedUtc ?? game.CreatedUtc;
            var commentEndTime = game.ClosedUtc ?? now;
            var commentHoursAvailable = (int)(commentEndTime - commentBaseTime).TotalHours;
            var hoursPerComment = gameCommentsToCreate > 0 ? Math.Max(1, commentHoursAvailable / gameCommentsToCreate) : 1;

            for (var j = 0; j < gameCommentsToCreate; j++)
            {
                var commentOffsetHours = Math.Min(j * hoursPerComment + Random.Shared.Next(0, hoursPerComment), Math.Max(1, commentHoursAvailable - 1));
                var commentAuthor = users[Random.Shared.Next(users.Count)];
                var gameComment = new DbComment
                {
                    CommentId = _guidFactory.Create(),
                    EntityId = game.GameId,
                    AuthorId = commentAuthor.UserId,
                    CreatedUtc = commentBaseTime.AddHours(commentOffsetHours),
                    Text = gameCommentTexts[Random.Shared.Next(gameCommentTexts.Length)],
                    IsRemoved = false
                };

                _dbContext.Set<DbComment>().Add(gameComment);
                game.CommentCount++;
                game.LastCommentId = gameComment.CommentId;
            }

            // Batch save after each game to avoid memory pressure from thousands of tracked entities
            await _dbContext.SaveChangesAsync();

            // Reload the game to get the auto-generated SerialNumber
            await _dbContext.Entry(game).ReloadAsync();

            // Update PublicId from SerialNumber (which was auto-generated on insert)
            game.PublicId = _publicIdService.Encode(game.SerialNumber);
            await _dbContext.SaveChangesAsync();
        }

        var skippedGames = existingTitles.Count;
        if (result.GamesCreated > 0)
        {
            result.Details.Add($"Created {result.GamesCreated} new games (skipped {skippedGames} existing)");
        }
        else if (skippedGames > 0)
        {
            result.Details.Add($"All {skippedGames} games already exist");
        }

        // Check if any existing games need characters/posts seeded
        var gamesWithoutCharacters = existingGames.Where(g => g.Characters.Count == 0 && g.Status != ModuleStatus.Draft).ToList();
        if (gamesWithoutCharacters.Count > 0)
        {
            result.Details.Add($"Seeding characters/posts for {gamesWithoutCharacters.Count} existing games without characters");
            SeedCharactersAndPostsForGames(gamesWithoutCharacters, users, result);
            await _dbContext.SaveChangesAsync();
        }

        if (result.CharactersCreated > 0 || result.PostsCreated > 0)
        {
            result.Details.Add($"Created {result.CharactersCreated} characters and {result.PostsCreated} posts total");
        }

        return gameIds;
    }

    private void SeedCharactersAndPostsForGames(List<DbGame> games, List<DbUser> users, ComprehensiveSeedResult result)
    {
        var now = DateTimeOffset.UtcNow;
        var characterNames = new[] { "Арагорн Следопыт", "Эльвира Чародейка", "Горим Железный Кулак", "Лиара Тенебраум", "Кассандра Видящая", "Торин Дубощит" };
        var races = new[] { "Человек", "Эльф", "Дварф", "Полуэльф", "Тифлинг", "Гном" };
        var classes = new[] { "Следопыт", "Маг", "Воин", "Плут", "Жрец", "Паладин" };
        var postTexts = new[]
        {
            "Я осторожно оглядываюсь по сторонам, держа руку на рукояти меча.",
            "Произношу заклинание обнаружения магии, пытаясь понять природу этого места.",
            "Проверяю следы на земле. Кто-то здесь был совсем недавно.",
            "Подхожу к двери и прислушиваюсь.",
            "Готовлю щит и занимаю оборонительную позицию.",
            "Внимательно осматриваю комнату в поисках скрытых проходов."
        };

        foreach (var game in games)
        {
            var room = game.Rooms.FirstOrDefault(r => r.AccessType == RoomAccessType.Open);
            if (room == null)
            {
                // Create a room if none exists
                var maxRoomNumber = game.Rooms.Count > 0 ? game.Rooms.Max(r => r.RoomNumber) : 0;
                room = new Room
                {
                    RoomId = _guidFactory.Create(),
                    GameId = game.GameId,
                    Title = "Главная локация",
                    AccessType = RoomAccessType.Open,
                    Type = RoomType.Default,
                    RoomNumber = maxRoomNumber + 1,
                    OrderNumber = 1.0,
                    ViewPrivateText = false,
                    ViewDiceResults = true,
                    DiceEnabled = true,
                    IsRemoved = false
                };
                _dbContext.Set<Room>().Add(room);
            }

            // Exclude "OnlyReader" from playing games (for testing 0 gamesPlaying tooltip).
            // Take(2) player characters + the single NPC below = 3 active characters per
            // game/room. Keeps demo tooltips (game.activeCharacters, room participants)
            // concise — earlier Take(3) + NPC = 4 characters felt overloaded in UI.
            var playersForGame = users.Where(u => u.UserId != game.MasterId && u.Username != "OnlyReader").OrderBy(_ => Random.Shared.Next()).Take(2).ToList();
            var createdCharacters = new List<Character>();

            // Create characters
            for (var ci = 0; ci < playersForGame.Count; ci++)
            {
                var player = playersForGame[ci];
                var character = new Character
                {
                    CharacterId = _guidFactory.Create(),
                    GameId = game.GameId,
                    AuthorId = player.UserId,
                    Status = CharacterStatus.Active,
                    IsDead = false,
                    IsPlayerLeft = false,
                    IsPlayerExiled = false,
                    CreatedUtc = game.CreatedUtc.AddDays(Random.Shared.Next(1, 7)),
                    Name = characterNames[ci % characterNames.Length],
                    Race = races[ci % races.Length],
                    Class = classes[ci % classes.Length],
                    Alignment = (Alignment?)(ci % 9),
                    Appearance = "Высокий, крепкого телосложения.",
                    Temper = "Решительный и отважный.",
                    Story = "Родился в маленькой деревне...",
                    Skills = "Владение мечом, выживание.",
                    Inventory = "Меч, лук, рюкзак.",
                    IsNpc = false,
                    AccessPolicy = CharacterAccessPolicy.NoAccess,
                    IsRemoved = false
                };

                _dbContext.Set<Character>().Add(character);
                createdCharacters.Add(character);
                result.CharactersCreated++;
            }

            // Create NPC
            var npc = new Character
            {
                CharacterId = _guidFactory.Create(),
                GameId = game.GameId,
                AuthorId = null,
                Status = CharacterStatus.Active,
                CreatedUtc = game.CreatedUtc,
                Name = "Таинственный Незнакомец",
                Race = "Неизвестно",
                Class = "Неизвестно",
                Appearance = "Фигура, скрытая тенью.",
                IsNpc = true,
                AccessPolicy = CharacterAccessPolicy.NoAccess,
                IsRemoved = false
            };
            _dbContext.Set<Character>().Add(npc);
            createdCharacters.Add(npc);
            result.CharactersCreated++;

            // Create posts
            var activeChars = createdCharacters.Where(c => c.Status == CharacterStatus.Active).ToList();
            if (activeChars.Count > 0 && game.ActivatedUtc.HasValue)
            {
                var postsToCreate = 10;
                var baseTime = game.ActivatedUtc.Value;
                // Spread posts between activation and now (or closed date)
                var endTime = game.ClosedUtc ?? now;
                var totalHoursAvailable = (int)(endTime - baseTime).TotalHours;
                var hoursPerPost = postsToCreate > 0 ? Math.Max(1, totalHoursAvailable / postsToCreate) : 1;

                for (var pi = 0; pi < postsToCreate; pi++)
                {
                    var character = activeChars[pi % activeChars.Count];
                    var postOffsetHours = Math.Min(pi * hoursPerPost + Random.Shared.Next(0, hoursPerPost), Math.Max(1, totalHoursAvailable - 1));
                    var post = new Post
                    {
                        PostId = _guidFactory.Create(),
                        RoomId = room.RoomId,
                        CharacterId = character.CharacterId,
                        AuthorId = character.AuthorId ?? game.MasterId,
                        CreatedUtc = baseTime.AddHours(postOffsetHours),
                        GameText = postTexts[pi % postTexts.Length],
                        IsRemoved = false
                    };
                    _dbContext.Set<Post>().Add(post);
                    result.PostsCreated++;
                }
            }
        }

        result.Details.Add($"Seeded {result.CharactersCreated} characters and {result.PostsCreated} posts for {games.Count} existing games");
    }

        private async Task CreateBlogs(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // Check if blogs already exist
        var existingBlogsCount = await _dbContext.Set<DbBlog>().CountAsync();
        if (existingBlogsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Blogs already exist ({existingBlogsCount}), skipping");
            return;
        }

        var mentor = users.First(u => u.Role == UserRole.Mentor);
        var experiencedUsers = users.Where(u => u.QuantityRating >= 100).ToList();
        var newbieUsers = users.Where(u => u.QuantityRating < 100).ToList();

        if (experiencedUsers.Count == 0) experiencedUsers = users.Take(2).ToList();

        // Local aliases for repeated enum values
        var (Active, Draft, Closed) = (ModuleStatus.Active, ModuleStatus.Draft, ModuleStatus.Closed);
        var (Private, Public) = (DraftVisibility.Private, DraftVisibility.Public);
        var (Approved, Awaiting) = (PremoderationStatus.Approved, PremoderationStatus.AwaitingApproval);

        // Assistants distribution: most blogs (80%) have 0, ~15% have 1, ~5% have 2
        var blogTemplates = new[]
        {
            // Active blogs (12) - spread 0-25, IsNew=true means activated < 7 days ago
            new { Title = "Заметки мастера", Description = "Советы по ведению игр и созданию сюжетов", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 25, IsNew = false, AssistantCount = 2 },
            new { Title = "Дневник приключенца", Description = "Истории из игр глазами игрока", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 20, IsNew = false, AssistantCount = 0 },
            new { Title = "Мир фэнтези", Description = "Обзоры сеттингов и игровых миров", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 16, IsNew = true, AssistantCount = 1 },
            new { Title = "Кухня мастера", Description = "Как готовить интересные сессии", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 12, IsNew = false, AssistantCount = 0 },
            new { Title = "Истории персонажей", Description = "Галерея лучших персонажей наших игр", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 10, IsNew = true, AssistantCount = 0 },
            new { Title = "Системный подход", Description = "Анализ разных игровых систем", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 8, IsNew = false, AssistantCount = 1 },
            new { Title = "Творческая мастерская", Description = "Советы по написанию постов и описаний", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 5, IsNew = false, AssistantCount = 0 },
            new { Title = "Новости ролевок", Description = "Обзоры новинок и событий в мире RPG", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 4, IsNew = true, AssistantCount = 0 },
            new { Title = "Путеводитель по системам", Description = "Сравнение и обзор игровых систем", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 3, IsNew = false, AssistantCount = 0 },
            new { Title = "Архив сессий", Description = "Записи и отчеты с прошедших игр", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 2, IsNew = false, AssistantCount = 0 },
            new { Title = "D&D для всех", Description = "Все о Dungeons & Dragons", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 1, IsNew = false, AssistantCount = 0 },
            new { Title = "Киберпанк-хроники", Description = "Блог о киберпанк-играх", Status = Active, DraftVisibility = Public, Premod = Approved, Readers = 0, IsNew = false, AssistantCount = 0 },
            // Closed blogs (3) - spread 0-8
            new { Title = "Советы новичкам", Description = "Как начать играть в ролевые игры", Status = Closed, DraftVisibility = Public, Premod = Approved, Readers = 8, IsNew = false, AssistantCount = 0 },
            new { Title = "Мастерская сюжетов", Description = "Идеи для квестов и приключений", Status = Closed, DraftVisibility = Public, Premod = Approved, Readers = 4, IsNew = false, AssistantCount = 1 },
            new { Title = "Голос игрока", Description = "Рассказы и впечатления от игр", Status = Closed, DraftVisibility = Public, Premod = Approved, Readers = 0, IsNew = false, AssistantCount = 0 },
            // Draft blogs (3): AwaitingApproval = Private (newbie), Approved = Public (experienced)
            new { Title = "Мой первый блог", Description = "Пробую писать", Status = Draft, DraftVisibility = Private, Premod = Awaiting, Readers = 0, IsNew = false, AssistantCount = 0 },
            new { Title = "Черновик идей", Description = "Заготовки для будущих статей", Status = Draft, DraftVisibility = Public, Premod = Approved, Readers = 0, IsNew = false, AssistantCount = 0 },
            new { Title = "Планы на будущее", Description = "Скоро здесь будет интересно", Status = Draft, DraftVisibility = Private, Premod = Awaiting, Readers = 0, IsNew = false, AssistantCount = 0 },
        };

        for (var bi = 0; bi < blogTemplates.Length; bi++)
        {
            var template = blogTemplates[bi];
            var isNewbieOwner = template.Premod == PremoderationStatus.AwaitingApproval;
            var owner = isNewbieOwner && newbieUsers.Count > 0
                ? newbieUsers[0]
                : experiencedUsers[bi % experiencedUsers.Count];

            // Generate realistic dates based on blog status and age
            // Draft: recent (1-14 days)
            // Active "new" (IsNew=true): created 7-14 days ago, activated 1-5 days ago
            // Active established (high readers): old (90-365 days)
            // Active newer (lower readers): medium (30-90 days)
            // Closed: old blogs that were active for some time, then closed
            DateTimeOffset blogCreatedUtc;
            DateTimeOffset? blogActivatedUtc;
            DateTimeOffset? blogClosedUtc = null;

            if (template.Status == ModuleStatus.Draft)
            {
                // Drafts are recent - work in progress
                blogCreatedUtc = now.AddDays(-Random.Shared.Next(1, 14));
                blogActivatedUtc = null;
            }
            else if (template.Status == ModuleStatus.Closed)
            {
                // Closed blogs: were active for a while, then closed 30-180 days ago
                var daysAgoCreated = Random.Shared.Next(180, 540); // Created 6-18 months ago
                blogCreatedUtc = now.AddDays(-daysAgoCreated);
                var activationDelay = Random.Shared.Next(1, 7);
                blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
                // Blog was active for 60-180 days before closing
                var activeDuration = Random.Shared.Next(60, 180);
                blogClosedUtc = blogActivatedUtc.Value.AddDays(activeDuration);
                // Make sure closedUtc is in the past (at least 30 days ago)
                if (blogClosedUtc > now.AddDays(-30))
                {
                    blogClosedUtc = now.AddDays(-Random.Shared.Next(30, 90));
                }
            }
            else if (template.IsNew)
            {
                // "New" blogs: created recently, activated within last week
                var daysAgoCreated = Random.Shared.Next(7, 14);
                blogCreatedUtc = now.AddDays(-daysAgoCreated);
                blogActivatedUtc = now.AddDays(-Random.Shared.Next(1, 5));
            }
            else if (template.Readers >= 8)
            {
                // Popular established blogs: older, been around a while
                var daysAgoCreated = Random.Shared.Next(180, 365);
                blogCreatedUtc = now.AddDays(-daysAgoCreated);
                var activationDelay = Random.Shared.Next(1, 7);
                blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
            }
            else if (template.Readers >= 4)
            {
                // Medium popularity: medium age
                var daysAgoCreated = Random.Shared.Next(60, 180);
                blogCreatedUtc = now.AddDays(-daysAgoCreated);
                var activationDelay = Random.Shared.Next(1, 5);
                blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
            }
            else
            {
                // Lower popularity: newer
                var daysAgoCreated = Random.Shared.Next(30, 90);
                blogCreatedUtc = now.AddDays(-daysAgoCreated);
                var activationDelay = Random.Shared.Next(1, 3);
                blogActivatedUtc = blogCreatedUtc.AddDays(activationDelay);
            }

            var blog = new DbBlog
            {
                BlogId = _guidFactory.Create(),
                AuthorId = owner.UserId,
                Title = template.Title,
                Description = template.Description,
                CreatedUtc = blogCreatedUtc,
                Status = template.Status,
                ActivatedUtc = blogActivatedUtc,
                ClosedUtc = blogClosedUtc,
                PremoderationStatus = template.Premod,
                MentorId = isNewbieOwner ? mentor.UserId : null,
                DraftVisibility = template.DraftVisibility,
                CommentsEnabled = true,
                PublicationCount = 0,
                CommentCount = 0,
                IsRemoved = false,
                // Temporary placeholder - will be updated after SaveChanges
                PublicId = $"t{_guidFactory.Create():N}"[..10]
            };

            _dbContext.Set<DbBlog>().Add(blog);
            result.BlogsCreated++;

            // Create rubrics
            var rubrics = new[]
            {
                new Rubric { RubricId = _guidFactory.Create(), BlogId = blog.BlogId, Title = "Общее", AccessType = RubricAccessType.Open, SortOrder = 1, CreatedUtc = blog.CreatedUtc, IsArchived = false, IsRemoved = false },
                new Rubric { RubricId = _guidFactory.Create(), BlogId = blog.BlogId, Title = "Для избранных", AccessType = RubricAccessType.Private, SortOrder = 2, CreatedUtc = blog.CreatedUtc, IsArchived = false, IsRemoved = false },
            };

            foreach (var rubric in rubrics)
            {
                _dbContext.Set<Rubric>().Add(rubric);
            }

            // Skip publications for draft blogs
            if (template.Status == ModuleStatus.Draft) continue;

            // Create publications - varied templates for different blogs
            var allPublicationTemplates = new[]
            {
                ("Как начать свою первую игру", "В этой статье я расскажу о подготовке к первой сессии. Главное — не бояться ошибок!"),
                ("Топ-5 ошибок начинающих мастеров", "Разбираем типичные ошибки и способы их избежать."),
                ("Создание интересных NPC", "Как сделать неигровых персонажей запоминающимися."),
                ("Секреты атмосферы в играх", "Как создать погружение для игроков с помощью описаний и музыки."),
                ("Баланс боя и ролеплея", "Как найти золотую середину между боевкой и отыгрышем."),
                ("Работа со сложными игроками", "Советы по разрешению конфликтов за игровым столом."),
                ("Импровизация для мастера", "Когда план рушится — как выкрутиться и сделать сессию интересной."),
                ("Обзор системы D&D 5e", "Плюсы и минусы самой популярной системы для начинающих."),
                ("Построение сюжетной арки", "Как спланировать кампанию от начала до эпичного финала."),
                ("Персонаж как часть истории", "Как интегрировать бэкграунд персонажей в общий сюжет."),
                ("Музыка для ролевых игр", "Подборка саундтреков для разных жанров и ситуаций."),
                ("Мои любимые монстры", "Обзор интересных противников и как их правильно использовать."),
            };

            // Select 3 publications for this blog (rotating through templates)
            var publicationTemplates = allPublicationTemplates.Skip((bi * 3) % allPublicationTemplates.Length).Take(3).ToArray();
            if (publicationTemplates.Length < 3)
            {
                publicationTemplates = allPublicationTemplates.Take(3).ToArray();
            }

            // Calculate available time range for publications
            // For closed blogs: between activation and closing
            // For active blogs: between activation and now
            var blogEndDate = blogClosedUtc ?? now;
            var blogAgeInDays = blogActivatedUtc.HasValue
                ? (int)(blogEndDate - blogActivatedUtc.Value).TotalDays
                : 1;
            var pubIndex = 0;

            foreach (var (title, content) in publicationTemplates)
            {
                // Spread publications evenly across the blog's lifetime
                var pubOffsetDays = blogAgeInDays > 3
                    ? (pubIndex * blogAgeInDays / publicationTemplates.Length) + Random.Shared.Next(1, Math.Max(2, blogAgeInDays / publicationTemplates.Length))
                    : pubIndex + 1;
                pubOffsetDays = Math.Min(pubOffsetDays, Math.Max(1, blogAgeInDays - 1));
                var pubCreatedUtc = (blogActivatedUtc ?? blog.CreatedUtc).AddDays(pubOffsetDays);
                pubIndex++;

                var publication = new Publication
                {
                    PublicationId = _guidFactory.Create(),
                    BlogId = blog.BlogId,
                    AuthorId = owner.UserId,
                    RubricId = rubrics[0].RubricId,
                    PublicationNumber = pubIndex, // Unique within blog
                    Title = title,
                    Content = content + "\n\nЭто пример содержания публикации. Здесь может быть гораздо больше текста с форматированием.",
                    Preview = content[..Math.Min(100, content.Length)] + "...",
                    CreatedUtc = pubCreatedUtc,
                    IsPublished = true,
                    PublishedUtc = pubCreatedUtc,
                    CommentsEnabled = true,
                    ViewCount = Random.Shared.Next(10, 500),
                    CommentCount = 0,
                    IsRemoved = false
                };

                _dbContext.Set<Publication>().Add(publication);
                blog.PublicationCount++;
                result.PublicationsCreated++;

                // Add publication comments
                var pubCommentTexts = new[]
                {
                    "Отличная статья, спасибо!",
                    "Очень полезно, особенно для новичков.",
                    "Согласен с автором, все так и есть.",
                    "Интересный взгляд на тему.",
                    "Жду еще статей!",
                };

                var pubCommentsToCreate = Random.Shared.Next(2, 5);
                // For closed blogs, comments should be before closing; for active - before now
                var commentsEndDate = blogClosedUtc ?? now;
                var hoursAvailable = (int)(commentsEndDate - publication.PublishedUtc!.Value).TotalHours;
                for (var pc = 0; pc < pubCommentsToCreate; pc++)
                {
                    // Spread comments evenly within available time
                    var commentOffsetHours = hoursAvailable > pubCommentsToCreate
                        ? (pc * hoursAvailable / pubCommentsToCreate) + Random.Shared.Next(1, Math.Max(2, hoursAvailable / pubCommentsToCreate))
                        : pc + 1;
                    commentOffsetHours = Math.Min(commentOffsetHours, Math.Max(1, hoursAvailable - 1));

                    var commentAuthor = users[Random.Shared.Next(users.Count)];
                    var pubComment = new DbComment
                    {
                        CommentId = _guidFactory.Create(),
                        EntityId = publication.PublicationId,
                        AuthorId = commentAuthor.UserId,
                        CreatedUtc = publication.PublishedUtc.Value.AddHours(commentOffsetHours),
                        Text = pubCommentTexts[Random.Shared.Next(pubCommentTexts.Length)],
                        IsRemoved = false
                    };

                    _dbContext.Set<DbComment>().Add(pubComment);
                    publication.CommentCount++;
                    publication.LastCommentId = pubComment.CommentId;
                    blog.CommentCount++;
                }
            }

            // Add blog assistants (most blogs have 0, few have 1, very few have 2)
            if (template.AssistantCount > 0)
            {
                var availableForAssistant = users
                    .Where(u => u.UserId != owner.UserId)
                    .OrderBy(_ => Random.Shared.Next())
                    .Take(template.AssistantCount)
                    .ToList();

                for (var ai = 0; ai < availableForAssistant.Count; ai++)
                {
                    _dbContext.Set<BlogAssistant>().Add(new BlogAssistant
                    {
                        BlogAssistantId = _guidFactory.Create(),
                        BlogId = blog.BlogId,
                        UserId = availableForAssistant[ai].UserId,
                        JoinedUtc = blog.CreatedUtc.AddDays(Random.Shared.Next(3, 30))
                    });
                }
            }

            // Add readers based on template
            var readerCount = Math.Min(template.Readers, users.Count - 2); // Don't exceed available users
            var readers = users.Where(u => u.UserId != owner.UserId).OrderBy(_ => Random.Shared.Next()).Take(readerCount).ToList();
            foreach (var reader in readers)
            {
                _dbContext.Set<Subscription>().Add(new Subscription
                {
                    SubscriptionId = _guidFactory.Create(),
                    SubscriberId = reader.UserId,
                    TargetType = SubscriptionTargetType.Blog,
                    TargetId = blog.BlogId,
                    Settings = SubscriptionSettings.None,
                    CreatedUtc = blog.CreatedUtc.AddDays(Random.Shared.Next(1, 14))
                });
            }

            // Batch save after each blog
            await _dbContext.SaveChangesAsync();

            // Reload the blog to get the auto-generated SerialNumber
            await _dbContext.Entry(blog).ReloadAsync();

            // Update PublicId from SerialNumber (which was auto-generated on insert)
            blog.PublicId = _publicIdService.Encode(blog.SerialNumber);
            await _dbContext.SaveChangesAsync();
        }

        result.Details.Add($"Created {result.BlogsCreated} blogs with {result.PublicationsCreated} publications");
    }

    private async Task CreateGlobalChatMessages(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var globalChatId = Chat.GlobalChatId;

        // Check if global chat exists
        var globalChat = await _dbContext.Set<Chat>().FirstOrDefaultAsync(c => c.ChatId == globalChatId);
        if (globalChat == null)
        {
            globalChat = new Chat
            {
                ChatId = globalChatId,
                Type = ChatType.Global,
                Title = "Глобальный чат"
            };
            _dbContext.Set<Chat>().Add(globalChat);
        }

        // Check if messages already exist
        var existingMessagesCount = await _dbContext.Set<Message>().Where(m => m.ChatId == globalChatId).CountAsync();
        if (existingMessagesCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Global chat messages already exist ({existingMessagesCount}), skipping");
            return;
        }

        var messageTemplates = new[]
        {
            "Привет всем! Как дела?",
            "Кто-нибудь играет сегодня?",
            "Ищу мастера для D&D one-shot",
            "Вчера была отличная сессия!",
            "Новички, не стесняйтесь спрашивать!",
            "Кто хочет присоединиться к нашей игре?",
            "Посоветуйте хорошую систему для новичков",
            "GURPS или Savage Worlds?",
            "Всем хорошего вечера!",
            "Спасибо за помощь!",
            "Когда следующий конкурс?",
            "Поздравляю победителей!",
        };

        Message? lastMessage = null;
        foreach (var text in messageTemplates)
        {
            var author = users[Random.Shared.Next(users.Count)];
            var message = new Message
            {
                MessageId = _guidFactory.Create(),
                UserId = author.UserId,
                ChatId = globalChatId,
                CreatedUtc = now.AddHours(-Random.Shared.Next(1, 168)),
                Text = text,
                IsRemoved = false
            };

            _dbContext.Set<Message>().Add(message);
            lastMessage = message;
            result.MessagesCreated++;
        }

        // Note: LastMessageId will be updated after SaveChangesAsync to avoid circular dependency

        result.Details.Add($"Created {result.MessagesCreated} global chat messages");
    }

    private async Task CreateGlobalChatEvent(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        if (users.Count == 0)
        {
            return;
        }

        var alreadyExists = await _dbContext.Set<GlobalChatEvent>().AnyAsync();
        if (alreadyExists)
        {
            result.Skipped++;
            result.Details.Add("Global chat event already exists, skipping");
            return;
        }

        _dbContext.Set<GlobalChatEvent>().Add(new GlobalChatEvent
        {
            GlobalChatEventId = _guidFactory.Create(),
            Title = "Литературный вечер",
            Description = "Совместное написание историй в прямом эфире — присоединяйтесь!",
            StartsUtc = now.AddDays(2),
            Duration = null,
            IsOpen = true,
            Status = GlobalChatEventStatus.Scheduled,
            CreatedByUserId = users[0].UserId,
            CreatedUtc = now,
        });

        result.Details.Add("Created global chat event");
    }

    private async Task CreateReviews(List<DbUser> users, List<Guid> gameIds, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // Check if testimonials already exist
        var existingTestimonialsCount = await _dbContext.WebsiteTestimonials.CountAsync();
        if (existingTestimonialsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Testimonials already exist ({existingTestimonialsCount}), skipping reviews seeding");
        }

        var experiencedUsers = users.Where(u => u.QuantityRating >= 100).ToList();
        if (experiencedUsers.Count == 0) experiencedUsers = users.Take(3).ToList();

        // Testimonials - website reviews (Text, DaysAgo) tuples for diverse lengths, styles, and dates
        // Enough for pagination testing (>10 per page)
        if (existingTestimonialsCount == 0)
        {
            var testimonials = new (string Text, int DaysAgo)[]
            {
                // Very short testimonial (like "Ня!" from production)
                ("Ня!", 2),

                // Long testimonials (detailed feedback)
                ("Пришел сюда по рекомендации друга около трех лет назад, и с тех пор это стало моим основным хобби. Особенно нравится, что здесь можно найти игры на любой вкус — от классического фэнтези до киберпанка и постапокалипсиса. Мастера в большинстве своем отзывчивые и готовы помочь новичкам разобраться в правилах. Отдельный плюс — возможность играть в своем темпе, не подстраиваясь под чужое расписание. Конечно, бывают и неудачные игры, но это скорее исключение. В целом — отличное место для тех, кто любит писать и создавать истории вместе с другими людьми.", 350),
                ("Начинал здесь как обычный игрок много лет назад. Тогда площадка была совсем другой — простенький форум, несколько десятков активных пользователей, пара сотен игр. Сейчас это полноценное сообщество с тысячами участников, продуманной системой тегов, удобным редактором постов и множеством других функций. Приятно осознавать, что внес свой вклад в развитие проекта. Здесь я нашел не только увлечение, но и настоящих друзей, с некоторыми из которых общаюсь уже вне площадки. Если вы любите писать, придумывать персонажей и погружаться в интересные сюжеты — вам точно сюда.", 300),
                ("Играю здесь с 2019 года. За это время площадка сильно изменилась в лучшую сторону — новый дизайн, удобные уведомления, быстрый поиск игр. Модерация работает оперативно, конфликты решаются справедливо. Единственное, чего не хватает — мобильного приложения, но и веб-версия на телефоне работает нормально. Рекомендую всем, кто хочет попробовать форумные ролевые игры.", 260),
                ("Долго искал площадку для словесок, и DM.am оказался именно тем, что нужно. Простой интерфейс, понятные правила, живое сообщество. Особенно радует, что можно найти игры практически в любом жанре — от классического фэнтези до современных детективов. Мастера отзывчивые, всегда готовы объяснить непонятные моменты. Единственный минус — иногда сложно выбрать, в какую игру вступить, потому что интересных слишком много!", 220),
                ("Пришла сюда по рекомендации подруги и не пожалела. Первые дни было немного страшно — все-таки новое место, незнакомые люди. Но оказалось, что здесь очень приветливо относятся к новичкам. Помогли разобраться с правилами, подсказали хорошие игры для старта. Сейчас у меня уже три активных персонажа в разных играх, и я планирую создать свою собственную игру в ближайшее время.", 180),
                ("Не ожидал, что форумные игры могут быть настолько затягивающими! Начал полгода назад с небольшого персонажа в фэнтези-игре, а теперь уже сам вожу две игры и участвую еще в трех. Формат постовой игры отлично подходит для работающих людей — можно писать когда удобно, не нужно подстраиваться под чужое расписание. Сообщество отзывчивое, всегда можно найти партнеров для игры или просто пообщаться на форуме.", 145),
                ("Площадка, на которой хочется оставаться. Здесь собрались действительно увлеченные люди, которые создают потрясающие истории. Каждая игра — это маленький мир со своими правилами и атмосферой. Очень нравится система тегов, которая помогает быстро найти игру по интересам. Отдельное спасибо разработчикам за удобный редактор постов с поддержкой форматирования.", 110),
                ("Площадка, которая объединяет людей с похожими интересами. Здесь я нашла не просто игры, а настоящее хобби, которое помогает отвлечься от рутины и погрузиться в увлекательные истории. Нравится, что каждый может найти что-то свое — есть игры с акцентом на боевку, есть чисто отыгрышевые, есть смешанные. Модерация адекватная, правила понятные.", 85),
                ("Пишу здесь уже пять лет. За это время сменилось несколько версий сайта, но атмосфера осталась прежней — дружелюбной и творческой. Особенно ценю возможность участвовать в нескольких играх одновременно и писать в удобное время. Форумный формат идеально подходит для тех, кто любит продумывать каждую реплику своего персонажа.", 65),

                // Medium testimonials (typical user feedback)
                ("Удобный интерфейс, дружелюбное комьюнити. Нашел здесь много интересных игр и познакомился с классными людьми.", 50),
                ("Зашел случайно, остался навсегда. Атмосфера здесь особенная — люди действительно любят то, чем занимаются.", 42),
                ("Классное комьюнити, интересные игры, удобный сайт. Что еще нужно?", 35),
                ("Очень нравится возможность участвовать в нескольких играх одновременно. Форматы игр на любой вкус!", 28),
                ("Отличный сайт для тех, кто ценит качественный отыгрыш и интересные сюжеты.", 22),
                ("Дружелюбная атмосфера, интересные люди, захватывающие истории. Что может быть лучше?", 18),

                // Very long testimonial (~1100 chars, multi-paragraph)
                ("Когда я впервые зашел сюда восемь лет назад, думал — ну, поиграю пару месяцев и забуду. Какие там восемь лет! Это место затягивает так, что не замечаешь, как пролетают годы. И дело не только в играх — хотя они здесь потрясающие — а в людях, которых встречаешь на этом пути.\n\nЗдесь я научился писать. Не в смысле грамматики — с этим и раньше было нормально. Научился чувствовать ритм текста, создавать атмосферу, передавать эмоции через слова. Каждый пост — это маленький вызов самому себе: сделать лучше, интереснее, глубже. И когда соигроки отвечают тем же — когда вместе создаете что-то по-настоящему стоящее — это ощущение ни с чем не сравнить.\n\nЕще я нашел здесь настоящих друзей. Людей, с которыми можно обсуждать не только игры, но и жизнь. Некоторых уже встречал вживую, и это было странно и прекрасно одновременно — узнавать голос человека, которого знаешь только по текстам. Кто бы мог подумать, что форум для ролевых игр станет местом, где найдешь единомышленников?\n\nСпасибо этому месту за все. За бессонные ночи над постами, за жаркие споры о лоре, за смех в чате в три часа ночи. За то, что оно просто есть)", 400),

                // Short testimonials (quick impressions)
                ("Лучшее место для текстовых ролевых игр!", 120),
                ("Отличная площадка для новичков. Всегда рады помочь!", 95),
                ("Отличное место для творческих людей. Рекомендую!", 75),
                ("Лучшая площадка для форумных ролевых игр в рунете. Без преувеличения.", 55),
                ("Годы идут, а DM.am остается любимым местом для творчества.", 40),
                ("Хороший сайт для тех, кто любит писать.", 30),
                ("10/10, рекомендую всем любителям ролевых игр!", 15),
                ("Супер! Всем советую!", 8),
            };

            // Each user gets exactly one testimonial (up to available texts)
            var testimonialCount = Math.Min(users.Count, testimonials.Length);
            for (var i = 0; i < testimonialCount; i++)
            {
                var (text, daysAgo) = testimonials[i];
                _dbContext.WebsiteTestimonials.Add(new WebsiteTestimonial
                {
                    WebsiteTestimonialId = _guidFactory.Create(),
                    AuthorId = users[i].UserId,
                    CreatedUtc = now.AddDays(-daysAgo),
                    Text = text,
                    IsRemoved = false
                });
                result.TestimonialsCreated++;
            }
        }

        // Game reviews (only for experienced users)
        var existingGameReviewsCount = await _dbContext.GameReviews.CountAsync();
        if (gameIds.Count > 0 && experiencedUsers.Count >= 2 && existingGameReviewsCount == 0)
        {
            var games = await _dbContext.Set<DbGame>()
                .Where(g => gameIds.Contains(g.GameId) && g.Status != ModuleStatus.Draft)
                .ToListAsync();

            foreach (var game in games.Take(2))
            {
                var reviewer = experiencedUsers.First(u => u.UserId != game.MasterId);
                _dbContext.GameReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.GameReview
                {
                    GameReviewId = _guidFactory.Create(),
                    AuthorId = reviewer.UserId,
                    GameId = game.GameId,
                    CreatedUtc = now.AddDays(-Random.Shared.Next(1, 30)),
                    Text = "Отличная игра! Мастер ведет интересно, сюжет захватывающий.",
                    IsRemoved = false
                });
                result.ReviewsCreated++;
            }
        }

        // User reviews (only between users who played together)
        // User endorsements between experienced users
        var existingEndorsementsCount = await _dbContext.UserEndorsements.CountAsync();
        if (experiencedUsers.Count >= 2 && existingEndorsementsCount == 0)
        {
            var endorser = experiencedUsers[0];
            var target = experiencedUsers[1];
            _dbContext.UserEndorsements.Add(new UserEndorsement
            {
                UserEndorsementId = _guidFactory.Create(),
                AuthorId = endorser.UserId,
                TargetUserId = target.UserId,
                CreatedUtc = now.AddDays(-Random.Shared.Next(1, 60)),
                Text = "Отличный игрок! Всегда вовремя пишет посты, интересные персонажи.",
                IsRemoved = false
            });
            result.ReviewsCreated++;
        }

        // Post reviews - get posts from change tracker OR database (for re-seeds)
        // Skip if post reviews already exist
        var existingPostReviewsCount = await _dbContext.PostReviews.CountAsync();
        if (existingPostReviewsCount > 0)
        {
            return;
        }

        // Find finished game IDs - first try ChangeTracker, then fall back to database
        var finishedGameIds = _dbContext.ChangeTracker.Entries<DbGame>()
            .Where(e => e.Entity.Status == ModuleStatus.Closed && e.Entity.ClosedReason == ClosedReason.Finished)
            .Select(e => e.Entity.GameId)
            .ToList();

        if (finishedGameIds.Count == 0)
        {
            // Fall back to database query for re-seeds
            finishedGameIds = await _dbContext.Set<DbGame>()
                .Where(g => g.Status == ModuleStatus.Closed && g.ClosedReason == ClosedReason.Finished)
                .Select(g => g.GameId)
                .ToListAsync();
        }

        if (finishedGameIds.Count == 0)
        {
            result.Details.Add("No finished games found for post reviews");
            return;
        }

        // Get rooms for finished games - first try ChangeTracker, then fall back to database
        var roomsFromTracker = _dbContext.ChangeTracker.Entries<Room>()
            .Where(e => finishedGameIds.Contains(e.Entity.GameId) && e.Entity.AccessType == RoomAccessType.Open)
            .Select(e => e.Entity)
            .ToList();

        List<Room> rooms;
        if (roomsFromTracker.Count > 0)
        {
            rooms = roomsFromTracker;
        }
        else
        {
            // Fall back to database query
            rooms = await _dbContext.Set<Room>()
                .Where(r => finishedGameIds.Contains(r.GameId) && r.AccessType == RoomAccessType.Open)
                .ToListAsync();
        }

        var roomIds = rooms.Select(r => r.RoomId).ToList();
        var roomToGameId = rooms.ToDictionary(r => r.RoomId, r => r.GameId);

        // Get posts - first try ChangeTracker, then fall back to database
        var postsFromTracker = _dbContext.ChangeTracker.Entries<Post>()
            .Where(e => !e.Entity.IsRemoved && roomIds.Contains(e.Entity.RoomId))
            .Select(e => e.Entity)
            .ToList();

        List<Post> posts;
        if (postsFromTracker.Count > 0)
        {
            posts = postsFromTracker;
        }
        else
        {
            // Fall back to database query
            posts = await _dbContext.Set<Post>()
                .Where(p => !p.IsRemoved && roomIds.Contains(p.RoomId))
                .ToListAsync();
        }

        // Find the Diopside post (long post with ~2000 chars about crystalline creature)
        // This will be "Latest rated" - the most recently reviewed post
        var diopsidePost = posts.FirstOrDefault(p => p.GameText.Contains("Диопсид"));

        // Find a post from "Эльвира Чародейка" for "Best of week" (highest rating this week)
        var elviraCharId = _dbContext.ChangeTracker.Entries<Character>()
            .FirstOrDefault(e => e.Entity.Name == "Эльвира Чародейка" && e.Entity.Status == CharacterStatus.Active)
            ?.Entity.CharacterId;
        var bestOfWeekPost = elviraCharId.HasValue
            ? posts.FirstOrDefault(p => p.CharacterId == elviraCharId.Value)
            : null;
        // Replace the post text with a thematic "burning the mage" RP scene
        if (bestOfWeekPost != null)
            bestOfWeekPost.GameText = "Сжигаю мага";

        // Create "Latest rated" - Diopside post with multiple reviews (most recent 1 hour ago)
        if (diopsidePost != null)
        {
            var gameId = roomToGameId.GetValueOrDefault(diopsidePost.RoomId);
            if (gameId != Guid.Empty)
            {
                var diopsideReviewers = experiencedUsers.Where(u => u.UserId != diopsidePost.AuthorId).Take(4).ToList();

                // Varied reviews for Diopside: 2 positive, 1 neutral, 1 negative (net +1)
                // Some reviews use BBCode for testing rendering
                var diopsideReviewData = new[]
                {
                    (ReviewSign.Positive, """
[quote]"Туман рассеялся, открывая древние руины"[/quote]
Отличное начало! Чувствуется проработка мира и внимание к деталям окружения. Персонаж сразу вызывает интерес.

[spoiler]Особенно понравилось: описание руин и первая встреча с драконом. [b]Атмосфера загадочности[/b] передана очень удачно![/spoiler]
""", -2), // 2 hours ago (SolohinLex master post review at now takes "Latest rated")
                    (ReviewSign.Positive, "Люблю такие детальные описания! [b]Атмосфера на высоте.[/b]", -3), // 3 hours ago
                    (ReviewSign.Neutral, "Нормальный пост. Стиль интересный, но не для всех.", -5), // 5 hours ago
                    (ReviewSign.Negative, """
Слишком длинно на мой вкус.

[spoiler]Развернутая критика: понимаю, что автор старался создать атмосферу, но местами текст затянут. Можно было бы сократить описания без потери смысла.

Впрочем, это субъективно — кому-то такой стиль нравится.[/spoiler]
""", -7), // 7 hours ago
                };

                var qualityDelta = 0;
                for (var i = 0; i < diopsideReviewers.Count && i < diopsideReviewData.Length; i++)
                {
                    var reviewer = diopsideReviewers[i];
                    var (sign, text, hoursAgo) = diopsideReviewData[i];
                    _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                    {
                        PostReviewId = _guidFactory.Create(),
                        AuthorId = reviewer.UserId,
                        PostId = diopsidePost.PostId,
                        PostAuthorId = diopsidePost.AuthorId,
                        GameId = gameId,
                        CreatedUtc = now.AddHours(hoursAgo),
                        Text = text,
                        SignValue = (short)sign,
                        IsRemoved = false
                    });
                    result.ReviewsCreated++;
                    qualityDelta += (int)sign;
                }
                var postAuthor = users.FirstOrDefault(u => u.UserId == diopsidePost.AuthorId);
                if (postAuthor != null) postAuthor.QualityRating += qualityDelta;
            }
        }

        // Create "Best of week" - different post with 5 reviews from earlier today (within current week)
        if (bestOfWeekPost != null)
        {
            var gameId = roomToGameId.GetValueOrDefault(bestOfWeekPost.RoomId);
            if (gameId != Guid.Empty)
            {
                var reviewCount = Math.Min(5, experiencedUsers.Count(u => u.UserId != bestOfWeekPost.AuthorId));
                var reviewersForBest = experiencedUsers.Where(u => u.UserId != bestOfWeekPost.AuthorId).Take(reviewCount).ToList();

                // All positive reviews (net +5) — master's post should clearly be best of the week
                var reviewData = new[]
                {
                    (ReviewSign.Positive, "Отличный пост! Замечательный отыгрыш мастера, атмосфера на высоте."),
                    (ReviewSign.Positive, "Очень атмосферно, браво! Мастер задал отличный тон сцене."),
                    (ReviewSign.Positive, "Красивое описание, мне понравилось. Сразу чувствуется мастерство."),
                    (ReviewSign.Positive, "Прекрасная подача! Мир оживает в каждой строчке."),
                    (ReviewSign.Positive, "Лучший мастерский пост за последнее время, однозначно!"),
                };

                var qualityDelta = 0;
                for (var i = 0; i < reviewersForBest.Count; i++)
                {
                    var reviewer = reviewersForBest[i];
                    var (sign, text) = reviewData[i % reviewData.Length];
                    var reviewDate = now.AddHours(-Random.Shared.Next(2, 13));
                    _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                    {
                        PostReviewId = _guidFactory.Create(),
                        AuthorId = reviewer.UserId,
                        PostId = bestOfWeekPost.PostId,
                        PostAuthorId = bestOfWeekPost.AuthorId,
                        GameId = gameId,
                        CreatedUtc = reviewDate,
                        Text = text,
                        SignValue = (short)sign,
                        IsRemoved = false
                    });
                    result.ReviewsCreated++;
                    qualityDelta += (int)sign;
                }
                var postAuthor = users.FirstOrDefault(u => u.UserId == bestOfWeekPost.AuthorId);
                if (postAuthor != null) postAuthor.QualityRating += qualityDelta;
            }
        }

        // Create master post from SolohinLex — "Latest rated" on homepage
        // Find a finished game where SolohinLex is master, create a master post (no character)
        var testAdmin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        Post? masterPost = null;
        if (testAdmin != null)
        {
            // Find a finished game mastered by SolohinLex
            var masterGameEntry = _dbContext.ChangeTracker.Entries<DbGame>()
                .FirstOrDefault(e => finishedGameIds.Contains(e.Entity.GameId) && e.Entity.MasterId == testAdmin.UserId);
            var masterGameId = masterGameEntry?.Entity.GameId ?? Guid.Empty;
            var masterRoom = masterGameId != Guid.Empty
                ? rooms.FirstOrDefault(r => roomToGameId.GetValueOrDefault(r.RoomId) == masterGameId)
                : null;

            if (masterRoom != null)
            {
                masterPost = new Post
                {
                    PostId = _guidFactory.Create(),
                    RoomId = masterRoom.RoomId,
                    CharacterId = null, // Master post — no character
                    AuthorId = testAdmin.UserId,
                    CreatedUtc = now.AddHours(-3),
                    GameText = "Отвоеванный у отца камин, 2 спальни, телевизор",
                    IsRemoved = false
                };
                _dbContext.Set<Post>().Add(masterPost);
                result.PostsCreated++;

                // 1 positive review at now — makes it "Latest rated" (most recent review)
                var masterReviewer = experiencedUsers.FirstOrDefault(u => u.UserId != testAdmin.UserId);
                if (masterReviewer != null)
                {
                    _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                    {
                        PostReviewId = _guidFactory.Create(),
                        AuthorId = masterReviewer.UserId,
                        PostId = masterPost.PostId,
                        PostAuthorId = testAdmin.UserId,
                        GameId = masterGameId,
                        CreatedUtc = now, // Most recent review → "Latest rated"
                        Text = "Атмосфера на высоте, сразу чувствуется стиль мастера!",
                        SignValue = (short)ReviewSign.Positive,
                        IsRemoved = false
                    });
                    result.ReviewsCreated++;
                    testAdmin.QualityRating += 1;
                }
            }
        }

        // Create a rated post from LongestLoginPossible (tests long username + long character name)
        var longestUser = users.FirstOrDefault(u => u.Username == "LongestLoginPossible");
        Post? longestUserPost = null;
        if (longestUser != null && finishedGameIds.Count > 0)
        {
            var longestGameId = finishedGameIds[0];
            var longestRoom = rooms.FirstOrDefault(r => roomToGameId.GetValueOrDefault(r.RoomId) == longestGameId);
            if (longestRoom != null)
            {
                // Create a character with a long multi-word name
                var longestChar = new Character
                {
                    CharacterId = _guidFactory.Create(),
                    GameId = longestGameId,
                    AuthorId = longestUser.UserId,
                    Status = CharacterStatus.Active,
                    CreatedUtc = now.AddDays(-20),
                    Name = "Сэр Максимилиан фон Штернберг",
                    Race = "Человек",
                    Class = "Паладин",
                    Appearance = "Высокий светловолосый мужчина в сияющих доспехах.",
                    IsNpc = false,
                    AccessPolicy = CharacterAccessPolicy.NoAccess,
                    IsRemoved = false
                };
                _dbContext.Set<Character>().Add(longestChar);
                result.CharactersCreated++;

                longestUserPost = new Post
                {
                    PostId = _guidFactory.Create(),
                    RoomId = longestRoom.RoomId,
                    CharacterId = longestChar.CharacterId,
                    AuthorId = longestUser.UserId,
                    CreatedUtc = now.AddHours(-6),
                    GameText = "Максимилиан поднял забрало и оглядел зал. Руны на платформе мерцали, и в их свете его доспехи отбрасывали мягкие блики на стены. Он покачал головой — за годы странствий он научился не доверять местам, которые выглядят слишком спокойно.",
                    IsRemoved = false
                };
                _dbContext.Set<Post>().Add(longestUserPost);
                result.PostsCreated++;
                longestUser.QuantityRating++;

                // 2 reviews (net +1)
                var longestReviewers = experiencedUsers.Where(u => u.UserId != longestUser.UserId).Take(2).ToList();
                var longestReviewData = new[]
                {
                    (ReviewSign.Positive, "Хороший отыгрыш, чувствуется характер персонажа."),
                    (ReviewSign.Neutral, "Коротковато, но по делу."),
                };
                var longestQualityDelta = 0;
                for (var i = 0; i < longestReviewers.Count && i < longestReviewData.Length; i++)
                {
                    var reviewer = longestReviewers[i];
                    var (sign, text) = longestReviewData[i];
                    _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                    {
                        PostReviewId = _guidFactory.Create(),
                        AuthorId = reviewer.UserId,
                        PostId = longestUserPost.PostId,
                        PostAuthorId = longestUser.UserId,
                        GameId = longestGameId,
                        CreatedUtc = now.AddHours(-Random.Shared.Next(1, 5)),
                        Text = text,
                        SignValue = (short)sign,
                        IsRemoved = false
                    });
                    result.ReviewsCreated++;
                    longestQualityDelta += (int)sign;
                }
                longestUser.QualityRating += longestQualityDelta;
            }
        }

        // Remaining posts - random reviews (excluding already processed)
        var processedPostIds = new HashSet<Guid>();
        if (diopsidePost != null) processedPostIds.Add(diopsidePost.PostId);
        if (bestOfWeekPost != null) processedPostIds.Add(bestOfWeekPost.PostId);
        if (masterPost != null) processedPostIds.Add(masterPost.PostId);
        if (longestUserPost != null) processedPostIds.Add(longestUserPost.PostId);

        // Review texts for variety (some with BBCode for testing)
        var positiveTexts = new[]
        {
            "Отличный пост!",
            "Хорошо написано!",
            "Мне понравилось!",
            "Красиво!",
            "Атмосферно!",
            "Браво!",
            "[b]Очень хорошо![/b] Продолжай в том же духе.",
            "Супер!",
            """
Отличная работа!

[spoiler]Подробнее: особенно понравилось описание окружения. Чувствуется, что автор хорошо понимает своего персонажа.[/spoiler]
""",
            "[quote]Прочитал с удовольствием[/quote]\nОтличный пост, жду продолжения!",
        };
        var neutralTexts = new[]
        {
            "Нормально",
            "Неплохо",
            "Сойдет",
            "Ок",
            "Средненько, но читабельно.",
        };
        var negativeTexts = new[]
        {
            "Можно лучше",
            "Не очень",
            "Слабовато",
            "[spoiler]Критика: текст сыроват, стоит поработать над стилем.[/spoiler]",
        };

        // This week posts - 30 posts with 2-4 reviews each (within last 6 days)
        // Enough for 2+ pages of pagination at 20/page
        var thisWeekPosts = posts.Where(p => !processedPostIds.Contains(p.PostId)).Take(30).ToList();
        foreach (var post in thisWeekPosts)
        {
            processedPostIds.Add(post.PostId);
            var gameId = roomToGameId.GetValueOrDefault(post.RoomId);
            if (gameId == Guid.Empty) continue;

            // 2-4 reviews per post
            var reviewCount = Random.Shared.Next(2, 5);
            var availableReviewers = experiencedUsers.Where(u => u.UserId != post.AuthorId).ToList();

            for (var r = 0; r < reviewCount && r < availableReviewers.Count; r++)
            {
                var reviewer = availableReviewers[r];
                var sign = (ReviewSign)Random.Shared.Next(-1, 2);
                // Within current week: 0-6 days ago
                var reviewDate = now.AddDays(-Random.Shared.Next(0, 6)).AddHours(-Random.Shared.Next(1, 24));
                var text = sign switch
                {
                    ReviewSign.Positive => positiveTexts[Random.Shared.Next(positiveTexts.Length)],
                    ReviewSign.Negative => negativeTexts[Random.Shared.Next(negativeTexts.Length)],
                    _ => neutralTexts[Random.Shared.Next(neutralTexts.Length)]
                };

                _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                {
                    PostReviewId = _guidFactory.Create(),
                    AuthorId = reviewer.UserId,
                    PostId = post.PostId,
                    PostAuthorId = post.AuthorId,
                    GameId = gameId,
                    CreatedUtc = reviewDate,
                    Text = text,
                    SignValue = (short)sign,
                    IsRemoved = false
                });
                result.ReviewsCreated++;
                var postAuthor = users.FirstOrDefault(u => u.UserId == post.AuthorId);
                if (postAuthor != null) postAuthor.QualityRating += (int)sign;
            }
        }

        // Older posts - 8 posts with single reviews (2-4 weeks ago, for history)
        foreach (var post in posts.Where(p => !processedPostIds.Contains(p.PostId)).Take(8))
        {
            var gameId = roomToGameId.GetValueOrDefault(post.RoomId);
            if (gameId == Guid.Empty) continue;

            var reviewer = experiencedUsers.FirstOrDefault(u => u.UserId != post.AuthorId);
            if (reviewer != null)
            {
                var sign = (ReviewSign)Random.Shared.Next(-1, 2);
                // Older: 2-4 weeks ago
                var reviewDate = now.AddDays(-Random.Shared.Next(14, 28)).AddHours(-Random.Shared.Next(1, 24));
                var text = sign switch
                {
                    ReviewSign.Positive => positiveTexts[Random.Shared.Next(positiveTexts.Length)],
                    ReviewSign.Negative => negativeTexts[Random.Shared.Next(negativeTexts.Length)],
                    _ => neutralTexts[Random.Shared.Next(neutralTexts.Length)]
                };

                _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                {
                    PostReviewId = _guidFactory.Create(),
                    AuthorId = reviewer.UserId,
                    PostId = post.PostId,
                    PostAuthorId = post.AuthorId,
                    GameId = gameId,
                    CreatedUtc = reviewDate,
                    Text = text,
                    SignValue = (short)sign,
                    IsRemoved = false
                });
                result.ReviewsCreated++;
                var postAuthor = users.FirstOrDefault(u => u.UserId == post.AuthorId);
                if (postAuthor != null) postAuthor.QualityRating += (int)sign;
            }
        }

        // Create Chuck character in first active recruiting game — "Best of week" on homepage
        // Chuck is a grapefruit-obsessed barbarian whose post gets 7 positive reviews (net +7)
        var activeRecruitingGame = _dbContext.ChangeTracker.Entries<DbGame>()
            .FirstOrDefault(e => e.Entity.Status == ModuleStatus.Active && e.Entity.IsRecruitmentOpen);
        if (activeRecruitingGame != null)
        {
            var chuckGameId = activeRecruitingGame.Entity.GameId;
            var chuckRoom = _dbContext.ChangeTracker.Entries<Room>()
                .FirstOrDefault(e => e.Entity.GameId == chuckGameId && e.Entity.AccessType == RoomAccessType.Open)
                ?.Entity;

            if (chuckRoom != null)
            {
                // Pick a player for Chuck (not the master)
                var chuckPlayer = users.FirstOrDefault(u =>
                    u.UserId != activeRecruitingGame.Entity.MasterId &&
                    u.Username != "SolohinLex");

                if (chuckPlayer != null)
                {
                    var chuckChar = new Character
                    {
                        CharacterId = _guidFactory.Create(),
                        GameId = chuckGameId,
                        AuthorId = chuckPlayer.UserId,
                        Status = CharacterStatus.Active,
                        CreatedUtc = now.AddDays(-10),
                        Name = "Чак",
                        Race = "Человек",
                        Class = "Варвар",
                        Appearance = "Коренастый мужчина с обветренным лицом и мощными руками. На поясе всегда висит мешочек с грейпфрутами.",
                        Temper = "Буйный, но добродушный. Впадает в ярость при виде несправедливости. И при виде апельсинов.",
                        Story = "Бывший фермер из долины Золотых Рощ, где выращивают лучшие грейпфруты континента. Ушел в приключенцы после того, как орки сожгли его плантацию.",
                        Skills = "Двуручное оружие, выживание, кулинария (грейпфрутовые блюда), запугивание.",
                        Inventory = "Двуручный топор, 3 грейпфрута, фляга с грейпфрутовым соком, потрепанная кулинарная книга.",
                        IsNpc = false,
                        AccessPolicy = CharacterAccessPolicy.NoAccess,
                        IsRemoved = false
                    };
                    _dbContext.Set<Character>().Add(chuckChar);
                    result.CharactersCreated++;

                    // Upload Chuck avatar through the real pipeline
                    // (EXIF-strip, WebP _m/_s thumbnails).
                    var chuckBytes = ReadEmbeddedSeedBytes("DM.Web.API.Assets.Seed.Chuck.png");
                    var chuckUpload = await SeedAvatarFromBytesAsync(
                        chuckBytes,
                        declaredContentType: "image/png",
                        sourceFileName: "Chuck.png",
                        type: UploadType.CharacterAvatar,
                        uploadId: _guidFactory.Create(),
                        userId: chuckPlayer.UserId,
                        entityId: chuckChar.CharacterId,
                        now: chuckChar.CreatedUtc);
                    _dbContext.Set<DM.Infrastructure.Persistence.Entities.Shared.Upload>().Add(chuckUpload);

                    // Chuck's magnum opus about grapefruit
                    var chuckPost = new Post
                    {
                        PostId = _guidFactory.Create(),
                        RoomId = chuckRoom.RoomId,
                        CharacterId = chuckChar.CharacterId,
                        AuthorId = chuckPlayer.UserId,
                        CreatedUtc = now.AddHours(-8),
                        GameText = """
                            Чак сел на поваленное бревно, достал из мешка грейпфрут и некоторое время просто держал его в руках. Тяжелый. Теплый от солнца. Идеальный.

                            Он провел большим пальцем по шершавой кожуре и закрыл глаза. Запах — горьковато-сладкий, с нотками утреннего тумана над рощей — ударил в нос, и на мгновение Чак оказался дома. Золотые Рощи. Ряды деревьев до горизонта. Мать на веранде, отец в саду. Корзины, полные розовато-желтых плодов.

                            «Знаете, в чем проблема этого мира?» — произнес он, ни к кому конкретно не обращаясь. Спутники уже привыкли к его монологам. — «Все едят яблоки. Яблоки! Пресные, скучные, предсказуемые яблоки. Ни горечи, ни вызова. Откусил — и забыл. А грейпфрут? Грейпфрут — это диалог. Он не сразу раскрывается. Сначала горчит, потом кислит, потом — вот оно — сладость. Настоящая, заслуженная сладость. Как жизнь, понимаете?»

                            Он аккуратно надрезал кожуру ножом — не топором, хотя мог бы — и начал чистить. Каждую дольку он отделял с почтением хирурга.

                            «Мой дед говорил: покажи мне, как человек ест грейпфрут, и я скажу тебе, кто он. Если морщится и бросает — трус. Если заливает сахаром — слабак. А если ест как есть, с горечью, с кислинкой, с мякотью между зубами — вот это воин.»

                            Чак откусил дольку и жевал медленно, с выражением абсолютного блаженства на лице. Сок потек по бороде. Ему было все равно.

                            «В таверне в Ривенделле однажды попросил грейпфрутовый сок. Трактирщик посмотрел на меня как на сумасшедшего. "У нас есть яблочный", говорит. Яблочный! Я чуть стол не перевернул. Нет, я перевернул. Но потом извинился и заплатил за ремонт. Я же не дикарь. Я варвар, но не дикарь. Есть разница.»

                            Он доел грейпфрут, аккуратно сложил кожуру в мешок — «на сушку, для чая» — и вытер руки о штаны.

                            «Вот когда мы закончим это приключение и я получу свою долю золота — знаете, что я сделаю? Куплю участок земли. Посажу грейпфрутовые деревья. И буду жить. Просто жить. Каждое утро — свежий грейпфрут с дерева. Каждый вечер — грейпфрутовый пирог. По праздникам — грейпфрутовое вино. Рай.»

                            Он помолчал, глядя на закат.

                            «Но сначала надо убить этого дракона. Потому что, по слухам, он сжег три грейпфрутовые рощи к югу отсюда. И за это он ответит.»
                            """,
                        MetagameText = "Это было прекрасно. У меня слезы на глазах.",
                        IsRemoved = false
                    };
                    _dbContext.Set<Post>().Add(chuckPost);
                    result.PostsCreated++;
                    chuckPlayer.QuantityRating++;

                    // 7 positive reviews — net +7, guarantees "Best of week" (> Elvira +5)
                    var chuckReviewers = experiencedUsers
                        .Where(u => u.UserId != chuckPlayer.UserId)
                        .Take(7)
                        .ToList();
                    var chuckReviewTexts = new[]
                    {
                        "Лучший пост, что я читал за последний год. Грейпфрут — это философия.",
                        "Я буквально плакал от смеха. И от красоты. Одновременно.",
                        "Чак — национальное достояние. Этот монолог надо высечь в камне.",
                        "Подписываюсь под каждым словом. Яблоки — это скучно. Грейпфрут — это жизнь.",
                        "Персонаж раскрыт на все сто. Глубина, юмор, драма — все в одном посте.",
                        "Отыгрыш уровня бог. Сцена с трактирщиком — шедевр.",
                        "Жду продолжения грейпфрутовой саги. Это лучше 'Властелина Колец'.",
                    };
                    for (var i = 0; i < chuckReviewers.Count; i++)
                    {
                        _dbContext.PostReviews.Add(new DM.Infrastructure.Persistence.Entities.Game.PostReview
                        {
                            PostReviewId = _guidFactory.Create(),
                            AuthorId = chuckReviewers[i].UserId,
                            PostId = chuckPost.PostId,
                            PostAuthorId = chuckPlayer.UserId,
                            GameId = chuckGameId,
                            CreatedUtc = now.AddHours(-Random.Shared.Next(2, 12)),
                            Text = chuckReviewTexts[i],
                            SignValue = (short)ReviewSign.Positive,
                            IsRemoved = false
                        });
                        result.ReviewsCreated++;
                    }
                    chuckPlayer.QualityRating += 7;
                }
            }
        }

        result.Details.Add($"Created {result.ReviewsCreated} reviews");
    }

    private async Task CreateUserSubscriptions(List<DbUser> users, ComprehensiveSeedResult result)
    {
        // Check if user subscriptions already exist
        var existingUserSubsCount = await _dbContext.Set<Subscription>()
            .Where(s => s.TargetType == SubscriptionTargetType.User)
            .CountAsync();

        if (existingUserSubsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"User subscriptions already exist ({existingUserSubsCount}), skipping");
            return;
        }

        // Create subscriptions: some users follow other users.
        // Per-subscriber Settings exercises ALL three Author*Events bits
        // so the profile UI groups subscribers under games / blogs / topics
        // tabs predictably. Plain UserSubscriptionDefault for the bulk
        // (mirrors what UserSubscribeButton sends from the FE popover) +
        // a few single-category subscribers to exercise the tab-level
        // filtering. InApp channel always on — same as production opt-ins.
        var admin = users.FirstOrDefault(u => u.Username == "SolohinLex");
        var mentor = users.FirstOrDefault(u => u.Username == "TestMentor");
        var honorary = users.FirstOrDefault(u => u.Username == "TestHonorary");

        // Per-index Settings pattern: each tab gets at least one
        // dedicated single-category subscriber + the rest get the
        // default "subscribed to everything" bundle.
        var settingsByIndex = new[]
        {
            SubscriptionSettings.UserSubscriptionDefault,                                // i=0 — all 3 categories
            SubscriptionSettings.AuthorGameEvents | SubscriptionSettings.InApp,          // i=1 — games only
            SubscriptionSettings.AuthorBlogEvents | SubscriptionSettings.InApp,          // i=2 — blogs only
            SubscriptionSettings.AuthorTopicEvents | SubscriptionSettings.InApp,         // i=3 — topics only
            SubscriptionSettings.UserSubscriptionDefault,                                // i=4 — all 3 again
        };
        SubscriptionSettings SettingsFor(int i) =>
            settingsByIndex[i % settingsByIndex.Length];

        var subscribersCreated = 0;

        if (admin != null)
        {
            var idx = 0;
            foreach (var user in users.Where(u => u.UserId != admin.UserId).Take(5))
            {
                _dbContext.Set<Subscription>().Add(new Subscription
                {
                    SubscriptionId = _guidFactory.Create(),
                    SubscriberId = user.UserId,
                    TargetType = SubscriptionTargetType.User,
                    TargetId = admin.UserId,
                    Settings = SettingsFor(idx++),
                });
                subscribersCreated++;
            }
        }

        if (mentor != null)
        {
            var idx = 0;
            foreach (var user in users.Where(u => u.UserId != mentor.UserId && u.UserId != admin?.UserId).Take(3))
            {
                _dbContext.Set<Subscription>().Add(new Subscription
                {
                    SubscriptionId = _guidFactory.Create(),
                    SubscriberId = user.UserId,
                    TargetType = SubscriptionTargetType.User,
                    TargetId = mentor.UserId,
                    Settings = SettingsFor(idx++),
                });
                subscribersCreated++;
            }
        }

        if (honorary != null)
        {
            var idx = 0;
            foreach (var user in users.Where(u => u.UserId != honorary.UserId && u.Role == UserRole.RegularUser).Take(2))
            {
                _dbContext.Set<Subscription>().Add(new Subscription
                {
                    SubscriptionId = _guidFactory.Create(),
                    SubscriberId = user.UserId,
                    TargetType = SubscriptionTargetType.User,
                    TargetId = honorary.UserId,
                    Settings = SettingsFor(idx++),
                });
                subscribersCreated++;
            }
        }

        result.Details.Add($"Created {subscribersCreated} user subscriptions");
    }

    private async Task CreatePolls(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        // Check if polls already exist
        var existingPollsCount = await _pollRepository.Count(new PollsQuery());
        if (existingPollsCount > 0)
        {
            result.Skipped++;
            result.Details.Add($"Polls already exist ({existingPollsCount}), skipping");
            return;
        }

        // ═══════════════════════════════════════════════════════════════════
        // PENDING POLLS (2) - start in future
        // ═══════════════════════════════════════════════════════════════════

        // Pending poll 1 - New Year event setting
        var pendingPoll1 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(3).UtcDateTime,
            EndsUtc = now.AddDays(17).UtcDateTime,
            Title = "Сеттинг для новогоднего ваншота",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Зимняя сказка" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Хоррор" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Киберпанк" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Советский ретро" },
            }
        };
        await _pollRepository.Create(pendingPoll1);
        result.PollsCreated++;

        // Pending poll 2 - Anniversary celebration format
        var pendingPoll2 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(7).UtcDateTime,
            EndsUtc = now.AddDays(21).UtcDateTime,
            Title = "Как отметим юбилей сайта?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Конкурс постов" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Ретроспектива игр" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Марафон ваншотов" },
            }
        };
        await _pollRepository.Create(pendingPoll2);
        result.PollsCreated++;

        // ═══════════════════════════════════════════════════════════════════
        // ACTIVE POLLS (3)
        // ═══════════════════════════════════════════════════════════════════

        // Closed poll - RPG systems preference
        var closedPoll6 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-30).UtcDateTime,
            EndsUtc = now.AddDays(-1).UtcDateTime,
            Title = "Ваша любимая система правил?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "D&D 5e" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Pathfinder" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Savage Worlds" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "GURPS" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Словеска" },
            }
        };
        await _pollRepository.Create(closedPoll6);
        result.PollsCreated++;

        // Add votes to closed poll 6
        var votersForClosed6 = users.Take(Math.Min(6, users.Count)).ToList();
        var optionIds6 = closedPoll6.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForClosed6.Count; i++)
        {
            await _pollRepository.Vote(closedPoll6.Id, optionIds6[i % optionIds6.Count], votersForClosed6[i].UserId);
        }

        // Active poll 2 - Site improvements priority
        var activePoll2 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-1).UtcDateTime,
            EndsUtc = now.AddDays(21).UtcDateTime,
            Title = "Что улучшить на сайте?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Мобильную версию" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Редактор постов" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Поиск игр" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Уведомления" },
            }
        };
        await _pollRepository.Create(activePoll2);
        result.PollsCreated++;

        // Add votes to active poll 2
        var votersForActive2 = users.Skip(2).Take(Math.Min(4, users.Count - 2)).ToList();
        var optionIds2 = activePoll2.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForActive2.Count; i++)
        {
            await _pollRepository.Vote(activePoll2.Id, optionIds2[i % optionIds2.Count], votersForActive2[i].UserId);
        }

        // Active poll 3 - Weekly one-shot time
        var activePoll3 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-5).UtcDateTime,
            EndsUtc = now.AddDays(9).UtcDateTime,
            Title = "Время для еженедельных ваншотов",
            Details = "По субботам, время по МСК",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "14:00" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "16:00" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "18:00" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "20:00" },
            }
        };
        await _pollRepository.Create(activePoll3);
        result.PollsCreated++;

        var votersForActive3 = users.Skip(1).Take(Math.Min(5, users.Count - 1)).ToList();
        var optionIds3 = activePoll3.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForActive3.Count; i++)
        {
            await _pollRepository.Vote(activePoll3.Id, optionIds3[i % optionIds3.Count], votersForActive3[i].UserId);
        }

        // ═══════════════════════════════════════════════════════════════════
        // ENDED POLLS (5)
        // ═══════════════════════════════════════════════════════════════════

        // Ended poll 1 - Favorite genre
        var endedPoll1 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-30).UtcDateTime,
            EndsUtc = now.AddDays(7).UtcDateTime,
            Title = "Ваш любимый жанр?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Фэнтези" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Sci-Fi" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Хоррор" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Современность" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Исторический" },
            }
        };
        await _pollRepository.Create(endedPoll1);

        var votersForEnded1 = users.Skip(1).Take(Math.Min(8, users.Count - 1)).ToList();
        var endedOptionIds1 = endedPoll1.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForEnded1.Count; i++)
        {
            await _pollRepository.Vote(endedPoll1.Id, endedOptionIds1[i % endedOptionIds1.Count], votersForEnded1[i].UserId);
        }
        await _pollRepository.Update(endedPoll1.Id, null, null, null, now.AddDays(-1), null);
        result.PollsCreated++;

        // Ended poll 2 - Post frequency
        var endedPoll2 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-45).UtcDateTime,
            EndsUtc = now.AddDays(7).UtcDateTime,
            Title = "Как часто вы пишете посты?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Каждый день" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Несколько раз в неделю" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Раз в неделю" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Реже" },
            }
        };
        await _pollRepository.Create(endedPoll2);

        var votersForEnded2 = users.Take(Math.Min(6, users.Count)).ToList();
        var endedOptionIds2 = endedPoll2.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForEnded2.Count; i++)
        {
            await _pollRepository.Vote(endedPoll2.Id, endedOptionIds2[i % endedOptionIds2.Count], votersForEnded2[i].UserId);
        }
        await _pollRepository.Update(endedPoll2.Id, null, null, null, now.AddDays(-7), null);
        result.PollsCreated++;

        // Ended poll 3 - Preferred game length
        var endedPoll3 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-90).UtcDateTime,
            EndsUtc = now.AddDays(7).UtcDateTime,
            Title = "Предпочтительная длина игры?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Ваншот (1-2 недели)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Короткая (1-3 месяца)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Средняя (до года)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Долгая (год+)" },
            }
        };
        await _pollRepository.Create(endedPoll3);

        var votersForEnded3 = users.Skip(3).Take(Math.Min(3, users.Count - 3)).ToList();
        var endedOptionIds3 = endedPoll3.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForEnded3.Count; i++)
        {
            await _pollRepository.Vote(endedPoll3.Id, endedOptionIds3[i % endedOptionIds3.Count], votersForEnded3[i].UserId);
        }
        await _pollRepository.Update(endedPoll3.Id, null, null, null, now.AddDays(-30), null);
        result.PollsCreated++;

        // Ended poll 4 - Dark theme request
        var endedPoll4 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-60).UtcDateTime,
            EndsUtc = now.AddDays(7).UtcDateTime,
            Title = "Нужна ли темная тема?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Да" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Было бы неплохо" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Все равно" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Нет" },
            }
        };
        await _pollRepository.Create(endedPoll4);

        // Most users vote for first option (dark theme wanted)
        var votersForEnded4 = users.Take(Math.Min(7, users.Count)).ToList();
        var endedOptionIds4 = endedPoll4.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForEnded4.Count; i++)
        {
            var optionIdx = i < 5 ? 0 : i % endedOptionIds4.Count;
            await _pollRepository.Vote(endedPoll4.Id, endedOptionIds4[optionIdx], votersForEnded4[i].UserId);
        }
        await _pollRepository.Update(endedPoll4.Id, null, null, null, now.AddDays(-14), null);
        result.PollsCreated++;

        // Ended poll 5 - Dice rolling (no details, simple question)
        var endedPoll5 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-120).UtcDateTime,
            EndsUtc = now.AddDays(7).UtcDateTime,
            Title = "Нужны ли кубики на сайте?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Да, встроенные" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Да, через бота" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Нет, хватает внешних" },
            }
        };
        await _pollRepository.Create(endedPoll5);

        var votersForEnded5 = users.Skip(2).Take(Math.Min(4, users.Count - 2)).ToList();
        var endedOptionIds5 = endedPoll5.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForEnded5.Count; i++)
        {
            await _pollRepository.Vote(endedPoll5.Id, endedOptionIds5[i % endedOptionIds5.Count], votersForEnded5[i].UserId);
        }
        await _pollRepository.Update(endedPoll5.Id, null, null, null, now.AddDays(-60), null);
        result.PollsCreated++;

        // Ended poll 6 - Character creation preference
        var endedPoll6 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-100).UtcDateTime,
            EndsUtc = now.AddDays(-70).UtcDateTime,
            Title = "Как вы создаете персонажей?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Начинаю с концепта" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Начинаю с механики" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "По ситуации" },
            }
        };
        await _pollRepository.Create(endedPoll6);
        result.PollsCreated++;

        // Ended poll 7 - PvP attitude
        var endedPoll7 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-110).UtcDateTime,
            EndsUtc = now.AddDays(-80).UtcDateTime,
            Title = "Ваше отношение к PvP?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Люблю PvP" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Иногда интересно" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Предпочитаю кооператив" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Не люблю PvP" },
            }
        };
        await _pollRepository.Create(endedPoll7);
        result.PollsCreated++;

        // Ended poll 8 - Session frequency
        var endedPoll8 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-150).UtcDateTime,
            EndsUtc = now.AddDays(-120).UtcDateTime,
            Title = "Сколько игр вы ведете одновременно?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Одну" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "2-3" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "4-5" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Больше 5" },
            }
        };
        await _pollRepository.Create(endedPoll8);
        result.PollsCreated++;

        // Ended poll 9 - Communication preference
        var endedPoll9 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-180).UtcDateTime,
            EndsUtc = now.AddDays(-150).UtcDateTime,
            Title = "Где обсуждаете игру с мастером?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "В личных сообщениях на сайте" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "В Discord/Telegram" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "В комментариях игры" },
            }
        };
        await _pollRepository.Create(endedPoll9);
        result.PollsCreated++;

        // Ended poll 10 - Combat preference
        var endedPoll10 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-200).UtcDateTime,
            EndsUtc = now.AddDays(-170).UtcDateTime,
            Title = "Что важнее в бою?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Тактика и механика" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Нарратив и описания" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Баланс того и другого" },
            }
        };
        await _pollRepository.Create(endedPoll10);
        result.PollsCreated++;

        // Ended poll 11 - Worldbuilding preference
        var endedPoll11 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-220).UtcDateTime,
            EndsUtc = now.AddDays(-190).UtcDateTime,
            Title = "Какой сеттинг предпочитаете?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Готовые миры (Forgotten Realms, etc.)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Авторские сеттинги" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Совместное создание мира" },
            }
        };
        await _pollRepository.Create(endedPoll11);
        result.PollsCreated++;

        // Ended poll 12 - Long title test
        var endedPoll12 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-240).UtcDateTime,
            EndsUtc = now.AddDays(-210).UtcDateTime,
            Title = "Как вы относитесь к длинным описаниям персонажей, которые включают подробную предысторию, характер, мотивацию и внешность?",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Чем подробнее, тем лучше" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Предпочитаю краткость" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Зависит от игры" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Раскрываю персонажа по ходу игры" },
            }
        };
        await _pollRepository.Create(endedPoll12);
        result.PollsCreated++;

        // Ended poll 13 - Long details test
        var endedPoll13 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-260).UtcDateTime,
            EndsUtc = now.AddDays(-230).UtcDateTime,
            Title = "Формат постов в играх",
            Details = "Мы хотим понять, какой формат постов предпочитает наше сообщество. " +
                      "Это поможет нам лучше настроить редактор и подсказки для новых игроков. " +
                      "Под «коротким постом» мы понимаем 2-5 предложений, под «средним» — 1-3 абзаца, " +
                      "под «длинным» — развернутые описания на несколько экранов. " +
                      "Учитывайте свой обычный стиль игры, а не идеальные пожелания.",
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Короткие посты (2-5 предложений)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Средние посты (1-3 абзаца)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Длинные посты (развернутые описания)" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Адаптируюсь под партию" },
            }
        };
        await _pollRepository.Create(endedPoll13);
        result.PollsCreated++;

        // ═══════════════════════════════════════════════════════════════════
        // PUBLIC POLLS (voters visible)
        // ═══════════════════════════════════════════════════════════════════

        // Public poll - Ended, with votes
        var publicPoll2 = new CreatePollEntity
        {
            Id = _guidFactory.Create(),
            StartsUtc = now.AddDays(-20).UtcDateTime,
            EndsUtc = now.AddDays(-10).UtcDateTime,
            Title = "Оцените прошедшее мероприятие",
            Details = "Открытый опрос для участников ивента.",
            IsAnonymous = false,
            Options = new[]
            {
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Отлично!" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Хорошо" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Могло быть лучше" },
                new CreatePollOptionEntity { Id = _guidFactory.Create(), Text = "Не участвовал" },
            }
        };
        await _pollRepository.Create(publicPoll2);

        // Add votes to public poll 2
        var votersForPublic2 = users.Skip(3).Take(Math.Min(18, users.Count - 3)).ToList();
        var publicOptionIds2 = publicPoll2.Options.Select(o => o.Id).ToList();
        for (var i = 0; i < votersForPublic2.Count; i++)
        {
            await _pollRepository.Vote(publicPoll2.Id, publicOptionIds2[i % publicOptionIds2.Count], votersForPublic2[i].UserId);
        }
        result.PollsCreated++;

        result.Details.Add($"Created {result.PollsCreated} polls (2 pending, 3 active, 15 closed; 2 public)");
    }

    /// <summary>
    /// Прочитать байты embedded-resource картинки seed-аватара. Кидает
    /// <see cref="InvalidOperationException"/> если ресурс не embedded'ed
    /// (deployment misconfig).
    /// </summary>
    private static byte[] ReadEmbeddedSeedBytes(string resourceName)
    {
        var assembly = typeof(ModerationApiService).Assembly;
        using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {resourceName} not found");

        using var ms = new MemoryStream();
        resourceStream.CopyTo(ms);
        return ms.ToArray();
    }
}
