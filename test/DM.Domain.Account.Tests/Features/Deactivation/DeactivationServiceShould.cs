using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Authentication;
using DM.Domain.Account.Features.Deactivation;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Testing;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;
using RemovedAccount = DM.Domain.Account.Features.Deactivation.RemovedAccount;

namespace DM.Domain.Account.Tests.Features.Deactivation;

public class DeactivationServiceShould : UnitTestBase
{
    private const string TargetEmail = "target@dm.am";

    private readonly IIdentityProvider _identityProvider;
    private readonly IUserLookupService _userLookupService;
    private readonly IDeactivationRepository _repository;
    private readonly IAccountRemover _remover;
    private readonly IAccountRemovalMailSender _removalMailSender;
    private readonly IAccountRestorationMailSender _restorationMailSender;
    private readonly DeactivationService _service;

    public DeactivationServiceShould()
    {
        _identityProvider = Mock<IIdentityProvider>();
        _userLookupService = Mock<IUserLookupService>();
        _repository = Mock<IDeactivationRepository>();
        _remover = Mock<IAccountRemover>();
        _removalMailSender = Mock<IAccountRemovalMailSender>();
        _restorationMailSender = Mock<IAccountRestorationMailSender>();

        _service = new DeactivationService(
            _identityProvider,
            _userLookupService,
            _repository,
            _remover,
            _removalMailSender,
            _restorationMailSender,
            Mock<ILogger<DeactivationService>>());
    }

    /// <summary>
    /// The only door out: a senior moderator removes an account from the
    /// moderation screen, without the password of the person who asked.
    /// </summary>
    /// <remarks>
    /// Nothing self-service is left beside it and nothing is deferred, so this
    /// call is the whole of what removal means in the product. A reader who
    /// wants his account gone writes to support and a person acts on it.
    /// </remarks>
    [Fact]
    public async Task RemoveAnAccountOnAModeratorsDecision()
    {
        var targetUserId = Guid.NewGuid();
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeTarget("Target", targetUserId, UserRole.RegularUser);

        await _service.DeactivateByModeration("Target", "Запрос в обращении 1234");

        await _remover.Received(1).Remove(targetUserId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The owner of a removed account learns of it by letter and by nothing else.
    /// </summary>
    /// <remarks>
    /// The account screens offer nothing about removal, because the decision is a
    /// moderator's rather than a step its owner walks through. Without the letter
    /// a person is left with an account that simply stopped answering.
    /// </remarks>
    [Fact]
    public async Task TellTheOwnerThatHisAccountIsGone()
    {
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeTarget("Target", Guid.NewGuid(), UserRole.RegularUser);

        await _service.DeactivateByModeration("Target", "Запрос в обращении 1234");

        await _removalMailSender.Received(1).SendCompleted(TargetEmail);
    }

    /// <summary>
    /// An ordinary moderator does not remove accounts.
    /// </summary>
    [Fact]
    public async Task RefuseTheModerationRemovalBelowSeniorModerator()
    {
        AuthenticateAs(UserRole.Moderator);
        ArrangeTarget("Target", Guid.NewGuid(), UserRole.RegularUser);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.DeactivateByModeration("Target", "Просто так"));

        exception.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _remover.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _removalMailSender.DidNotReceive().SendCompleted(Arg.Any<string>());
    }

    /// <summary>
    /// The reach of a ban: never an administrator, never an equal rank.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator)]
    public async Task RefuseTheModerationRemovalOfAnEqualOrHigherRank(UserRole targetRole)
    {
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeTarget("Target", Guid.NewGuid(), targetRole);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.DeactivateByModeration("Target", "Есть основания"));

        exception.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _remover.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _removalMailSender.DidNotReceive().SendCompleted(Arg.Any<string>());
    }

    /// <summary>
    /// An account removed with nothing written down leaves no record of who took
    /// the decision or why.
    /// </summary>
    [Fact]
    public async Task RefuseTheModerationRemovalWithoutAReason()
    {
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeTarget("Target", Guid.NewGuid(), UserRole.RegularUser);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => _service.DeactivateByModeration("Target", "   "));

        exception.ValidationErrors.Should().ContainKey("reason");
        await _remover.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _removalMailSender.DidNotReceive().SendCompleted(Arg.Any<string>());
    }

    /// <summary>
    /// Restoration is the removal read backwards: a moderator names the account
    /// and it answers again the same moment, with nothing to file and nothing to
    /// wait for.
    /// </summary>
    [Fact]
    public async Task RestoreAnAccountOnAModeratorsDecision()
    {
        var targetUserId = Guid.NewGuid();
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeRemoved("Target", targetUserId);

        await _service.RestoreByModeration("Target");

        await _repository.Received(1).Restore(targetUserId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The owner learns his account is back the way he learned it was gone.
    /// </summary>
    /// <remarks>
    /// He filed nothing and is waiting on no answer - somebody read his letter to
    /// support and acted on it - so without this letter the only way to find out
    /// is to keep trying to sign in.
    /// </remarks>
    [Fact]
    public async Task TellTheOwnerThatHisAccountIsBack()
    {
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeRemoved("Target", Guid.NewGuid());

        await _service.RestoreByModeration("Target");

        await _restorationMailSender.Received(1).SendRestored(TargetEmail);
    }

    /// <summary>
    /// Restoring takes the rank removing takes.
    /// </summary>
    [Fact]
    public async Task RefuseTheRestorationBelowSeniorModerator()
    {
        AuthenticateAs(UserRole.Moderator);
        ArrangeRemoved("Target", Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.RestoreByModeration("Target"));

        exception.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().Restore(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _restorationMailSender.DidNotReceive().SendRestored(Arg.Any<string>());
    }

    /// <summary>
    /// An account nobody removed has nothing to restore, and its owner gets no
    /// letter telling him otherwise.
    /// </summary>
    [Fact]
    public async Task RefuseTheRestorationOfAnAccountNobodyRemoved()
    {
        AuthenticateAs(UserRole.SeniorModerator);
        _repository.GetRemovedByUsername("Living", Arg.Any<CancellationToken>())
            .Returns((RemovedAccount?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.RestoreByModeration("Living"));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await _repository.DidNotReceive().Restore(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _restorationMailSender.DidNotReceive().SendRestored(Arg.Any<string>());
    }

    /// <summary>
    /// A removed row under the name that signs everything removed accounts wrote stays
    /// removed, however it is asked for.
    /// </summary>
    /// <remarks>
    /// Checked on the stored name: the lookup compares names without case, so the
    /// moderator's spelling proves nothing about the row that would come back. The
    /// DM2 import brings the shared "Аккаунт Удален" account in exactly like this.
    /// </remarks>
    [Theory]
    [InlineData("Аккаунт Удален")]
    [InlineData("Aккаунт удален")]
    public async Task RefuseToBringBackTheReservedName(string storedName)
    {
        AuthenticateAs(UserRole.SeniorModerator);
        _repository.GetRemovedByUsername("asked", Arg.Any<CancellationToken>())
            .Returns(ArrangedAccount(storedName, Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.RestoreByModeration("asked"));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exception.Message.Should().Be(RefusalMessage.ReservedNameNotRestorable);
        await _repository.DidNotReceive().Restore(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _restorationMailSender.DidNotReceive().SendRestored(Arg.Any<string>());
    }

    /// <summary>
    /// The list of the removed is the only place they can be found at all, and it
    /// carries the search term and the page the caller asked for.
    /// </summary>
    [Fact]
    public async Task ListTheRemovedAccountsWithTheirPage()
    {
        AuthenticateAs(UserRole.Admin);
        ArrangeRemovedList(totalCount: 3, ArrangedAccount("Target", Guid.NewGuid()));

        var (accounts, paging) = await _service.GetRemovedAccounts(new RemovedAccountsQuery { Search = "tar" });

        accounts.Should().ContainSingle().Which.Username.Should().Be("Target");
        paging.TotalEntitiesCount.Should().Be(3);
        await _repository.Received(1).CountRemoved("tar", Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An address is an administrator's to read, on this list as on the profile.
    /// </summary>
    /// <remarks>
    /// The moderated profile hands the address to an administrator and nulls it
    /// for everybody else. A list is where the same leak would be wholesale: one
    /// page, every address on it, for a rank that may not see a single one.
    /// </remarks>
    [Fact]
    public async Task HideTheAddressesFromAModeratorBelowAdmin()
    {
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeRemovedList(totalCount: 1, ArrangedAccount("Target", Guid.NewGuid()));

        var (accounts, _) = await _service.GetRemovedAccounts(new RemovedAccountsQuery());

        accounts.Should().ContainSingle().Which.Email.Should().BeNull();
    }

    /// <summary>
    /// An administrator reads the addresses, which is what lets a support letter
    /// be matched to the account it is about.
    /// </summary>
    [Fact]
    public async Task ShowTheAddressesToAnAdministrator()
    {
        AuthenticateAs(UserRole.Admin);
        ArrangeRemovedList(totalCount: 1, ArrangedAccount("Target", Guid.NewGuid()));

        var (accounts, _) = await _service.GetRemovedAccounts(new RemovedAccountsQuery());

        accounts.Should().ContainSingle().Which.Email.Should().Be(TargetEmail);
    }

    /// <summary>
    /// The removed are hidden from the whole product, and this list does not open
    /// them to a rank that could not act on them anyway.
    /// </summary>
    [Fact]
    public async Task RefuseTheRemovedListBelowSeniorModerator()
    {
        AuthenticateAs(UserRole.Moderator);
        ArrangeRemovedList(totalCount: 1, ArrangedAccount("Target", Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => _service.GetRemovedAccounts(new RemovedAccountsQuery()));

        exception.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await _repository.DidNotReceive().GetRemoved(
            Arg.Any<string?>(), Arg.Any<PagingData>(), Arg.Any<CancellationToken>());
    }

    private void AuthenticateAs(UserRole role) =>
        _identityProvider.Current.Returns(Identity.Success(
            new AuthenticatedUser { UserId = Guid.NewGuid(), Role = role, Username = "Moderator" },
            new Session { Id = Guid.NewGuid() },
            UserSettings.Default,
            "token"));

    private void ArrangeTarget(string username, Guid userId, UserRole role) =>
        _userLookupService.GetAsync(username).Returns(new GeneralUser
        {
            UserId = userId,
            Username = username,
            Email = TargetEmail,
            Role = role
        });

    private void ArrangeRemoved(string username, Guid userId) =>
        _repository.GetRemovedByUsername(username, Arg.Any<CancellationToken>())
            .Returns(ArrangedAccount(username, userId));

    private void ArrangeRemovedList(int totalCount, params RemovedAccount[] accounts)
    {
        _repository.CountRemoved(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(totalCount);
        _repository.GetRemoved(Arg.Any<string?>(), Arg.Any<PagingData>(), Arg.Any<CancellationToken>())
            .Returns(accounts.ToList());
    }

    private static RemovedAccount ArrangedAccount(string username, Guid userId) => new(
        userId,
        username,
        TargetEmail,
        UserRole.RegularUser,
        DateTimeOffset.UtcNow.AddYears(-1),
        DateTimeOffset.UtcNow.AddDays(-30));
}
