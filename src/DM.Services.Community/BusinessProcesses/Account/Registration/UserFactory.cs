using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Account.Registration;

/// <summary>
/// Creates new user DAL models for email-first registration flow
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
    public User CreateFromPending(PendingRegistration pending, string login)
    {
        return new User
        {
            UserId = _guidFactory.Create(),
            Login = login.Trim(),
            Email = pending.Email, // Already lowercase
            CreatedUtc = _dateTimeProvider.Now,
            LastActivityUtc = _dateTimeProvider.Now, // Set on activation so user appears in active list
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified,
            Salt = pending.Salt,
            PasswordHash = pending.PasswordHash,
            PasswordHashVersion = pending.PasswordHashVersion,
            RatingDisabled = false,
            QualityRating = 0,
            QuantityRating = 0,
            IsRemoved = false,
            // Initialize string fields
            Status = string.Empty,
            Name = string.Empty,
            Location = string.Empty,
            Icq = string.Empty,
            Skype = string.Empty,
            Info = string.Empty
        };
    }
}
