using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;

namespace DM.Domain.Community.Tests.Dsl;

public class AuthenticatedUserBuilder(Guid userId)
{
    private readonly AuthenticatedUser _user = new() { UserId = userId };

    public AuthenticatedUserBuilder WithRole(UserRole role)
    {
        _user.Role = role;
        return this;
    }

    public AuthenticatedUser Please() => _user;
}
