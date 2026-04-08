using DM.Domain.Account.Configuration;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Parsing;
using Microsoft.Extensions.Options;

namespace DM.Domain.Account.Features.Authentication;

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
    public CreateSession Create(bool persistent, bool invisible, SessionContext? context = null)
    {
        var rightNow = _dateTimeProvider.Now.UtcDateTime;
        return new CreateSession
        {
            Id = _guidFactory.Create(),
            Persistent = persistent,
            Invisible = invisible,
            ExpirationUtc = persistent
                ? rightNow.AddDays(_config.PersistentSessionExpirationDays)
                : rightNow.AddHours(_config.SessionExpirationHours),
            CreatedUtc = rightNow,
            IpAddress = context?.IpAddress,
            UserAgent = context?.UserAgent,
            DeviceInfo = UserAgentParser.Parse(context?.UserAgent)
        };
    }
}
