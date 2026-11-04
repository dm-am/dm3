using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Identity;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Moderation.Features.Bans;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Bans;

/// <summary>
/// The automatic ban the points of warnings lead to.
/// </summary>
public class AutomaticBanShould : BanServiceTestBase
{
    /// <summary>
    /// What the automatic ban is made of: two days, the ordinary scope, the
    /// system account.
    /// </summary>
    /// <remarks>
    /// Arithmetic issues it before a human reads the case, so the scope is the ordinary one and two days is the window for a senior moderator.
    /// </remarks>
    [Fact]
    public async Task IssueTheAutomaticBanForTwoDaysUnderTheSystemAccount()
    {
        ArrangeAutomaticTarget(UserRole.RegularUser);

        CreateBanEntity? captured = null;
        banRepository.Create(Arg.Do<CreateBanEntity>(e => captured = e), Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = banId });

        var ban = await service.IssueAutomaticBan(targetUserId);

        ban.Should().NotBeNull();
        captured.Should().NotBeNull();
        captured!.TargetUserId.Should().Be(targetUserId);
        captured.AuthorId.Should().Be(SystemUser.Id);
        captured.StartedUtc.Should().Be(now);
        // Spelled out rather than read off the constant, so a changed constant turns this red.
        captured.EndedUtc.Should().Be(now.AddDays(2));
        captured.AccessRestrictionPolicy.Should().Be(AccessPolicy.DemocraticBan);
        captured.IsVoluntary.Should().BeFalse();
        captured.Comment.Should().Be(WarningPolicy.AutoBanComment);
    }

    /// <summary>
    /// The banned reader is told, by the same event a moderator's ban sends.
    /// </summary>
    [Fact]
    public async Task AnnounceTheAutomaticBan()
    {
        ArrangeAutomaticTarget(UserRole.RegularUser);
        banRepository.Create(Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = banId });

        await service.IssueAutomaticBan(targetUserId);

        await eventProducer.Received(1).SendAsync(EventType.BanIssued, banId);
    }

    /// <summary>
    /// It is never stacked on a ban already running.
    /// </summary>
    /// <remarks>
    /// A second window on top would move the end date without anybody having decided anything.
    /// </remarks>
    [Fact]
    public async Task NotIssueTheAutomaticBanOverABanAlreadyRunning()
    {
        ArrangeAutomaticTarget(UserRole.RegularUser);
        banRepository.GetActiveBan(targetUserId, Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = Guid.NewGuid(), TargetUserId = targetUserId, EndedUtc = now.AddDays(3) });

        var ban = await service.IssueAutomaticBan(targetUserId);

        ban.Should().BeNull();
        await banRepository.DidNotReceive().Create(
            Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// An administrator is a site owner, and arithmetic does not ban one.
    /// </summary>
    /// <remarks>
    /// There is no in-app authority over an owner, and a threshold is no exception to it.
    /// </remarks>
    [Fact]
    public async Task NotIssueTheAutomaticBanToAnAdministrator()
    {
        ArrangeAutomaticTarget(UserRole.Admin);

        var ban = await service.IssueAutomaticBan(targetUserId);

        ban.Should().BeNull();
        await banRepository.DidNotReceive().Create(
            Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The rank of whoever happens to be signed in has no say in it.
    /// </summary>
    /// <remarks>
    /// An ordinary moderator issues warnings and may not ban, and the warning still has to lead to the ban.
    /// </remarks>
    [Fact]
    public async Task IssueTheAutomaticBanWhateverTheCallersRank()
    {
        SetCurrentUser(UserRole.Moderator);
        ArrangeAutomaticTarget(UserRole.RegularUser);
        banRepository.Create(Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = banId });

        var ban = await service.IssueAutomaticBan(targetUserId);

        ban.Should().NotBeNull();
    }

    /// <summary>
    /// The arrangement the automatic path needs: the target is looked up by id,
    /// not by name, and nothing is banned yet.
    /// </summary>
    private void ArrangeAutomaticTarget(UserRole targetRole)
    {
        userLookupService.GetAsync(targetUserId)
            .Returns(new GeneralUser { UserId = targetUserId, Username = "Target", Role = targetRole });
        banRepository.GetActiveBan(targetUserId, Arg.Any<CancellationToken>()).Returns((Ban?)null);
    }
}
