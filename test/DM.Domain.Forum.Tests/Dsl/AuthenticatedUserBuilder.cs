using System;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Enums;

namespace DM.Domain.Forum.Tests.Dsl;

public class AuthenticatedUserBuilder(Guid userId)
{
    private readonly AuthenticatedUser user = new() {UserId = userId};

    public AuthenticatedUserBuilder WithRole(UserRole role)
    {
        user.Role = role;
        return this;
    }

    public AuthenticatedUser Please() => user;
}