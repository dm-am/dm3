using DM.Services.Core.Dto.Enums;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Account.Registration;

/// <inheritdoc />
internal class UserFactory : IUserFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public UserFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public User Create(UserRegistration registration, string salt, string hash, int hashVersion)
    {
        return new User
        {
            UserId = _guidFactory.Create(),
            Login = registration.Login.Trim(),
            Email = registration.Email.Trim(),
            CreatedUtc = _dateTimeProvider.Now,
            LastActivityUtc = null,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified,
            Salt = salt,
            PasswordHash = hash,
            PasswordHashVersion = hashVersion,
            RatingDisabled = false,
            QualityRating = 0,
            QuantityRating = 0,
            Activated = false,
            CanMerge = false,
            MergeRequested = null,
            IsRemoved = false,
            // Initialize NOT NULL string fields with defaults
            Status = string.Empty,
            Name = string.Empty,
            Location = string.Empty,
            Icq = string.Empty,
            Skype = string.Empty,
            Info = string.Empty,
            ProfilePictureUrl = string.Empty,
            SmallProfilePictureUrl = string.Empty,
            MediumProfilePictureUrl = string.Empty,
#pragma warning disable CS0618 // TimezoneId is obsolete
            TimezoneId = "UTC"
#pragma warning restore CS0618
        };
    }
}
