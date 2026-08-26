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
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
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

    private readonly IAuthenticationService authentication;
    private readonly ICredentialsStorage credentials;
    private readonly IIdentitySetter identitySetter;
    private readonly ISuspiciousLoginDetector detector;
    private readonly ISuspiciousLoginNotificationSender sender;
    private readonly ISecurityAuditRepository audit;
    private readonly IEventProducer events;
    private readonly RecordingLogger<WebAuthenticationService> logger;
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
        logger = new RecordingLogger<WebAuthenticationService>();

        authentication
            .Authenticate(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(),
                Arg.Any<SessionContext>(), Arg.Any<CancellationToken>()).Returns(Authenticated());
        credentials.Load(Arg.Any<HttpContext>(), Arg.Any<IIdentity>()).Returns(Task.CompletedTask);
        detector
            .IsSuspiciousAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        audit
            .LogAsync(Arg.Any<Guid>(), Arg.Any<SecurityEventType>(), Arg.Any<string>(),
                Arg.Any<string>(), Arg.Any<string>()).Returns(Task.CompletedTask);

        service = new WebAuthenticationService(authentication, credentials, identitySetter,
            detector, sender, audit, events, logger);
    }

    [Fact]
    public async Task HoldTheLoginUntilTheLetterIsHandedToTheBroker()
    {
        var handedOver = new TaskCompletionSource();
        sender
            .SendAsync(Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>()).Returns(handedOver.Task);

        var login = service.Authenticate(Credentials(), Context());

        login.IsCompleted.Should().BeFalse(
            "the publish goes through a sender this request owns and disposes, so the request " +
            "is the only thing that can keep it alive");

        handedOver.SetResult();
        await login;

        await sender.Received(1).SendAsync(Email, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task KeepTheLoginWhenTheBrokerRefusesTheLetter()
    {
        sender
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
            .ThrowsAsync(new BrokerUnreachableException(new Exception("connection refused")));

        var identity = await service.Awaiting(s => s.Authenticate(Credentials(), Context()))
            .Should().NotThrowAsync("an informational letter does not cost anybody their session");

        identity.Subject.User.IsAuthenticated.Should().BeTrue();

        // The journal is the record the owner of the account reads afterwards, and
        // it does not depend on anything leaving the machine.
        await audit.Received(1).LogAsync(UserId, SecurityEventType.SuspiciousLogin, Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CountTheLetterItSwallowed()
    {
        sender
            .SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>())
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
        logger.At(LogLevel.Warning).Should().ContainSingle();
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

        await events.Received(1).SendAsync(EventType.SuspiciousLoginActivity, UserId);
    }

    [Fact]
    public async Task SendNothingWhenTheLoginIsOrdinary()
    {
        detector
            .IsSuspiciousAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        await service.Authenticate(Credentials(), Context());

        await sender.DidNotReceive().SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>());
        await audit.DidNotReceive().LogAsync(Arg.Any<Guid>(), Arg.Any<SecurityEventType>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<string>());
        await events.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
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
