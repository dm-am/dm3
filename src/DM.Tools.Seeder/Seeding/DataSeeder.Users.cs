using System;
using DM.Domain.Core.Tokens;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbPendingRegistration = DM.Infrastructure.Persistence.Entities.Account.PendingRegistration;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// Seed the base development users. Users whose login already exists are skipped.
    /// </summary>
    /// <returns>Created and skipped counts</returns>
    public async Task<SeedResult> SeedTestUsers()
    {

        // The ratings here are a start: the run recomputes QuantityRating from the posts it seeded.
        // All rows are activated; PendingRegistration covers the pending activation. Usernames:
        // docs/conventions/USERNAME_POLICY.md.
        var testAccounts = new TestAccount[]
        {
            // One per role
            new() { Login = PrimaryLogin, Email = "admin@test.local", Role = UserRole.Admin },
            new() { Login = "TestSeniorMod", Email = "seniormod@test.local", Role = UserRole.SeniorModerator },
            new() { Login = "TestModerator", Email = "mod@test.local", Role = UserRole.Moderator },
            new() { Login = "TestMentor", Email = "mentor@test.local", Role = UserRole.Mentor },
            new() { Login = "TestUser", Email = "user@test.local", Role = UserRole.RegularUser },

            // Length
            new() { Login = "Ян", Email = "yan@test.local", Role = UserRole.RegularUser }, // min length (2), cyrillic
            new() { Login = "LongestLoginPossible", Email = "longest@test.local", Role = UserRole.RegularUser }, // max length (20)

            // Special characters
            new() { Login = "Player_One", Email = "player1@test.local", Role = UserRole.RegularUser }, // underscore
            new() { Login = "Player-Two", Email = "player2@test.local", Role = UserRole.RegularUser }, // hyphen
            new() { Login = "Player.Three", Email = "player3@test.local", Role = UserRole.RegularUser }, // dot, and the second factor on (DataSeeder.TwoFactor.cs)
            new() { Login = "Player Four", Email = "player4@test.local", Role = UserRole.RegularUser }, // space

            // Cyrillic
            new() { Login = "Игрок", Email = "igrok@test.local", Role = UserRole.RegularUser }, // cyrillic only
            new() { Login = "Игрок_Один", Email = "igrok1@test.local", Role = UserRole.RegularUser }, // cyrillic + underscore
            new() { Login = "Тест Елки", Email = "yolka@test.local", Role = UserRole.RegularUser }, // cyrillic + space + "Е"

            // Special states
            new() { Login = "TestHonorary", Email = "honorary@test.local", Role = UserRole.RegularUser }, // holds the "Почетный гоблин" award

            // Ratings
            new() { Login = "RatingDisabled", Email = "rating-off@test.local", Role = UserRole.RegularUser }, // rating disabled (n/a)
            new() { Login = "RatingPositive", Email = "rating-pos@test.local", Role = UserRole.RegularUser }, // positive rating
            new() { Login = "RatingNegative", Email = "rating-neg@test.local", Role = UserRole.RegularUser }, // negative rating
            new() { Login = "RatingZero", Email = "rating-zero@test.local", Role = UserRole.RegularUser }, // zero rating
            new() { Login = "Experienced", Email = "experienced@test.local", Role = UserRole.RegularUser }, // 150 posts, not newbie

            // Activity
            new() { Login = "OnlyReader", Email = "reader@test.local", Role = UserRole.RegularUser }, // reads games but never plays (0 gamesPlaying)
        };

        var result = new SeedResult();
        await CreateTestAccounts(testAccounts, TestPassword, result);
        await SeedPendingRegistration(TestPassword, result);

        if (result.Created > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return result;
    }

    /// <summary>A development account: its login, its address and its role.</summary>
    private sealed record TestAccount
    {
        public required string Login { get; init; }

        public required string Email { get; init; }

        public required UserRole Role { get; init; }
    }

    /// <summary>
    /// Writes the test accounts whose login is not taken yet.
    /// </summary>
    private async Task CreateTestAccounts(TestAccount[] testAccounts, string password, SeedResult result)
    {
        var existingLogins = await _dbContext.Users
            .Where(u => testAccounts.Select(a => a.Login.ToLower()).Contains(u.Username.ToLower()))
            .Select(u => u.Username.ToLower())
            .ToListAsync();


        foreach (var account in testAccounts)
        {
            if (existingLogins.Contains(account.Login.ToLower()))
            {
                result.Skipped++;
                result.SkippedUsernames.Add(account.Login);
                continue;
            }

            _dbContext.Users.Add(NewTestUser(account, password));
            result.Created++;
            result.CreatedUsernames.Add(account.Login);
        }
    }

    /// <summary>
    /// The row of one test account, with the ratings, the last activity and the
    /// registration date its login stands for.
    /// </summary>
    private DbUser NewTestUser(TestAccount account, string password)
    {
        var (hash, salt) = _securityManager.GeneratePassword(password);

        var (ratingDisabled, qualityRating, quantityRating) = account.Login switch
        {
            "RatingDisabled" => (true, 0, 50),
            "RatingPositive" => (false, 25, 80),
            "RatingNegative" => (false, -10, 40),
            "RatingZero" => (false, 0, 30),
            "Experienced" => (false, 50, 150), // Not newbie (100+ posts)
            _ => (false, 0, 0)
        };

        var lastActivity = account.Login switch
        {
            "RatingDisabled" => _now.AddHours(-2), // offline (>10 min)
            "RatingNegative" => _now.AddDays(-7), // offline long ago
            _ => _now // online
        };

        // Staff registered earlier, newbies later
        var registeredUtc = account.Login switch
        {
            PrimaryLogin => _now.AddYears(-11).AddDays(-_random.Next(0, 180)), // 11+ years (enough for platinum of "Выслуга лет", T4 = 3650 days)
            "TestSeniorMod" => _now.AddYears(-4).AddDays(-_random.Next(0, 180)), // 4+ years ago
            "TestModerator" => _now.AddYears(-3).AddDays(-_random.Next(0, 180)), // 3+ years ago
            "TestMentor" => _now.AddYears(-2).AddDays(-_random.Next(0, 180)), // 2+ years ago
            "TestHonorary" => _now.AddYears(-6).AddDays(-_random.Next(0, 180)), // 6+ years ago (veteran)
            "Experienced" => _now.AddYears(-2).AddDays(-_random.Next(0, 365)), // 2+ years ago
            "OnlyReader" => _now.AddMonths(-2).AddDays(-_random.Next(0, 30)), // Recent
            _ => _now.AddMonths(-_random.Next(1, 24)).AddDays(-_random.Next(0, 30)) // 1-24 months ago
        };

        return new DbUser
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
            PasswordHashVersion = PasswordHashing.CurrentVersion,
            RatingDisabled = ratingDisabled,
            QualityRating = qualityRating,
            QuantityRating = quantityRating,
            Status = string.Empty,
            Name = string.Empty,
            Location = string.Empty,
        };
    }

    /// <summary>
    /// The registration that waits for its letter, for the PendingActivation flow.
    /// </summary>
    private async Task SeedPendingRegistration(string password, SeedResult result)
    {

        var pendingEmail = "inactive@test.local";
        var pendingExists = await _dbContext.PendingRegistrations
            .AnyAsync(p => p.Email == pendingEmail);

        if (!pendingExists)
        {
            var (hash, salt) = _securityManager.GeneratePassword(password);
            var pending = new DbPendingRegistration
            {
                PendingRegistrationId = _guidFactory.Create(),
                // The row keeps the hash; the secret exists only in the letter, and there is no letter.
                SecretHash = ConfirmationSecret.Hash(_guidFactory.Create()),
                Email = pendingEmail,
                PasswordHash = hash,
                Salt = salt,
                PasswordHashVersion = PasswordHashing.CurrentVersion,
                CreatedUtc = _now,
                TokenCreatedUtc = _now,
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
    }

    private Task UpdateUserProfiles(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var profiles = new (string Username, string Name, string Status, string Location, string Info, Gender Gender, int QuantityRating)[]
        {
            (PrimaryLogin, "Алексей Солохин", "Слежу за порядком", "Москва", "Администратор сайта с многолетним опытом. Отвечаю за техническую часть и модерацию.", Gender.Male, 1500),
            ("TestSeniorMod", "Старший Модератор", "На страже правил", "Санкт-Петербург", "Помогаю поддерживать дружелюбную атмосферу на сайте. Обращайтесь с вопросами!", Gender.Female, 800),
            ("TestModerator", "Модератор Форума", "Читаю все", "Новосибирск", "Модерирую форум и помогаю новичкам освоиться.", Gender.Male, 400),
            ("TestMentor", "Опытный Наставник", "Учу мастерству", "Екатеринбург", "Наставник для начинающих мастеров. Провожу игры уже 10 лет.", Gender.Male, 1200),
            ("TestUser", "Активный Игрок", "Ищу новые приключения", "Казань", "Люблю фэнтези и sci-fi. Играю за воинов и магов.", Gender.Male, 300),
            ("TestHonorary", "Почетный Гоблин", "Ветеран сообщества", "Нижний Новгород", "Один из первых участников сайта. Почетный гоблин с 2015 года.", Gender.Male, 2000),
            ("Ян", "Ян", "Минималист", "Владивосток", "Краткость — сестра таланта.", Gender.Male, 80),
            ("Player_One", "Игрок Первый", "Ready Player One", "Краснодар", "Геймер и ролевик. Люблю D&D 5e и Pathfinder.", Gender.Male, 250),
            ("Player-Two", "Игрок Второй", "Второй не значит худший", "Самара", "Мастер интриг и политических игр.", Gender.Female, 350),
            ("Player.Three", "Игрок Третий", "Точка — это стиль", "Ростов-на-Дону", "Специализируюсь на horror-играх и мистике.", Gender.Unknown, 150),
            ("Player Four", "Игрок Четвертый", "Пробелы разрешены", "Воронеж", "Новичок, но учусь быстро!", Gender.Male, 45),
            ("Игрок", "Русский Игрок", "Только кириллица", "Омск", "Предпочитаю русскоязычные игры.", Gender.Male, 200),
            ("Игрок_Один", "Первый Русский", "Кириллица и символы", "Челябинск", "Люблю славянское фэнтези.", Gender.Female, 120),
            ("Тест Елки", "Тестовая Елка", "С буквой Е", "Уфа", "Проверяю поддержку буквы Е в системе.", Gender.Unknown, 40),
            ("LongestLoginPossible", "Длинное Имя", "Максимальная длина", "Пермь", "У меня самый длинный логин на сайте!", Gender.Male, 100),
            ("OnlyReader", "Только Читатель", "Читаю, не играю", "Тула", "Люблю читать игры, но сам не участвую.", Gender.Male, 50),
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
                user.QuantityRating = profile.QuantityRating;
                user.LastActivityUtc = _now.AddMinutes(-_random.Next(1, 1440));
            }
        }

        result.Details.Add("User profiles updated");
        return Task.CompletedTask;
    }

    /// <summary>
    /// The Settings each subscriber of one author gets, by the subscriber's
    /// place in line, so that all three Author*Events bits are exercised.
    /// </summary>
    /// <remarks>
    /// Most get UserSubscriptionDefault, as the subscribe popover sends it; a few get one category each.
    /// </remarks>
    private static readonly SubscriptionSettings[] AuthorSubscriptionSettings =
    [
        SubscriptionSettings.UserSubscriptionDefault,                                // i=0 — all 3 categories
        SubscriptionSettings.AuthorGameEvents | SubscriptionSettings.InApp,          // i=1 — games only
        SubscriptionSettings.AuthorBlogEvents | SubscriptionSettings.InApp,          // i=2 — blogs only
        SubscriptionSettings.AuthorTopicEvents | SubscriptionSettings.InApp,         // i=3 — topics only
        SubscriptionSettings.UserSubscriptionDefault,                                // i=4 — all 3 again
    ];

    private async Task CreateUserSubscriptions(List<DbUser> users, ComprehensiveSeedResult result)
    {
        var existingUserSubsCount = await _dbContext.Set<Subscription>()
            .Where(s => s.TargetType == SubscriptionTargetType.User)
            .CountAsync();

        if (existingUserSubsCount > 0)
        {
            result.Details.Add($"User subscriptions already exist ({existingUserSubsCount}), skipping");
            return;
        }

        var admin = users.FirstOrDefault(u => u.Username == PrimaryLogin);
        var mentor = users.FirstOrDefault(u => u.Username == "TestMentor");
        var honorary = users.FirstOrDefault(u => u.Username == "TestHonorary");

        var subscribersCreated = 0;

        if (admin != null)
        {
            subscribersCreated += SubscribeToAuthor(admin, users.Where(u => u.UserId != admin.UserId).Take(5));
        }

        if (mentor != null)
        {
            subscribersCreated += SubscribeToAuthor(
                mentor, users.Where(u => u.UserId != mentor.UserId && u.UserId != admin?.UserId).Take(3));
        }

        if (honorary != null)
        {
            subscribersCreated += SubscribeToAuthor(
                honorary, users.Where(u => u.UserId != honorary.UserId && u.Role == UserRole.RegularUser).Take(2));
        }

        result.Details.Add($"Created {subscribersCreated} user subscriptions");
    }

    /// <summary>
    /// Subscribes the readers to an author, in order, and returns how many.
    /// </summary>
    private int SubscribeToAuthor(DbUser author, IEnumerable<DbUser> subscribers)
    {
        var idx = 0;
        foreach (var user in subscribers)
        {
            _dbContext.Set<Subscription>().Add(new Subscription
            {
                SubscriptionId = _guidFactory.Create(),
                SubscriberId = user.UserId,
                TargetType = SubscriptionTargetType.User,
                TargetId = author.UserId,
                Settings = AuthorSubscriptionSettings[idx++ % AuthorSubscriptionSettings.Length],
            });
        }

        return idx;
    }
}
