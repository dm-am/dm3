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

    private readonly ITwoFactorRepository _repository;
    private readonly IConfirmationTokenRepository _tokens;
    private readonly ITwoFactorRemovalMailSender _mailSender;
    private readonly IAuthenticationService _authenticationService;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IIdentityProvider _identityProvider;
    private readonly TwoFactorRemovalService _service;

    public TwoFactorRemovalServiceShould()
    {
        _repository = Mock<ITwoFactorRepository>();
        _tokens = Mock<IConfirmationTokenRepository>();
        _mailSender = Mock<ITwoFactorRemovalMailSender>();
        _authenticationService = Mock<IAuthenticationService>();
        _auditService = Mock<ISecurityAuditRepository>();
        _identityProvider = Mock<IIdentityProvider>();
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

        _service = new TwoFactorRemovalService(
            _repository,
            _tokens,
            _mailSender,
            tokenFactory,
            _authenticationService,
            _identityProvider,
            _auditService,
            dateTimeProvider,
            Mock<ILogger<TwoFactorRemovalService>>(),
            Options.Create(new TwoFactorConfiguration { RemovalDelayDays = 7 }),
            Options.Create(new TokenConfiguration { TwoFactorRemovalTokenLifetimeHours = 24 }));
    }

    private void ArrangeAccount(UserRole role, bool confirmed = true)
    {
        var account = new TwoFactorAccount(
            UserId, "reader", "reader@example.com", role, "Europe/Moscow");
        _repository.FindAccountByEmail("reader@example.com", Arg.Any<CancellationToken>()).Returns(account);
        _repository.FindAccount(UserId, Arg.Any<CancellationToken>()).Returns(account);
        _repository.FindAccountByUsername("reader", Arg.Any<CancellationToken>()).Returns(account);
        _repository.IsConfirmed(UserId, Arg.Any<CancellationToken>()).Returns(confirmed);
        // The read above is the hint and the write is the decision, so a
        // substitute that answered "nothing scheduled" by default would refuse
        // every link in this file.
        _repository.ScheduleRemoval(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(confirmed);
    }

    // ── Asking ──

    [Fact]
    public async Task MailTheRequestToAnOrdinaryAccountWithAFactor()
    {
        ArrangeAccount(UserRole.RegularUser);

        await _service.Request("reader@example.com");

        await _tokens.Received(1).ReplaceToken(
            UserId, Arg.Is<CreateToken>(t => t.Type == TokenType.TwoFactorRemovalRequest),
            Arg.Any<CancellationToken>());
        await _mailSender.Received(1).SendRequest("reader@example.com", Secret);
    }

    /// <summary>
    /// A rank that owes a second factor takes the mailed path like anyone else.
    /// </summary>
    /// <remarks>
    /// The rank used to close this door, and closing it was the wrong way round:
    /// the mailed path is the only way back for the one person who has lost both
    /// the phone and the recovery codes, and the accounts it was shut for are
    /// exactly the accounts a lockout costs the most. The rank is not lost with
    /// the factor - it is withheld until a factor is set up again, which is the
    /// part that keeps a privileged account from running without one.
    /// </remarks>
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public async Task MailTheRequestToARankThatOwesAFactor(UserRole role)
    {
        ArrangeAccount(role);

        await _service.Request("reader@example.com");

        await _tokens.Received(1).ReplaceToken(
            UserId, Arg.Is<CreateToken>(t => t.Type == TokenType.TwoFactorRemovalRequest),
            Arg.Any<CancellationToken>());
        await _mailSender.Received(1).SendRequest("reader@example.com", Secret);
    }

    [Fact]
    public async Task IssueNothingForAnAddressWithNoAccount()
    {
        await _service.Request("stranger@example.com");

        await _tokens.DidNotReceive().ReplaceToken(
            Arg.Any<Guid>(), Arg.Any<CreateToken>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task IssueNothingForAnAccountWithoutAFactor()
    {
        ArrangeAccount(UserRole.RegularUser, confirmed: false);

        await _service.Request("reader@example.com");

        await _mailSender.DidNotReceive().SendRequest(
            Arg.Any<string>(), Arg.Any<Guid>());
    }

    // ── Following the link ──

    /// <summary>
    /// The link schedules a removal, it does not perform one, and the letter that
    /// says so goes out at the moment of scheduling.
    /// </summary>
    [Fact]
    public async Task ScheduleTheRemovalRatherThanPerformIt()
    {
        ArrangeAccount(UserRole.RegularUser);
        _tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalRequest, Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>()).Returns(UserId);

        await _service.Schedule(Secret);

        await _repository.Received(1).ScheduleRemoval(
            UserId, Moment.AddDays(7), Arg.Any<CancellationToken>());
        await _repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _authenticationService.Received(1).LogoutAll(UserId);
        await _mailSender.Received(1).SendScheduled(
            "reader@example.com", Moment.AddDays(7), Secret, "Europe/Moscow");
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRemovalScheduled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>
    /// A promotion between the letter and the link changes nothing about it.
    /// </summary>
    /// <remarks>
    /// The rank used to refuse here, which turned a promotion into a trap: the
    /// reader had the letter, followed it, and met a wall that no screen had
    /// warned him about and no action of his had caused.
    /// </remarks>
    [Fact]
    public async Task ScheduleARemovalForAnAccountPromotedSinceTheLinkWasIssued()
    {
        ArrangeAccount(UserRole.Admin);
        _tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalRequest, Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>()).Returns(UserId);

        await _service.Schedule(Secret);

        await _repository.Received(1).ScheduleRemoval(
            UserId, Moment.AddDays(7), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A factor switched off between the read and the write schedules nothing,
    /// and nothing that goes with a scheduling happens either.
    /// </summary>
    /// <remarks>
    /// The link is followed days after it was mailed and the owner may have
    /// switched the factor off in between - by password and code, or through a
    /// removal that came due. Told by the read alone, this ended every session,
    /// wrote the journal entry and mailed a date and a cancellation link that
    /// answered "already gone" the moment it was followed.
    /// </remarks>
    [Fact]
    public async Task ScheduleNothingForAFactorSwitchedOffSinceTheLetter()
    {
        ArrangeAccount(UserRole.RegularUser);
        _tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalRequest, Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>()).Returns(UserId);
        _repository.ScheduleRemoval(UserId, Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>())
            .Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Schedule(Secret));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
        thrown.Message.Should().Be(RefusalMessage.TwoFactorNotEnabled);
        await _authenticationService.DidNotReceive().LogoutAll(Arg.Any<Guid>());
        await _mailSender.DidNotReceive().SendScheduled(
            Arg.Any<string>(), Arg.Any<DateTimeOffset>(), Arg.Any<Guid>(), Arg.Any<string?>());
        await _auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorRemovalScheduled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task RefuseALinkNobodyIssued()
    {
        _tokens.RedeemToken(Arg.Any<Guid>(), Arg.Any<TokenType>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Schedule(Secret));

        thrown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Calling it off ──

    [Fact]
    public async Task CallTheRemovalOffFromTheSecondLink()
    {
        _tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalCancellation, null,
            Arg.Any<CancellationToken>()).Returns(UserId);
        _repository.CancelScheduledRemoval(UserId, Arg.Any<CancellationToken>()).Returns(true);

        await _service.Cancel(Secret);

        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRemovalCancelled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task RefuseToCallOffARemovalThatIsNotPending()
    {
        _tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalCancellation, null,
            Arg.Any<CancellationToken>()).Returns(UserId);
        _repository.CancelScheduledRemoval(UserId, Arg.Any<CancellationToken>()).Returns(false);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Cancel(Secret));

        thrown.StatusCode.Should().Be(HttpStatusCode.Gone);
    }

    // ── The waiting period running out ──

    [Fact]
    public async Task TakeTheFactorOffWhenTheWaitingPeriodIsOver()
    {
        _repository.FindRemovalsDue(Moment, Arg.Any<CancellationToken>()).Returns([UserId]);
        _repository.RemoveIfDue(UserId, Moment, Arg.Any<CancellationToken>()).Returns(true);

        (await _service.RunDue()).Should().Be(1);

        await _repository.Received(1).RemoveIfDue(UserId, Moment, Arg.Any<CancellationToken>());
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorDisabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
        // The cancellation link dies with the removal it was issued against:
        // opened afterwards, it must not say "already cancelled" about a
        // protection that is gone (SECURITY.md).
        await _tokens.Received(1).RevokeTokens(
            UserId, TokenType.TwoFactorRemovalCancellation, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TakeNothingOffBeforeTheWaitingPeriodIsOver()
    {
        _repository.FindRemovalsDue(Moment, Arg.Any<CancellationToken>()).Returns([]);

        (await _service.RunDue()).Should().Be(0);

        await _repository.DidNotReceive().RemoveIfDue(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A removal called off between the listing and the write is not carried out,
    /// and nothing is written about it anywhere.
    /// </summary>
    /// <remarks>
    /// The pass lists the accounts whose waiting period is over and then removes
    /// them one by one, and cancelling - the second link out of the letter, or any
    /// sign-in that passes the factor - lands in that gap. Told only by the listing
    /// the pass took the factor off an owner who had just kept it, and wrote him a
    /// journal entry saying so. The write is what decides, and everything after it
    /// waits for its answer.
    /// </remarks>
    [Fact]
    public async Task SayNothingAboutARemovalCalledOffWhileThePassWasRunning()
    {
        _repository.FindRemovalsDue(Moment, Arg.Any<CancellationToken>()).Returns([UserId]);
        _repository.RemoveIfDue(UserId, Moment, Arg.Any<CancellationToken>()).Returns(false);

        (await _service.RunDue()).Should().Be(0, "nothing was taken off");

        await _auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorDisabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    // ── The second administrator ──

    private void SignIn(UserRole role, Guid userId, string username = "boss")
    {
        var caller = Create.User(userId).WithRole(role).Please();
        caller.Username = username;
        _identityProvider.Current.Returns(
            Identity.Success(caller, new Session { Id = Guid.NewGuid() }, UserSettings.Default, "t"));
    }

    /// <summary>Journalled on both accounts, sessions of the target ended.</summary>
    [Fact]
    public async Task TakeAColleaguesFactorOffAndSaySoInBothJournals()
    {
        SignIn(UserRole.Admin, AdminId);
        ArrangeAccount(UserRole.Admin);

        await _service.ClearForColleague("reader");

        await _repository.Received(1).Remove(UserId, Arg.Any<CancellationToken>());
        await _authenticationService.Received(1).LogoutAll(UserId);
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRemovedByAdmin,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
        await _auditService.Received(1).LogAsync(
            AdminId, SecurityEventType.TwoFactorRemovedByAdmin,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>
    /// The caller is handed nothing - no session of the other account.
    /// </summary>
    [Fact]
    public async Task HandTheAdministratorNoSessionOfTheOtherAccount()
    {
        SignIn(UserRole.Admin, AdminId);
        ArrangeAccount(UserRole.Admin);

        await _service.ClearForColleague("reader");

        await _authenticationService.DidNotReceive().Authenticate(
            UserId, Arg.Any<SessionContext?>());
        _identityProvider.Current.User.UserId.Should().Be(AdminId);
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
            () => _service.ClearForColleague("reader"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
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
            () => _service.ClearForColleague("reader"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefuseToTakeOffAFactorThatIsNotThere()
    {
        SignIn(UserRole.Admin, AdminId);
        ArrangeAccount(UserRole.Admin, confirmed: false);

        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => _service.ClearForColleague("reader"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task RefuseToTakeOffAFactorOfSomebodyWhoDoesNotExist()
    {
        SignIn(UserRole.Admin, AdminId);

        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => _service.ClearForColleague("ghost"));

        thrown.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
