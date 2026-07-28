using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Dto;
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
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.Extensions.Options;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;
using DbBbCodeConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints;
using DbListConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints;
using DbListValueKind = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListValueKind;
using DbListAttributeValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;
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
            new { Login = "SolohinLex", Email = "admin@test.local", Role = UserRole.Admin },
            new { Login = "TestSeniorMod", Email = "seniormod@test.local", Role = UserRole.SeniorModerator },
            new { Login = "TestModerator", Email = "mod@test.local", Role = UserRole.Moderator },
            new { Login = "TestMentor", Email = "mentor@test.local", Role = UserRole.Mentor },
            new { Login = "TestUser", Email = "user@test.local", Role = UserRole.RegularUser },

            // === Edge cases: length ===
            new { Login = "Ян", Email = "yan@test.local", Role = UserRole.RegularUser }, // min length (2), cyrillic
            new { Login = "LongestLoginPossible", Email = "longest@test.local", Role = UserRole.RegularUser }, // max length (20)

            // === Edge cases: special characters ===
            new { Login = "Player_One", Email = "player1@test.local", Role = UserRole.RegularUser }, // underscore
            new { Login = "Player-Two", Email = "player2@test.local", Role = UserRole.RegularUser }, // hyphen
            new { Login = "Player.Three", Email = "player3@test.local", Role = UserRole.RegularUser }, // dot
            new { Login = "Player Four", Email = "player4@test.local", Role = UserRole.RegularUser }, // space

            // === Edge cases: cyrillic ===
            new { Login = "Игрок", Email = "igrok@test.local", Role = UserRole.RegularUser }, // cyrillic only
            new { Login = "Игрок_Один", Email = "igrok1@test.local", Role = UserRole.RegularUser }, // cyrillic + underscore
            new { Login = "Тест Елки", Email = "yolka@test.local", Role = UserRole.RegularUser }, // cyrillic + space + Е

            // === Special states ===
            new { Login = "TestHonorary", Email = "honorary@test.local", Role = UserRole.RegularUser }, // holds the "Почетный гоблин" award

            // === Rating test cases ===
            new { Login = "RatingDisabled", Email = "rating-off@test.local", Role = UserRole.RegularUser }, // rating disabled (n/a)
            new { Login = "RatingPositive", Email = "rating-pos@test.local", Role = UserRole.RegularUser }, // positive rating
            new { Login = "RatingNegative", Email = "rating-neg@test.local", Role = UserRole.RegularUser }, // negative rating
            new { Login = "RatingZero", Email = "rating-zero@test.local", Role = UserRole.RegularUser }, // zero rating
            new { Login = "Experienced", Email = "experienced@test.local", Role = UserRole.RegularUser }, // 150 posts, not newbie

            // === Activity test cases ===
            new { Login = "OnlyReader", Email = "reader@test.local", Role = UserRole.RegularUser }, // reads games but never plays (0 gamesPlaying)
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
                "SolohinLex" => now.AddYears(-11).AddDays(-Random.Shared.Next(0, 180)), // 11+ years (enough for platinum of "Выслуга лет", T4 = 3650 days)
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

    private Task UpdateUserProfiles(List<DbUser> users, DateTimeOffset now, ComprehensiveSeedResult result)
    {
        var profiles = new (string Username, string Name, string Status, string Location, string Info, Gender Gender, int QualityRating, int QuantityRating)[]
        {
            ("SolohinLex", "Алексей Солохин", "Слежу за порядком", "Москва", "Администратор сайта с многолетним опытом. Отвечаю за техническую часть и модерацию.", Gender.Male, 500, 1500),
            ("TestSeniorMod", "Старший Модератор", "На страже правил", "Санкт-Петербург", "Помогаю поддерживать дружелюбную атмосферу на сайте. Обращайтесь с вопросами!", Gender.Female, 350, 800),
            ("TestModerator", "Модератор Форума", "Читаю все", "Новосибирск", "Модерирую форум и помогаю новичкам освоиться.", Gender.Male, 200, 400),
            ("TestMentor", "Опытный Наставник", "Учу мастерству", "Екатеринбург", "Ментор для начинающих мастеров. Провожу игры уже 10 лет.", Gender.Male, 450, 1200),
            ("TestUser", "Активный Игрок", "Ищу новые приключения", "Казань", "Люблю фэнтези и sci-fi. Играю за воинов и магов.", Gender.Male, 150, 300),
            ("TestHonorary", "Почетный Гоблин", "Ветеран сообщества", "Нижний Новгород", "Один из первых участников сайта. Почетный гоблин с 2015 года.", Gender.Male, 600, 2000),
            ("Ян", "Ян", "Минималист", "Владивосток", "Краткость — сестра таланта.", Gender.Male, 50, 80),
            ("Player_One", "Игрок Первый", "Ready Player One", "Краснодар", "Геймер и ролевик. Люблю D&D 5e и Pathfinder.", Gender.Male, 120, 250),
            ("Player-Two", "Игрок Второй", "Второй не значит худший", "Самара", "Мастер интриг и политических игр.", Gender.Female, 180, 350),
            ("Player.Three", "Игрок Третий", "Точка — это стиль", "Ростов-на-Дону", "Специализируюсь на horror-играх и мистике.", Gender.Unknown, 90, 150),
            ("Player Four", "Игрок Четвертый", "Пробелы разрешены", "Воронеж", "Новичок, но учусь быстро!", Gender.Male, 30, 45),
            ("Игрок", "Русский Игрок", "Только кириллица", "Омск", "Предпочитаю русскоязычные игры.", Gender.Male, 100, 200),
            ("Игрок_Один", "Первый Русский", "Кириллица и символы", "Челябинск", "Люблю славянское фэнтези.", Gender.Female, 75, 120),
            ("Тест Елки", "Тестовая Елка", "С буквой Е", "Уфа", "Проверяю поддержку буквы Е в системе.", Gender.Unknown, 25, 40),
            ("LongestLoginPossible", "Длинное Имя", "Максимальная длина", "Пермь", "У меня самый длинный логин на сайте!", Gender.Male, 60, 100),
            ("OnlyReader", "Только Читатель", "Читаю, не играю", "Тула", "Люблю читать игры, но сам не участвую.", Gender.Male, 15, 50),
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
                user.QualityRating = profile.QualityRating;
                user.QuantityRating = profile.QuantityRating;
                user.LastActivityUtc = now.AddMinutes(-Random.Shared.Next(1, 1440));
            }
        }

        result.Details.Add("User profiles updated");
        return Task.CompletedTask;
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
}
