using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using AwesomeAssertions;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Testing;
using DM.Testing.Dsl;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Security;

public class SecurityJournalServiceShould : UnitTestBase
{
    private readonly Guid userId = Guid.NewGuid();
    private readonly IIdentityProvider identityProvider;
    private readonly ISecurityAuditRepository repository;
    private readonly SecurityJournalService service;

    public SecurityJournalServiceShould()
    {
        identityProvider = Mock<IIdentityProvider>();
        repository = Mock<ISecurityAuditRepository>();
        identityProvider.Current.Returns(Identities.User(userId));

        service = new SecurityJournalService(identityProvider, repository);
    }

    [Theory]
    [InlineData(SecurityLogType.Login, new[]
    {
        SecurityEventType.LoginSuccess,
        SecurityEventType.LoginFailure,
        SecurityEventType.SuspiciousLogin
    })]
    [InlineData(SecurityLogType.Password, new[]
    {
        SecurityEventType.PasswordChange,
        SecurityEventType.PasswordResetRequest,
        SecurityEventType.PasswordResetComplete
    })]
    [InlineData(SecurityLogType.Session, new[]
    {
        SecurityEventType.Logout,
        SecurityEventType.SessionTerminated,
        SecurityEventType.LogoutElsewhere
    })]
    [InlineData(SecurityLogType.TwoFactor, new[]
    {
        SecurityEventType.TwoFactorEnabled,
        SecurityEventType.TwoFactorDisabled,
        SecurityEventType.TwoFactorRecoveryCodeUsed,
        SecurityEventType.TwoFactorRecoveryCodesReissued,
        SecurityEventType.TwoFactorRemovalScheduled,
        SecurityEventType.TwoFactorRemovalCancelled,
        SecurityEventType.TwoFactorRemovedByAdmin,
        SecurityEventType.TwoFactorSetupFailure
    })]
    public async Task ReadAFilterAsItsOwnCategory(SecurityLogType filter, SecurityEventType[] expectedTypes)
    {
        await service.GetOwnAsync(filter, 30);

        await repository.Received(1).GetByTypesAsync(
            userId,
            Arg.Is<IReadOnlyList<SecurityEventType>>(types => types.SequenceEqual(expectedTypes)),
            30);
        await repository.DidNotReceiveWithAnyArgs().GetRecentEventsAsync(default);
    }

    [Fact]
    public async Task ReadTheWholeJournalWhenNoFilterIsAsked()
    {
        await service.GetOwnAsync(null, 30);

        await repository.Received(1).GetRecentEventsAsync(userId, 30);
        await repository.DidNotReceiveWithAnyArgs().GetByTypesAsync(default, default!);
    }

    [Fact]
    public async Task RefuseAGuest()
    {
        identityProvider.Current.Returns(Identities.Guest());

        var exception = await Assert.ThrowsAsync<HttpException>(() => service.GetOwnAsync(SecurityLogType.Login));

        exception.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await repository.DidNotReceiveWithAnyArgs().GetByTypesAsync(default, default!);
        await repository.DidNotReceiveWithAnyArgs().GetRecentEventsAsync(default);
    }
}
