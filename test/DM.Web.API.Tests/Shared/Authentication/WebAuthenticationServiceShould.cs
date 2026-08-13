using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Events;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Web.API.Shared.Authentication;
using DM.Web.API.Shared.Authentication.Credentials;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace DM.Web.API.Tests.Shared.Authentication;

/// <summary>
/// The warning about a login from an unknown address leaves before the request does.
/// </summary>
/// <remarks>
/// Sending it publishes through MailSender, which is scoped to the request and
/// returns its rented AMQP channel in Dispose. Handed to a task nobody waits for,
/// the publish raced the end of the request with nothing ordering the two — and
/// the losing side went through a disposed sender, which either throws where
/// nobody listens or leaks the channel. Neither outcome is visible from outside:
/// the login answers normally and the letter that never left looks exactly like a
/// letter nobody needed.
///
/// So the assertion is about state and not about time. The sender is held on an
/// unfinished task and the login is required to be unfinished with it — no sleep,
/// no polling, no timeout, and nothing that can go green on a lucky schedule.
/// </remarks>
public class WebAuthenticationServiceShould : UnitTestBase
{
    private static readonly Guid UserId = Guid.Parse("6ff6a1d8-1c0c-4b56-9c0b-2cf1a3d4e5f6");
    private const string Email = "reader@dm.am";

    private readonly Mock<IAuthenticationService> authentication;
    private readonly Mock<ICredentialsStorage> credentials;
    private readonly Mock<IIdentitySetter> identitySetter;
    private readonly Mock<ISuspiciousLoginDetector> detector;
    private readonly Mock<ISuspiciousLoginNotificationSender> sender;
    private readonly Mock<ISecurityAuditRepository> audit;
    private readonly Mock<IEventProducer> events;
    private readonly Mock<ILogger<WebAuthenticationService>> logger;
    private readonly WebAuthenticationService service;

    public WebAuthenticationServiceShould()
    {
        authentication = Mock<IAuthenticationService>();
        credentials = Mock<ICredentialsStorage>();
        identitySetter = Mock<IIdentitySetter>();
        detector = Mock<ISuspiciousLoginDetector>();
        sender = Mock<ISuspiciousLoginNotificationSender>();
        audit = Mock<ISecurityAuditRepository>();
        events = Mock<IEventProducer>();
        logger = Mock<ILogger<WebAuthenticationService>>();

        authentication
            .Setup(a => a.Authenticate(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>(),
                It.IsAny<SessionContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Authenticated());
        credentials.Setup(c => c.Load(It.IsAny<HttpContext>(), It.IsAny<IIdentity>())).Returns(Task.CompletedTask);
        detector
            .Setup(d => d.IsSuspiciousAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(true);
        audit
            .Setup(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<SecurityEventType>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        service = new WebAuthenticationService(authentication.Object, credentials.Object, identitySetter.Object,
            detector.Object, sender.Object, audit.Object, events.Object, logger.Object);
    }

    [Fact]
    public async Task HoldTheLoginUntilTheLetterIsHandedToTheBroker()
    {
        var handedOver = new TaskCompletionSource();
        sender
            .Setup(s => s.SendAsync(Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(handedOver.Task);

        var login = service.Authenticate(Credentials(), Context());

        login.IsCompleted.Should().BeFalse(
            "the publish goes through a sender this request owns and disposes, so the request " +
            "is the only thing that can keep it alive");

        handedOver.SetResult();
        await login;

        sender.Verify(s => s.SendAsync(Email, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Once);
    }

    [Fact]
    public async Task KeepTheLoginWhenTheBrokerRefusesTheLetter()
    {
        sender
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new BrokerUnreachableException(new Exception("connection refused")));

        var identity = await service.Awaiting(s => s.Authenticate(Credentials(), Context()))
            .Should().NotThrowAsync("an informational letter does not cost anybody their session");

        identity.Subject.User.IsAuthenticated.Should().BeTrue();

        // The journal is the record the owner of the account reads afterwards, and
        // it does not depend on anything leaving the machine.
        audit.Verify(a => a.LogAsync(UserId, SecurityEventType.SuspiciousLogin, It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CountTheLetterItSwallowed()
    {
        sender
            .Setup(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new BrokerUnreachableException(new Exception("connection refused")));

        var events = new List<string?>();
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (instrument.Name == "dm.messaging.publish_failed")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "event")
                {
                    events.Add(tag.Value?.ToString());
                }
            }
        });
        listener.Start();

        await service.Authenticate(Credentials(), Context());
        listener.RecordObservableInstruments();

        // Swallowing is only allowed while it is counted: an alert reads this
        // counter, and without it a broker refusing every publish looks exactly
        // like a site where nothing suspicious ever happens.
        events.Should().Contain(nameof(SecurityEventType.SuspiciousLogin));
        logger.Verify(
            l => l.Log(LogLevel.Warning, It.IsAny<EventId>(), It.IsAny<It.IsAnyType>(), It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    /// <summary>
    /// The site says it too, not only the letter.
    /// </summary>
    /// <remarks>
    /// The event type, its wording and its category all existed and nothing ever
    /// produced it, so the Security category the settings screen offers to
    /// subscribe to was one item short of what it promised.
    /// </remarks>
    [Fact]
    public async Task AnnounceASuspiciousLoginOnTheSiteAsWell()
    {
        await service.Authenticate(Credentials(), Context());

        events.Verify(e => e.SendAsync(EventType.SuspiciousLoginActivity, UserId), Times.Once);
    }

    [Fact]
    public async Task SendNothingWhenTheLoginIsOrdinary()
    {
        detector
            .Setup(d => d.IsSuspiciousAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(false);

        await service.Authenticate(Credentials(), Context());

        sender.Verify(s => s.SendAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>()), Times.Never);
        audit.Verify(a => a.LogAsync(It.IsAny<Guid>(), It.IsAny<SecurityEventType>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        events.Verify(e => e.SendAsync(It.IsAny<EventType>(), It.IsAny<Guid>()), Times.Never);
    }

    private static IIdentity Authenticated() => Identity.Success(
        new AuthenticatedUser { UserId = UserId, Username = "reader", Email = Email, Role = UserRole.RegularUser },
        new Session { Id = Guid.NewGuid() },
        UserSettings.Default,
        "token");

    private static AuthCredentials Credentials() => new LoginCredentials
    {
        Email = Email,
        Password = "password",
        RememberMe = false
    };

    private static HttpContext Context() => new DefaultHttpContext
    {
        Connection = { RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.7") }
    };
}
