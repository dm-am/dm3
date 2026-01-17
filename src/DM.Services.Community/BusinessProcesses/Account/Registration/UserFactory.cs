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
    public User Create(UserRegistration registration, string salt, string hash)
    {
        return new User
        {
            UserId = _guidFactory.Create(),
            Login = registration.Login.Trim(),
            Email = registration.Email.Trim(),
            RegistrationDate = _dateTimeProvider.Now,
            LastVisitDate = null,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified,
            Salt = salt,
            PasswordHash = hash,
            RatingDisabled = false,
            QualityRating = 0,
            QuantityRating = 0,
            Activated = false,
            CanMerge = false,
            MergeRequested = null,
            IsRemoved = false
        };
    }
}