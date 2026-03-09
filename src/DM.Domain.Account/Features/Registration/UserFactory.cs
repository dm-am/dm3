using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;

namespace DM.Domain.Account.Features.Registration;

/// <summary>
/// Creates new user DTOs for email-first registration flow
/// </summary>
internal class UserFactory : IUserFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    public UserFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public CreateUser CreateFromPending(PendingRegistration pending, string username)
    {
        return new CreateUser
        {
            UserId = _guidFactory.Create(),
            Username = username.Trim(),
            Email = pending.Email, // Already lowercase
            CreatedUtc = _dateTimeProvider.Now,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified,
            Salt = pending.Salt,
            PasswordHash = pending.PasswordHash,
            PasswordHashVersion = pending.PasswordHashVersion
        };
    }
}
