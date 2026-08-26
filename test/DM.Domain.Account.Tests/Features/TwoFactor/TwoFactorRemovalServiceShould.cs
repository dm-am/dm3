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
/// The letter does not take the factor off (INV-13): it schedules the removal a
/// week out, ends every session, and any successful sign-in with the factor calls
/// it off. For the ranks that owe a factor the mailed path is closed altogether
/// (INV-12) and the second administrator is the only door.
/// </remarks>
public class TwoFactorRemovalServiceShould : UnitTestBase
{
    private static readonly Guid UserId = Guid.Parse("11112222-3333-4444-5555-666677778888");
    private static readonly Guid AdminId = Guid.Parse("99990000-1111-2222-3333-444455556666");
    private static readonly DateTimeOffset Moment = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Secret = Guid.Parse("aaaabbbb-cccc-dddd-eeee-ffff00001111");

    private readonly ITwoFactorRepository _repository;
    private readonly ITwoFactorRemovalTokenRepository _tokens;
    private readonly ITwoFactorRemovalMailSender _mailSender;
    private readonly IAuthenticationService _authenticationService;
    private readonly ISecurityAuditRepository _auditService;
    private readonly IIdentityProvider _identityProvider;
    private readonly TwoFactorRemovalService _service;

    public TwoFactorRemovalServiceShould()
    {
        _repository = Mock<ITwoFactorRepository>();
        _tokens = Mock<ITwoFactorRemovalTokenRepository>();
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
        var account = new TwoFactorAccount(UserId, "reader", "reader@example.com", role);
        _repository.FindAccountByEmail("reader@example.com", Arg.Any<CancellationToken>()).Returns(account);
        _repository.FindAccount(UserId, Arg.Any<CancellationToken>()).Returns(account);
        _repository.FindAccountByUsername("reader", Arg.Any<CancellationToken>()).Returns(account);
        _repository.IsConfirmed(UserId, Arg.Any<CancellationToken>()).Returns(confirmed);
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
        await _mailSender.Received(1).SendRequest("reader@example.com", "reader", Secret);
    }

    /// <summary>AC-22: a privileged account gets nothing out of the mailed path.</summary>
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public async Task IssueNothingForARankThatOwesAFactor(UserRole role)
    {
        ArrangeAccount(role);

        await _service.Request("reader@example.com");

        await _tokens.DidNotReceive().ReplaceToken(
            Arg.Any<Guid>(), Arg.Any<CreateToken>(), Arg.Any<CancellationToken>());
        await _mailSender.DidNotReceive().SendRequest(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>());
        // The refusal is written where the owner of the account can read it: the
        // answer to the caller is the same for every address.
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRemovalRefused,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());

        // And written as a refusal. Under the scheduled type it used to render as
        // "Назначено снятие второго фактора", sending the owner to look for a
        // waiting period that was never started - and, worse, telling him
        // somebody had got further than they had.
        await _auditService.DidNotReceive().LogAsync(
            Arg.Any<Guid>(), SecurityEventType.TwoFactorRemovalScheduled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
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
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Guid>());
    }

    // ── Following the link ──

    /// <summary>
    /// INV-13: the link schedules a removal, it does not perform one, and the
    /// letter that says so goes out at the moment of scheduling.
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
            "reader@example.com", "reader", Moment.AddDays(7), Secret);
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorRemovalScheduled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    /// <summary>
    /// INV-12: a link that outlived a promotion still does not open the path.
    /// </summary>
    [Fact]
    public async Task RefuseALinkForAnAccountPromotedSinceItWasIssued()
    {
        ArrangeAccount(UserRole.Admin);
        _tokens.RedeemToken(Secret, TokenType.TwoFactorRemovalRequest, Arg.Any<DateTimeOffset?>(),
            Arg.Any<CancellationToken>()).Returns(UserId);

        var thrown = await Assert.ThrowsAsync<HttpException>(() => _service.Schedule(Secret));

        thrown.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().ScheduleRemoval(
            Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
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

        (await _service.RunDue()).Should().Be(1);

        await _repository.Received(1).Remove(UserId, Arg.Any<CancellationToken>());
        await _auditService.Received(1).LogAsync(
            UserId, SecurityEventType.TwoFactorDisabled,
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<string?>());
    }

    [Fact]
    public async Task TakeNothingOffBeforeTheWaitingPeriodIsOver()
    {
        _repository.FindRemovalsDue(Moment, Arg.Any<CancellationToken>()).Returns([]);

        (await _service.RunDue()).Should().Be(0);

        await _repository.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    // ── The second administrator ──

    private void SignIn(UserRole role, Guid userId, string username = "boss")
    {
        var caller = Create.User(userId).WithRole(role).Please();
        caller.Username = username;
        caller.ApplySecondFactorRequirement(secondFactorConfirmed: true);
        _identityProvider.Current.Returns(
            Identity.Success(caller, new Session { Id = Guid.NewGuid() }, UserSettings.Default, "t"));
    }

    /// <summary>AC-24: journalled on both accounts, sessions of the target ended.</summary>
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
    /// AC-24: the caller is handed nothing - no session of the other account.
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
    /// The withheld rank is what is compared, so an administrator who owes a
    /// factor cannot use the staff door to hand anybody a way in (INV-10).
    /// </summary>
    [Fact]
    public async Task RefuseAnAdministratorWhoseOwnRankIsWithheld()
    {
        var caller = Create.User(AdminId).WithRole(UserRole.Admin).Please();
        caller.Username = "boss";
        caller.ApplySecondFactorRequirement(secondFactorConfirmed: false);
        _identityProvider.Current.Returns(
            Identity.Success(caller, new Session { Id = Guid.NewGuid() }, UserSettings.Default, "t"));
        ArrangeAccount(UserRole.Admin);

        var thrown = await Assert.ThrowsAsync<HttpException>(
            () => _service.ClearForColleague("reader"));

        thrown.StatusCode.Should().Be(HttpStatusCode.Forbidden);
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
