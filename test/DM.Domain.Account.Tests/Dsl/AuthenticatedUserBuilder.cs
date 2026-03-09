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

    public AuthenticatedUser Please() => user;
}
