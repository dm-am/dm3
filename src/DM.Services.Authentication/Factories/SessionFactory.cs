using DM.Services.Authentication.Configuration;
using DM.Services.Core.Implementation;
using DM.Services.DataAccess.BusinessObjects.Users;
using Microsoft.Extensions.Options;

namespace DM.Services.Authentication.Factories;

/// <inheritdoc />
internal class SessionFactory : ISessionFactory
{
    private readonly IGuidFactory _guidFactory;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly AuthenticationConfiguration _config;

    /// <inheritdoc />
    public SessionFactory(
        IGuidFactory guidFactory,
        IDateTimeProvider dateTimeProvider,
        IOptions<AuthenticationConfiguration> authConfig)
    {
        _guidFactory = guidFactory;
        _dateTimeProvider = dateTimeProvider;
        _config = authConfig.Value;
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
                ? rightNow.AddDays(_config.PersistentSessionExpirationDays)
                : rightNow.AddHours(_config.SessionExpirationHours)
        };
    }
}