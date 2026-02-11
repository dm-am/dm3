using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Authentication.Implementation.Security;
using DM.Services.Authentication.Implementation.UserIdentity;
using DM.Services.Common.Authorization;
using DM.Services.Community.BusinessProcesses.Users;
using DM.Services.Community.BusinessProcesses.Users.Reading;
using DM.Services.Community.BusinessProcesses.Users.Updating;
using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Users;
using DbUser = DM.Services.DataAccess.BusinessObjects.Users.User;
using Microsoft.EntityFrameworkCore;
using UserDetails = DM.Web.API.Dto.Users.UserDetails;

namespace DM.Web.API.Services.Moderation;

/// <inheritdoc />
internal class ModerationApiService : IModerationApiService
{
    private readonly DmDbContext _dbContext;
    private readonly IIdentityProvider _identityProvider;
    private readonly IUserReadingService _userReadingService;
    private readonly IUserUpdatingService _userUpdatingService;
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
        IUserReadingService userReadingService,
        IUserUpdatingService userUpdatingService,
        IIntentionManager intentionManager,
        ISecurityManager securityManager,
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _identityProvider = identityProvider;
        _userReadingService = userReadingService;
        _userUpdatingService = userUpdatingService;
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
            .ThenBy(u => u.Login)
            .Select(u => new TestAccountInfo
            {
                Login = u.Login,
                Password = null,
                Role = u.Role
            })
            .ToListAsync();

        return users;
    }

    /// <inheritdoc />
    public async Task<Envelope<UserDetails>> ModerateUserProfile(string login, ModerateProfile profile)
    {
        var user = await _userReadingService.Get(login);
        _intentionManager.ThrowIfForbidden(UserIntention.Moderate, user);

        var updateUser = new UpdateUser
        {
            Login = login,
            Info = profile.Info ?? string.Empty
        };

        var updatedUser = await _userUpdatingService.Update(updateUser);
        return new Envelope<UserDetails>(_mapper.Map<UserDetails>(updatedUser));
    }

    /// <inheritdoc />
    public async Task<SeedResult> SeedTestUsers()
    {
        const string defaultPassword = "Test123!";

        // Test accounts to create based on the testing plan
        // All users start with 0 posts (QuantityRating=0), so all are "newbie" status
        // Note: All records in Users table are fully activated. For pending activation testing, use PendingRegistration.
        var testAccounts = new[]
        {
            // Roles
            new { Login = "TestAdmin", Email = "admin@test.local", Role = UserRole.Admin, IsHonorary = false },
            new { Login = "TestSeniorMod", Email = "seniormod@test.local", Role = UserRole.SeniorModerator, IsHonorary = false },
            new { Login = "TestModerator", Email = "mod@test.local", Role = UserRole.Moderator, IsHonorary = false },
            new { Login = "TestMentor", Email = "mentor@test.local", Role = UserRole.Mentor, IsHonorary = false },
            new { Login = "TestUser", Email = "user@test.local", Role = UserRole.RegularUser, IsHonorary = false },

            // Edge cases for login length
            new { Login = "Ab", Email = "ab@test.local", Role = UserRole.RegularUser, IsHonorary = false },
            new { Login = "LongestLoginPossible", Email = "longest@test.local", Role = UserRole.RegularUser, IsHonorary = false },
            new { Login = "Player_One", Email = "player1@test.local", Role = UserRole.RegularUser, IsHonorary = false },
            new { Login = "Player-Two", Email = "player2@test.local", Role = UserRole.RegularUser, IsHonorary = false },

            // Special states
            new { Login = "TestHonorary", Email = "honorary@test.local", Role = UserRole.RegularUser, IsHonorary = true },
        };

        var result = new SeedResult();

        // Get existing logins to skip
        var existingLogins = await _dbContext.Users
            .Where(u => testAccounts.Select(a => a.Login.ToLower()).Contains(u.Login.ToLower()))
            .Select(u => u.Login.ToLower())
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
            var (hash, salt, version) = _securityManager.GeneratePassword(defaultPassword);

            var user = new DbUser
            {
                UserId = _guidFactory.Create(),
                Login = account.Login,
                Email = account.Email.ToLowerInvariant(),
                CreatedUtc = now.AddMonths(-1), // Created a month ago
                LastActivityUtc = now,
                Role = account.Role,
                AccessPolicy = AccessPolicy.NotSpecified,
                Salt = salt,
                PasswordHash = hash,
                PasswordHashVersion = version,
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
            var (hash, salt, version) = _securityManager.GeneratePassword(defaultPassword);
            var pending = new DM.Services.DataAccess.BusinessObjects.Users.PendingRegistration
            {
                PendingRegistrationId = _guidFactory.Create(),
                TokenId = _guidFactory.Create(),
                Email = pendingEmail,
                PasswordHash = hash,
                Salt = salt,
                PasswordHashVersion = version,
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
