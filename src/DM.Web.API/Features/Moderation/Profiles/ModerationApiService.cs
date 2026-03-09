using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Dto;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using Microsoft.EntityFrameworkCore;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <inheritdoc />
internal class ModerationApiService : IModerationApiService
{
    private readonly DmDbContext _dbContext;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserService _userService;
    private readonly IIntentionManager _intentionManager;
    private readonly ISecurityManager _securityManager;
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly IMapper _mapper;

    /// <summary>
    /// Creates a new instance of <see cref="ModerationApiService"/>
    /// </summary>
    public ModerationApiService(
        DmDbContext dbContext,
        IIdentityProvider identityProvider,
        IUserService userService,
        IIntentionManager intentionManager,
        ISecurityManager securityManager,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _identityProvider = identityProvider;
        _userService = userService;
        _intentionManager = intentionManager;
        _securityManager = securityManager;
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task SetRole(UserRole role)
    {
        var userId = _identityProvider.Current.User.UserId;
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.UserId == userId);
        if (user != null)
        {
            user.Role = role;
            await _dbContext.SaveChangesAsync();
        }
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
                Login = u.Username,
                Password = null,
                Role = u.Role
            })
            .ToListAsync();

        return users;
    }

    /// <inheritdoc />
    public async Task<Envelope<UserProfile>> ModerateUserProfile(string login, ModerateProfile profile)
    {
        var user = await _userService.Get(login);
        _intentionManager.ThrowIfForbidden(UserIntention.Moderate, user);

        var updateUser = new UpdateUser
        {
            Username = login,
            Info = profile.Info ?? string.Empty
        };

        var updatedUser = await _userService.Update(updateUser);
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
        // - Allowed: a-z A-Z а-я А-Я ёЁ 0-9 _ - . space
        var testAccounts = new[]
        {
            // === All roles (one per role) ===
            new { Login = "TestAdmin", Email = "admin@test.local", Role = UserRole.Admin, IsHonorary = false },
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
            new { Login = "Тест Ёлки", Email = "yolka@test.local", Role = UserRole.RegularUser, IsHonorary = false }, // cyrillic + space + Ё

            // === Special states ===
            new { Login = "TestHonorary", Email = "honorary@test.local", Role = UserRole.RegularUser, IsHonorary = true }, // honorary goblin
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
                result.SkippedLogins.Add(account.Login);
                continue;
            }

            // Generate password hash
            var (hash, salt) = _securityManager.GeneratePassword(defaultPassword);

            var user = new DbUser
            {
                UserId = _guidFactory.Create(),
                Username = account.Login,
                Email = account.Email.ToLowerInvariant(),
                CreatedUtc = now.AddMonths(-1), // Created a month ago
                LastActivityUtc = now,
                Role = account.Role,
                AccessPolicy = AccessPolicy.NotSpecified,
                Salt = salt,
                PasswordHash = hash,
                PasswordHashVersion = 4, // Argon2id
                IsRemoved = false,
                IsHonorary = account.IsHonorary,
                RatingDisabled = false,
                QualityRating = 0,
                QuantityRating = 0, // Real rating - no posts yet
                Status = string.Empty,
                Name = string.Empty,
                Location = string.Empty,
            };

            _dbContext.Users.Add(user);
            result.Created++;
            result.CreatedLogins.Add(account.Login);
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
            result.CreatedLogins.Add("(pending) inactive@test.local");
        }
        else
        {
            result.Skipped++;
            result.SkippedLogins.Add("(pending) inactive@test.local");
        }

        if (result.Created > 0)
        {
            await _dbContext.SaveChangesAsync();
        }

        return result;
    }
}
