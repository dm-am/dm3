using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;

namespace DM.Domain.Account.Tests.Dsl;

public class AuthenticatedUserBuilder(Guid userId)
{
    private readonly AuthenticatedUser user = new() { UserId = userId };

    public AuthenticatedUserBuilder WithRole(UserRole role)
    {
        user.Role = role;
        return this;
    }

    public AuthenticatedUserBuilder WithAccessPolicy(AccessPolicy policy)
    {
        user.AccessPolicy = policy;
        return this;
    }

    public AuthenticatedUserBuilder WithCredentials(string salt, string passwordHash)
    {
        user.Salt = salt;
        user.PasswordHash = passwordHash;
        return this;
    }

    public AuthenticatedUser Please() => user;
}
