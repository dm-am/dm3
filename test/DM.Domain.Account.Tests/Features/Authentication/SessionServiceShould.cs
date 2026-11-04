using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Authentication;

public class SessionServiceShould : UnitTestBase
{
    private readonly IAuthenticationRepository repository;
    private readonly IIdentityProvider identityProvider;
    private readonly ISecurityAuditRepository auditService;
    private readonly SessionService service;

    public SessionServiceShould()
    {
        repository = Mock<IAuthenticationRepository>();
        identityProvider = Mock<IIdentityProvider>();
        auditService = Mock<ISecurityAuditRepository>();
        var dateTimeProvider = Mock<IDateTimeProvider>();
        dateTimeProvider.Now.Returns(DateTimeOffset.UtcNow);

        service = new SessionService(
            repository,
            identityProvider,
            auditService,
            dateTimeProvider);
    }

    [Fact]
    public async Task LogoutAndRemoveSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SignIn(userId, sessionId);

        var result = await service.Logout();

        result.User.IsAuthenticated.Should().BeFalse();
        await repository.Received(1).RemoveSession(userId, sessionId);
        await repository.Received(1).UpdateActivity(userId, Arg.Any<DateTimeOffset>());
        await auditService.Received(1).LogAsync(userId, SecurityEventType.Logout, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task LogoutElsewhereButKeepCurrentSession()
    {
        var userId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var identity = SignIn(userId, sessionId);

        var result = await service.LogoutElsewhere();

        result.Should().Be(identity);
        await repository.Received(1).RemoveSessionsExcept(userId, sessionId);
        await auditService.Received(1).LogAsync(userId, SecurityEventType.LogoutElsewhere, Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task TerminateSessionForCurrentUser()
    {
        var userId = Guid.NewGuid();
        var currentSessionId = Guid.NewGuid();
        var targetSessionId = Guid.NewGuid();
        SignIn(userId, currentSessionId);

        await service.TerminateSession(userId, targetSessionId);

        await repository.Received(1).RemoveSession(userId, targetSessionId);
        await auditService.Received(1).LogAsync(userId, SecurityEventType.SessionTerminated,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task ThrowWhenTerminatingOtherUserSession()
    {
        var currentUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        SignIn(currentUserId, Guid.NewGuid());

        // Not the caller's session to end, and a refusal is a refusal: the error
        // middleware maps HttpException and its kin and nothing else, so anything
        // outside that family reaches the client as a server fault.
        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => service.TerminateSession(otherUserId, sessionId));
        thrown.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private IIdentity SignIn(Guid userId, Guid sessionId)
    {
        var identity = Identity.Success(
            new AuthenticatedUser { UserId = userId, Role = UserRole.RegularUser },
            new Session { Id = sessionId },
            UserSettings.Default,
            "token");
        identityProvider.Current.Returns(identity);
        return identity;
    }
}
