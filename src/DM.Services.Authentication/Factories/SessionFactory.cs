using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Authentication.Factories;

/// <inheritdoc />
internal class SessionFactory : ISessionFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;

    /// <inheritdoc />
    public SessionFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
    }

    /// <inheritdoc />
    public Session Create(bool persistent, bool invisible)
    {
        var rightNow = _dateTimeProvider.Now.UtcDateTime;
        return new Session
        {
            Id = _guidFactory.Create(),
            Persistent = persistent,
            Invisible = invisible,
            ExpirationDate = persistent
                ? rightNow.AddMonths(1)
                : rightNow.AddDays(1)
        };
    }
}