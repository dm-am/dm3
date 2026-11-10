using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Account.Features.Deactivation;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Users;
using DM.Testing;
using DM.Testing.Dsl;
using AwesomeAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace DM.Domain.Account.Tests.Features.Deactivation;

public class DeactivationServiceShould : UnitTestBase
{
    private const string TargetEmail = "target@dm.am";

    private readonly IIdentityProvider identityProvider;
    private readonly IUserLookupService userLookupService;
    private readonly IDeactivationRepository repository;
    private readonly IAccountRemover remover;
    private readonly IAccountRemovalMailSender removalMailSender;
    private readonly IAccountRestorationMailSender restorationMailSender;
    private readonly DeactivationService service;

    public DeactivationServiceShould()
    {
        identityProvider = Mock<IIdentityProvider>();
        userLookupService = Mock<IUserLookupService>();
        repository = Mock<IDeactivationRepository>();
        remover = Mock<IAccountRemover>();
        removalMailSender = Mock<IAccountRemovalMailSender>();
        restorationMailSender = Mock<IAccountRestorationMailSender>();

        service = new DeactivationService(
            identityProvider,
            userLookupService,
            repository,
            remover,
            removalMailSender,
            restorationMailSender,
            Mock<ILogger<DeactivationService>>());
    }

    // The only door out: a senior moderator removes an account from the moderation screen, without
    // the password of the person who asked.
    [Fact]
    public async Task RemoveAnAccountOnAModeratorsDecision()
    {
        var targetUserId = Guid.NewGuid();
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeTarget("Target", targetUserId, UserRole.RegularUser);

        await service.DeactivateByModeration("Target", "Запрос в обращении 1234");

        await remover.Received(1).Remove(targetUserId, Arg.Any<CancellationToken>());
    }

    // Without the letter a person is left with an account that simply stopped answering.
    [Fact]
    public async Task TellTheOwnerThatHisAccountIsGone()
    {
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeTarget("Target", Guid.NewGuid(), UserRole.RegularUser);

        await service.DeactivateByModeration("Target", "Запрос в обращении 1234");

        await removalMailSender.Received(1).SendCompleted(TargetEmail);
    }

    [Fact]
    public async Task RefuseTheModerationRemovalBelowSeniorModerator()
    {
        AuthenticateAs(UserRole.Moderator);
        ArrangeTarget("Target", Guid.NewGuid(), UserRole.RegularUser);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.DeactivateByModeration("Target", "Просто так"));

        exception.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await remover.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await removalMailSender.DidNotReceive().SendCompleted(Arg.Any<string>());
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
            () => service.DeactivateByModeration("Target", "Есть основания"));

        exception.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await remover.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await removalMailSender.DidNotReceive().SendCompleted(Arg.Any<string>());
    }

    // An account removed with nothing written down leaves no record of who took the decision or
    // why.
    [Fact]
    public async Task RefuseTheModerationRemovalWithoutAReason()
    {
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeTarget("Target", Guid.NewGuid(), UserRole.RegularUser);

        var exception = await Assert.ThrowsAsync<HttpBadRequestException>(
            () => service.DeactivateByModeration("Target", "   "));

        exception.ValidationErrors.Should().ContainKey("reason");
        await remover.DidNotReceive().Remove(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await removalMailSender.DidNotReceive().SendCompleted(Arg.Any<string>());
    }

    [Fact]
    public async Task RestoreAnAccountOnAModeratorsDecision()
    {
        var targetUserId = Guid.NewGuid();
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeRemoved("Target", targetUserId);

        await service.RestoreByModeration("Target");

        await repository.Received(1).Restore(targetUserId, Arg.Any<CancellationToken>());
    }

    // He filed nothing and is waiting on no answer - somebody read his letter to support and acted
    // on it - so without this letter the only way to find out is to keep trying to sign in.
    [Fact]
    public async Task TellTheOwnerThatHisAccountIsBack()
    {
        AuthenticateAs(UserRole.SeniorModerator);
        ArrangeRemoved("Target", Guid.NewGuid());

        await service.RestoreByModeration("Target");

        await restorationMailSender.Received(1).SendRestored(TargetEmail);
    }

    [Fact]
    public async Task RefuseTheRestorationBelowSeniorModerator()
    {
        AuthenticateAs(UserRole.Moderator);
        ArrangeRemoved("Target", Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.RestoreByModeration("Target"));

        exception.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await repository.DidNotReceive().Restore(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await restorationMailSender.DidNotReceive().SendRestored(Arg.Any<string>());
    }

    // An account nobody removed has nothing to restore, and its owner gets no letter telling him
    // otherwise.
    [Fact]
    public async Task RefuseTheRestorationOfAnAccountNobodyRemoved()
    {
        AuthenticateAs(UserRole.SeniorModerator);
        repository.GetRemovedByUsername("Living", Arg.Any<CancellationToken>())
            .Returns((RemovedAccountRow?)null);

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.RestoreByModeration("Living"));

        exception.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await repository.DidNotReceive().Restore(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await restorationMailSender.DidNotReceive().SendRestored(Arg.Any<string>());
    }

    // Checked on the stored name: the lookup compares names without case, so the moderator's
    // spelling proves nothing about the row that would come back.
    [Theory]
    [InlineData("Аккаунт Удален")]
    [InlineData("Aккаунт удален")]
    public async Task RefuseToBringBackTheReservedName(string storedName)
    {
        AuthenticateAs(UserRole.SeniorModerator);
        repository.GetRemovedByUsername("asked", Arg.Any<CancellationToken>())
            .Returns(ArrangedAccount(storedName, Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.RestoreByModeration("asked"));

        exception.StatusCode.Should().Be(HttpStatusCode.Conflict);
        exception.Message.Should().Be(RefusalMessage.ReservedNameNotRestorable);
        await repository.DidNotReceive().Restore(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await restorationMailSender.DidNotReceive().SendRestored(Arg.Any<string>());
    }

    [Fact]
    public async Task ListTheRemovedAccountsWithTheirPage()
    {
        AuthenticateAs(UserRole.Admin);
        ArrangeRemovedList(totalCount: 3, ArrangedAccount("Target", Guid.NewGuid()));

        var (accounts, paging) = await service.GetRemovedAccounts(new RemovedAccountsQuery { Search = "tar" });

        accounts.Should().ContainSingle().Which.Username.Should().Be("Target");
        paging.TotalEntitiesCount.Should().Be(3);
        await repository.Received(1).CountRemoved("tar", Arg.Any<CancellationToken>());
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

        var (accounts, _) = await service.GetRemovedAccounts(new RemovedAccountsQuery());

        accounts.Should().ContainSingle().Which.Email.Should().BeNull();
    }

    // An administrator reads the addresses, which is what lets a support letter be matched to the
    // account it is about.
    [Fact]
    public async Task ShowTheAddressesToAnAdministrator()
    {
        AuthenticateAs(UserRole.Admin);
        ArrangeRemovedList(totalCount: 1, ArrangedAccount("Target", Guid.NewGuid()));

        var (accounts, _) = await service.GetRemovedAccounts(new RemovedAccountsQuery());

        accounts.Should().ContainSingle().Which.Email.Should().Be(TargetEmail);
    }

    // The removed are hidden from the whole product, and this list does not open them to a rank
    // that could not act on them anyway.
    [Fact]
    public async Task RefuseTheRemovedListBelowSeniorModerator()
    {
        AuthenticateAs(UserRole.Moderator);
        ArrangeRemovedList(totalCount: 1, ArrangedAccount("Target", Guid.NewGuid()));

        var exception = await Assert.ThrowsAsync<HttpException>(
            () => service.GetRemovedAccounts(new RemovedAccountsQuery()));

        exception.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await repository.DidNotReceive().GetRemoved(
            Arg.Any<string?>(), Arg.Any<PagingData>(), Arg.Any<CancellationToken>());
    }

    private void AuthenticateAs(UserRole role) =>
        identityProvider.Current.Returns(Identities.User(Guid.NewGuid(), "Moderator", role));

    private void ArrangeTarget(string username, Guid userId, UserRole role) =>
        userLookupService.GetAsync(username).Returns(new GeneralUser
        {
            UserId = userId,
            Username = username,
            Email = TargetEmail,
            Role = role
        });

    private void ArrangeRemoved(string username, Guid userId) =>
        repository.GetRemovedByUsername(username, Arg.Any<CancellationToken>())
            .Returns(ArrangedAccount(username, userId));

    private void ArrangeRemovedList(int totalCount, params RemovedAccountRow[] accounts)
    {
        repository.CountRemoved(Arg.Any<string?>(), Arg.Any<CancellationToken>()).Returns(totalCount);
        repository.GetRemoved(Arg.Any<string?>(), Arg.Any<PagingData>(), Arg.Any<CancellationToken>())
            .Returns(accounts.ToList());
    }

    private static RemovedAccountRow ArrangedAccount(string username, Guid userId) => new(
        userId,
        username,
        TargetEmail,
        UserRole.RegularUser,
        DateTimeOffset.UtcNow.AddYears(-1),
        DateTimeOffset.UtcNow.AddDays(-30));
}
