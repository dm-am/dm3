using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Configuration;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Security;
using DM.Domain.Account.Features.Tokens;
using DM.Domain.Account.Features.TwoFactor;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Tokens;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using Identity = DM.Domain.Account.Features.Authentication.Identity;

namespace DM.Domain.Account.Tests.Features.TwoFactor;

/// <summary>
/// Getting back into an account whose second factor is gone.
/// </summary>
/// <remarks>
/// The letter does not take the factor off: it schedules the removal a week out,
/// ends every session, and any successful sign-in with the factor calls it off.
/// Every rank takes the same path: it is the only way back for a person who has
/// lost both the phone and the recovery codes, and a privileged account
/// simply has its rank withheld until a factor is set up again.
/// </remarks>
public class TwoFactorRemovalServiceShould : UnitTestBase
{
    private static readonly Guid UserId = Guid.Parse("11112222-3333-4444-5555-666677778888");
    private static readonly Guid AdminId = Guid.Parse("99990000-1111-2222-3333-444455556666");
    private static readonly DateTimeOffset Moment = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Secret = Guid.Parse("aaaabbbb-cccc-dddd-eeee-ffff00001111");

    private readonly ITwoFactorRepository repository;
    private readonly IConfirmationTokenRepository tokens;
    private readonly ITwoFactorRemovalMailSender mailSender;
    private readonly ISessionService sessionService;
    private readonly ISecurityAuditRepository auditService;
    private readonly IIdentityProvider identityProvider;
    private readonly TwoFactorRemovalService service;

    public TwoFactorRemovalServiceShould()
    {
        repository = Mock<ITwoFactorRepository>();
        tokens = Mock<IConfirmationTokenRepository>();
        mailSender = Mock<ITwoFactorRemovalMailSender>();
        sessionService = Mock<ISessionService>();
        auditService = Mock<ISecurityAuditRepository>();
        identityProvider = Mock<IIdentityProvider>();
        var tokenFactory = Mock<ITokenFactory>();
        var dateTimeProvider = Mock<IDateTimeProvider>();

        dateTimeProvider.Now.Returns(Moment);
        tokenFactory.Create(Arg.Any<Guid>(), Arg.Any<TokenType>()).Returns(call => new CreateToken
        {
            TokenId = Guid.NewGuid(),
            Secret = Secret,
            UserId = (Guid)call[0],
            Type = (TokenType)call[1],
            CreatedUtc = Moment
        });

        service = new TwoFactorRemovalService(
            repository,
            tokens,
            mailSender,
            tokenFactory,
            sessionService,
            identityProvider,
            auditService,
            dateTimeProvider,
            Mock<ILogger<TwoFactorRemovalService>>(),
            Options.Create(new TwoFactorConfiguration { RemovalDelayDays = 7 }),
            Options.Create(new TokenConfiguration { TwoFactorRemovalTokenLifetimeHours = 24 }));
    }

    private void ArrangeAccount(UserRole role, bool confirmed = true)
    {
        var account = new TwoFactorAccount(
            UserId, "reader", "reader@example.com", role, "Europe/Moscow");
        repository.FindAccountByEmail("reader@example.com", Arg.Any<CancellationToken>()).Returns(account);
        repository.FindAccount(UserId, Arg.Any<CancellationToken>()).Returns(account);
        repository.FindAccountByUsername("reader", Arg.Any<CancellationToken>()).Returns(account);
        repository.IsConfirmed(UserId, Arg.Any<CancellationToken>()).Returns(confirmed);
        // The read above is the hint and the write is the decision, so a
        // substitute that answered "nothing scheduled" by default would refuse
        // every link in this file.
        repository.ScheduleRemoval(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(confirmed);
    }

    /// <summary>
    /// A rank that owes a second factor takes the mailed path like anyone else.
    /// </summary>
    /// <remarks>
    /// The rank is not lost with the factor, it is withheld until a factor is set up again.
    /// </remarks>
    [Theory]
    [InlineData(UserRole.RegularUser)]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public async Task MailTheRequestToARankThatOwesAFactor(UserRole role)
    {
        ArrangeAccount(role);

        await service.Request("reader@example.com");

        await tokens.Received(1).ReplaceToken(
            UserId, Arg.Is<CreateToken>(t => t.Type == TokenType.TwoFactorRemovalRequest),
            Arg.Any<CancellationToken>());
        await mailSender.Received(1).SendRequest("reader@example.com", Secret);
    }

    [Fact]
    public async Task IssueNothingForAnAddressWithNoAccount()
    {
        await service.Request("stranger@example.com");

        await tokens.DidNotReceive().ReplaceToken(
            Arg.Any<Guid>(), Arg.Any<CreateToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueNothingForAnAccountWithoutAFactor()
    {
        ArrangeAccount(UserRole.RegularUser, confirmed: false);

        await service.Request("reader@example.com");

        await mailSender.DidNotReceive().SendRequest(
            Arg.Any<string>(), Arg.Any<Guid>());
    }

    /// <summary>
    /// The link schedules a removal, it does not perform one, and the letter that
    /// says so goes out at the moment of scheduling.
    /// </summary>
    [Fact]
    public async Task ScheduleTheRemovalRatherThanPerformIt()
    {
        ArrangeAccount(UserRole.RegularUser);
        tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalRequest, Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>()).Returns(UserId);

        await service.Schedule(Secret);

        await repository.Received(1).ScheduleRemoval(
            UserId, Moment.AddDays(7), Arg.Any<CancellationToken>());
        await repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await sessionService.Received(1).LogoutAll(UserId);
        await mailSender.Received(1).SendScheduled(
            "reader@example.com", Moment.AddDays(7), Secret, "Europe/Moscow");
        await auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRemovalScheduled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>
    /// A promotion between the letter and the link changes nothing about it.
    /// </summary>
    [Fact]
    public async Task ScheduleARemovalForAnAccountPromotedSinceTheLinkWasIssued()
    {
        ArrangeAccount(UserRole.Admin);
        tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalRequest, Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>()).Returns(UserId);

        await service.Schedule(Secret);

        await repository.Received(1).ScheduleRemoval(
            UserId, Moment.AddDays(7), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A factor switched off between the read and the write schedules nothing,
    /// and nothing that goes with a scheduling happens either.
    /// </summary>
    /// <remarks>
    /// The link is followed days after it was mailed, so the write decides, not the read.
    /// </remarks>
    [Fact]
    public async Task ScheduleNothingForAFactorSwitchedOffSinceTheLetter()
    {
        ArrangeAccount(UserRole.RegularUser);
        tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalRequest, Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>()).Returns(UserId);
        repository.ScheduleRemoval(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.Schedule(Secret));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
        thrown.Message.Should().Be(RefusalMessage.TwoFactorNotEnabled);
        await sessionService.DidNotReceive().LogoutAll(Arg.Any<Guid>());
        await mailSender.DidNotReceive().SendScheduled(
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid>(), Arg.Any<string?>());
        await auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorRemovalScheduled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task RefuseALinkNobodyIssued()
    {
        tokens.RedeemToken(Arg.Any<Guid>(), Arg.Any<TokenType>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.Schedule(Secret));

        thrown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CallTheRemovalOffFromTheSecondLink()
    {
        tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalCancellation, null,
            Arg.Any<CancellationToken>()).Returns(UserId);
        repository.CancelScheduledRemoval(UserId, Arg.Any<CancellationToken>()).Returns(true);

        await service.Cancel(Secret);

        await auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRemovalCancelled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task RefuseToCallOffARemovalThatIsNotPending()
    {
        tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalCancellation, null,
            Arg.Any<CancellationToken>()).Returns(UserId);
        repository.CancelScheduledRemoval(UserId, Arg.Any<CancellationToken>()).Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => service.Cancel(Secret));

        thrown.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    [Fact]
    public async Task TakeTheFactorOffWhenTheWaitingPeriodIsOver()
    {
        repository.FindRemovalsDue(Moment, Arg.Any<CancellationToken>()).Returns([UserId]);
        repository.RemoveIfDue(UserId, Moment, Arg.Any<CancellationToken>()).Returns(true);

        (await service.RunDue()).Should().Be(1);

        await repository.Received(1).RemoveIfDue(UserId, Moment, Arg.Any<CancellationToken>());
        await auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorDisabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
        // The cancellation link dies with the removal it was issued against:
        // opened afterwards, it must not say "already cancelled" about a
        // protection that is gone (SECURITY.md).
        await tokens.Received(1).RevokeTokens(
            UserId, TokenType.TwoFactorRemovalCancellation, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TakeNothingOffBeforeTheWaitingPeriodIsOver()
    {
        repository.FindRemovalsDue(Moment, Arg.Any<CancellationToken>()).Returns([]);

        (await service.RunDue()).Should().Be(0);

        await repository.DidNotReceive().RemoveIfDue(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A removal called off between the listing and the write is not carried out,
    /// and nothing is written about it anywhere.
    /// </summary>
    /// <remarks>
    /// Cancelling lands between the listing and the removal, so the write decides and everything after it waits.
    /// </remarks>
    [Fact]
    public async Task SayNothingAboutARemovalCalledOffWhileThePassWasRunning()
    {
        repository.FindRemovalsDue(Moment, Arg.Any<CancellationToken>()).Returns([UserId]);
        repository.RemoveIfDue(UserId, Moment, Arg.Any<CancellationToken>()).Returns(false);

        (await service.RunDue()).Should().Be(0, "nothing was taken off");

        await auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorDisabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    private void SignIn(UserRole role, Guid userId, string username = "boss")
    {
        var caller = Create.User(userId).WithRole(role).Please();
        caller.Username = username;
        identityProvider.Current.Returns(
            Identity.Success(caller, new Session { Id = Guid.NewGuid() }, UserSettings.Default, "t"));
    }

    /// <summary>Journalled on both accounts, sessions of the target ended.</summary>
    [Fact]
    public async Task TakeAColleaguesFactorOffAndSaySoInBothJournals()
    {
        SignIn(UserRole.Admin, AdminId);
        ArrangeAccount(UserRole.Admin);

        await service.ClearForColleague("reader");

        await repository.Received(1).Remove(UserId, Arg.Any<CancellationToken>());
        await sessionService.Received(1).LogoutAll(UserId);
        await auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRemovedByAdmin,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
        await auditService.Received(1).LogAsync(
            AdminId, SecurityEventType.TwoFactorRemovedByAdmin,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>The administrator's line is read on the security screen: a comma between its parts, the rank in words.</summary>
    [Fact]
    public async Task WordTheAdministratorsJournalLineForTheReader()
    {
        SignIn(UserRole.Admin, AdminId);
        ArrangeAccount(UserRole.Admin);

        await service.ClearForColleague("reader");

        await auditService.Received(1).LogAsync(
            AdminId, SecurityEventType.TwoFactorRemovedByAdmin,
            Arg.Any<string?>(), Arg.Any<string?>(),
            "Учетная запись: reader, роль администратора: Администратор");
    }

    [Theory]
    [InlineData(UserRole.SeniorModerator)]
    [InlineData(UserRole.Moderator)]
    [InlineData(UserRole.RegularUser)]
    public async Task RefuseToTakeAFactorOffBelowAdministrator(UserRole role)
    {
        SignIn(role, AdminId);
        ArrangeAccount(UserRole.Admin);

        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => service.ClearForColleague("reader"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The arrangement needs two administrators, so it must not serve one.
    /// </summary>
    [Fact]
    public async Task RefuseAnAdministratorTakingTheirOwnFactorOff()
    {
        SignIn(UserRole.Admin, UserId, "reader");
        ArrangeAccount(UserRole.Admin);

        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => service.ClearForColleague("reader"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseToTakeOffAFactorThatIsNotThere()
    {
        SignIn(UserRole.Admin, AdminId);
        ArrangeAccount(UserRole.Admin, confirmed: false);

        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => service.ClearForColleague("reader"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RefuseToTakeOffAFactorOfSomebodyWhoDoesNotExist()
    {
        SignIn(UserRole.Admin, AdminId);

        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => service.ClearForColleague("ghost"));

        thrown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
