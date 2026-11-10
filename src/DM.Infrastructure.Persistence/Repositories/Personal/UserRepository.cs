using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Users;
using DM.Domain.Personal.Features.Profiles;
using DM.Infrastructure.Persistence.Shared.Users;
using Microsoft.EntityFrameworkCore;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;

namespace DM.Infrastructure.Persistence.Repositories.Personal;

/// <inheritdoc />
internal partial class UserRepository : IUserRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserRepository(
        DmDbContext dbContext,
        IDateTimeProvider dateTimeProvider)
    {
        _dbContext = dbContext;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public async Task<GeneralUser?> GetUserAsync(string username)
    {
        var user = await _dbContext.Users
            .Where(AccountNames.UsernameIs(username))
            .ProjectToGeneralUser()
            .FirstOrDefaultAsync();

        if (user != null)
        {
            await PopulateProfileCounters(new[] { user });
        }

        return user;
    }

    /// <inheritdoc />
    public Task<Guid?> FindUserIdAsync(string username) =>
        _dbContext.Users
            .TagWith("DM.User.FindId")
            .Where(AccountNames.UsernameIs(username))
            .Select(u => (Guid?)u.UserId)
            .FirstOrDefaultAsync();

    /// <inheritdoc />
    public async Task<GeneralUser?> GetUserAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .Where(u => u.UserId == userId)
            .ProjectToGeneralUser()
            .FirstOrDefaultAsync();

        if (user != null)
        {
            await PopulateProfileCounters(new[] { user });
        }

        return user;
    }

    /// <inheritdoc />
    public async Task<UserDetails?> GetUserDetailsAsync(string username)
    {
        var userDetails = await _dbContext.Users
            .Where(AccountNames.UsernameIs(username))
            .ProjectToUserDetails()
            .FirstOrDefaultAsync();

        if (userDetails == null)
        {
            return null;
        }

        await PopulateProfileCounters(new[] { userDetails });

        userDetails.Settings = await ReadSettings(userDetails.UserId);
        return userDetails;
    }

    /// <inheritdoc />
    public async Task<UserDetails?> GetUserDetailsAsync(Guid userId)
    {
        var userDetails = await _dbContext.Users
            .Where(u => u.UserId == userId)
            .ProjectToUserDetails()
            .FirstOrDefaultAsync();

        if (userDetails == null)
        {
            return null;
        }

        await PopulateProfileCounters(new[] { userDetails });

        userDetails.Settings = await ReadSettings(userDetails.UserId);
        return userDetails;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsersByRoleAsync(UserRole role)
    {
        var users = await _dbContext.Users
            .Where(u => u.Role == role)
            .OrderBy(u => u.Username)
            .ProjectToGeneralUser()
            .ToArrayAsync();

        await PopulateProfileCounters(users);
        return users;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<GeneralUser>> GetUsersAsync(IEnumerable<Guid> userIds)
    {
        var idsList = userIds.ToList();
        if (!idsList.Any())
        {
            return Array.Empty<GeneralUser>();
        }

        var users = await _dbContext.Users
            .Where(u => idsList.Contains(u.UserId))
            .ProjectToGeneralUser()
            .ToArrayAsync();

        await PopulateProfileCounters(users);
        return users;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<UserReference>> GetUserReferencesAsync(IEnumerable<Guid> userIds)
    {
        var idsList = userIds.ToList();
        if (idsList.Count == 0)
        {
            return Array.Empty<UserReference>();
        }

        // Written out rather than through ProjectToGeneralUser, to select five columns only.
        return await _dbContext.Users
            .TagWith("DM.User.References")
            .Where(u => idsList.Contains(u.UserId))
            .Select(u => new UserReference
            {
                UserId = u.UserId,
                Username = u.Username,
                LastActivityUtc = u.LastActivityUtc,
                Role = u.Role,
                IsNewbie = u.IsNewbie,
            })
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task ReplaceUserContacts(Guid userId, IEnumerable<UserContactEntity> contacts)
    {
        var existingContacts = await _dbContext.UserContacts
            .Where(c => c.UserId == userId)
            .ToListAsync();
        _dbContext.UserContacts.RemoveRange(existingContacts);

        var newContacts = contacts.Select(c => new DbUserContact
        {
            UserContactId = c.UserContactId,
            UserId = c.UserId,
            ContactType = c.ContactType,
            ContactValue = c.ContactValue,
            SortOrder = c.SortOrder
        });
        _dbContext.UserContacts.AddRange(newContacts);

        await _dbContext.SaveChangesAsync();
    }
}
