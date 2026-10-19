using System;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Events;
using DM.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace DM.Domain.Account.Tests.Dsl;

/// <summary>
/// The authentication service over substitutes of everything it asks, with a
/// clock that answers now and the session lifetimes the token tests count on.
/// </summary>
/// <remarks>
/// A test class moves what its cases need from here, by the fields: an account
/// the repository answers with, a password the manager accepts, a session to
/// refresh. The service has no identity provider: it builds identities and
/// reads none, so there is nothing of the kind to substitute.
/// </remarks>
public abstract class AuthenticationServiceTestBase : UnitTestBase
{
    protected readonly ISecurityManager securityManager;
    protected readonly ISymmetricCryptoService cryptoService;
    protected readonly IAuthenticationRepository repository;
    private protected readonly ISessionFactory sessionFactory;
    protected readonly IDateTimeProvider dateTimeProvider;
    protected readonly ILoginAttemptTracker loginAttemptTracker;
    protected readonly ISecurityAuditRepository auditService;
    protected readonly IEventProducer eventProducer;
    protected readonly ITwoFactorRepository twoFactorRepository;
    protected readonly ITwoFactorVerifier twoFactorVerifier;
    protected readonly IGuidFactory guidFactory;
    private protected readonly AuthenticationService service;

    protected AuthenticationServiceTestBase()
    {
        securityManager = Mock<ISecurityManager>();
        cryptoService = Mock<ISymmetricCryptoService>();
        repository = Mock<IAuthenticationRepository>();
        sessionFactory = Mock<ISessionFactory>();
        dateTimeProvider = Mock<IDateTimeProvider>();
        loginAttemptTracker = Mock<ILoginAttemptTracker>();
        auditService = Mock<ISecurityAuditRepository>();
        eventProducer = Mock<IEventProducer>();
        twoFactorRepository = Mock<ITwoFactorRepository>();
        twoFactorVerifier = Mock<ITwoFactorVerifier>();
        guidFactory = Mock<IGuidFactory>();
        var logger = Mock<ILogger<AuthenticationService>>();
        var config = Options.Create(new AuthenticationConfiguration
        {
            SessionRefreshMinutes = 60,
            ActivityTrackingMinutes = 5,
            SessionExpirationHours = 24,
            PersistentSessionExpirationDays = 365
        });

        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new AuthenticationService(
            securityManager,
            cryptoService,
            repository,
            sessionFactory,
            dateTimeProvider,
            loginAttemptTracker,
            auditService,
            eventProducer,
            twoFactorRepository,
            twoFactorVerifier,
            guidFactory,
            logger,
            config,
            Options.Create(new TwoFactorConfiguration()));
    }
}
