using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Warnings;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Warnings;

/// <summary>
/// The issue of a warning: who may issue it and to whom, what is written, and
/// the automatic ban its points lead to.
/// </summary>
public class WarningIssueShould : WarningServiceTestBase
{
    [Fact]
    public async Task ThrowWhenNonModeratorTriesToCreateWarning()
    {
        Authenticate(Guid.NewGuid(), UserRole.RegularUser, "User");

        var createWarning = new CreateWarning { Username = "Target", Reason = "Bad behavior", Points = 2 };
        var act = () => service.CreateWarning(createWarning);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Выносить предупреждения может только модератор");
    }

    /// <summary>
    /// A verbal warning carries no points, and the type of the object is read in any case.
    /// </summary>
    [Theory]
    [InlineData("Topic", WarningEntityType.Topic, 2)]
    [InlineData("comment", WarningEntityType.Comment, 0)]
    public async Task CreateWarningWithCorrectData(string entityType, WarningEntityType parsed, int points)
    {
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "Target" };
        userLookupService.GetAsync("Target").Returns(targetUser);

        CreateWarningEntity? capturedEntity = null;
        warningRepository.Create(Arg.Do<CreateWarningEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Warning());

        var entityId = Guid.NewGuid();
        var createWarning = new CreateWarning
        {
            Username = "Target",
            EntityId = entityId,
            EntityType = entityType,
            Reason = "Spam",
            Points = points
        };

        await service.CreateWarning(createWarning);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.WarningId.Should().Be(warningId);
        capturedEntity.TargetUserId.Should().Be(targetUserId);
        capturedEntity.AuthorId.Should().Be(moderatorUserId);
        capturedEntity.EntityId.Should().Be(entityId);
        capturedEntity.EntityType.Should().Be(parsed);
        capturedEntity.Text.Should().Be("Spam");
        capturedEntity.Points.Should().Be(points);
        capturedEntity.CreatedUtc.Should().Be(now);
    }

    /// <summary>
    /// Nothing in a game room is moderated, so a warning cannot name a game post.
    /// </summary>
    [Fact]
    public async Task RefuseAWarningOnGameContent()
    {
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });

        var act = () => service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            EntityId = Guid.NewGuid(),
            EntityType = "Post",
            Reason = "Оскорбление в игровом посте",
            Points = 2
        });

        (await act.Should().ThrowAsync<HttpBadRequestException>())
            .Which.ValidationErrors["entityType"].Should().StartWith("Это место не модерируется");

        await warningRepository.DidNotReceive().Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>());
        await entityResolver.DidNotReceive().CaptureSnapshot(
                Arg.Any<WarningEntityType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Only the global chat is moderated: a message the resolver takes no copy of, a direct one, one of a
    /// group or of a game room, is refused, and nothing is written.
    /// </summary>
    [Fact]
    public async Task RefuseAWarningOnAMessageOutsideTheGlobalChat()
    {
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        var messageId = Guid.NewGuid();
        entityResolver.CaptureSnapshot(WarningEntityType.Message, messageId, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var act = () => service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            EntityId = messageId,
            EntityType = "Message",
            Reason = "Оскорбление в личной переписке",
            Points = 2
        });

        (await act.Should().ThrowAsync<HttpBadRequestException>())
            .Which.ValidationErrors["entityType"].Should().StartWith("Это место не модерируется");
        await warningRepository.DidNotReceive().Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A warning to the user himself, for what was done where nothing is moderated: no kind, no object and no
    /// copy of a text, whatever identifier the request carries.
    /// </summary>
    [Fact]
    public async Task IssueAWarningToTheUserWithoutAPlace()
    {
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        CreateWarningEntity? captured = null;
        warningRepository.Create(Arg.Do<CreateWarningEntity>(e => captured = e), Arg.Any<CancellationToken>())
            .Returns(new Warning());

        await service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            EntityId = Guid.NewGuid(),
            Reason = "Оскорбления в личной переписке",
            Points = 2
        });

        captured.Should().NotBeNull();
        captured!.EntityType.Should().Be(WarningEntityType.Unknown);
        captured.EntityId.Should().Be(Guid.Empty);
        captured.EntitySnapshot.Should().BeNull();
        captured.Points.Should().Be(2);
        await entityResolver.DidNotReceive().CaptureSnapshot(
            Arg.Any<WarningEntityType>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A warning reaches only a rank below the moderator's own, and never an
    /// administrator (ModerationRank, the rule of every action against a
    /// person): a moderator warns neither a colleague nor a senior moderator nor
    /// an administrator, a senior moderator warns a moderator but not another
    /// senior moderator, and an administrator warns anybody but an administrator.
    /// </summary>
    /// <remarks>
    /// Strictly below, or a senior moderator could take the points off his own warning.
    /// </remarks>
    [Theory]
    [InlineData(UserRole.Moderator, UserRole.Moderator)]
    [InlineData(UserRole.Moderator, UserRole.SeniorModerator)]
    [InlineData(UserRole.Moderator, UserRole.Admin)]
    [InlineData(UserRole.SeniorModerator, UserRole.SeniorModerator)]
    [InlineData(UserRole.SeniorModerator, UserRole.Admin)]
    [InlineData(UserRole.Admin, UserRole.Admin)]
    public async Task RefuseAWarningToAnEqualOrHigherRank(UserRole issuer, UserRole target)
    {
        Authenticate(moderatorUserId, issuer, "Issuer");
        userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = targetUserId, Username = "Target", Role = target });

        var act = () => service.CreateWarning(new CreateWarning { Username = "Target", Reason = "Флуд", Points = 1 });

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden);
        await warningRepository.DidNotReceive().Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// And reaches every rank below: the reader, the mentor, and for a senior
    /// moderator and an administrator the moderators under them.
    /// </summary>
    [Theory]
    [InlineData(UserRole.Moderator, UserRole.RegularUser)]
    [InlineData(UserRole.Moderator, UserRole.Mentor)]
    [InlineData(UserRole.SeniorModerator, UserRole.Moderator)]
    [InlineData(UserRole.Admin, UserRole.SeniorModerator)]
    public async Task IssueAWarningToALowerRank(UserRole issuer, UserRole target)
    {
        Authenticate(moderatorUserId, issuer, "Issuer");
        userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = targetUserId, Username = "Target", Role = target });
        warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning { WarningId = warningId });

        await service.CreateWarning(new CreateWarning { Username = "Target", Reason = "Флуд", Points = 1 });

        await warningRepository.Received(1).Create(
            Arg.Is<CreateWarningEntity>(w => w.TargetUserId == targetUserId), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AnnounceAnIssuedWarning()
    {
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "Target" };
        userLookupService.GetAsync("Target").Returns(targetUser);
        warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning { WarningId = warningId });

        await service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            Reason = "Spam",
            Points = 2
        });

        await eventProducer.Received(1).SendAsync(EventType.WarningIssued, warningId);
    }

    /// <summary>
    /// The warning that carries the total to the threshold hands the reader to
    /// the site's own ban.
    /// </summary>
    /// <remarks>
    /// The whole of the automatic ban from this side: the service reads the total
    /// before the write and after it, and asks for the ban on the crossing. What
    /// the ban is made of - two days, the ordinary scope, the system account - is
    /// BanService's, and AutomaticBanShould holds it.
    /// </remarks>
    [Fact]
    public async Task BanTheReaderTheLastPointCarriesOverTheThreshold()
    {
        userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning { WarningId = warningId });
        // Four before the write, six after it.
        warningRepository.GetUserWarningPoints(targetUserId, Arg.Any<CancellationToken>())
            .Returns(4, 6);

        await service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            Reason = "Оскорбление",
            Points = 2
        });

        await banService.Received(1).IssueAutomaticBan(targetUserId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A reader who already stood at the threshold is not banned again by the
    /// next warning.
    /// </summary>
    /// <remarks>
    /// Firing on the standing rather than on the crossing would put a fresh
    /// two-day window on him at every touch of his record for as long as the
    /// points live, including the moment the previous window ran out.
    /// </remarks>
    [Fact]
    public async Task NotBanAgainAReaderWhoWasAlreadyOverTheThreshold()
    {
        userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning { WarningId = warningId });
        warningRepository.GetUserWarningPoints(targetUserId, Arg.Any<CancellationToken>())
            .Returns(6, 8);

        await service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            Reason = "Оскорбление",
            Points = 2
        });

        await banService.DidNotReceive().IssueAutomaticBan(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// Below the threshold nothing is issued: the points are a record until they
    /// add up to it.
    /// </summary>
    [Fact]
    public async Task NotBanBelowTheThreshold()
    {
        userLookupService.GetAsync("Target")
            .Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        warningRepository.Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Warning { WarningId = warningId });
        warningRepository.GetUserWarningPoints(targetUserId, Arg.Any<CancellationToken>())
            .Returns(1, 3);

        await service.CreateWarning(new CreateWarning
        {
            Username = "Target",
            Reason = "Флуд",
            Points = 2
        });

        await banService.DidNotReceive().IssueAutomaticBan(
            Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// A correction upwards reaches the threshold by the same door.
    /// </summary>
    /// <remarks>
    /// Raising the points of an old warning adds to the same total a fresh
    /// warning adds to, and the reader is over the line either way.
    /// </remarks>
    [Fact]
    public async Task BanWhenARaisedCorrectionCarriesTheTotalOverTheThreshold()
    {
        warningRepository.Get(warningId, Arg.Any<CancellationToken>()).Returns(Issued());
        warningRepository.ChangePoints(warningId, 6, moderatorUserId, now, "Нарушение тяжелее",
            Arg.Any<CancellationToken>()).Returns(true);
        warningRepository.GetUserWarningPoints(targetUserId, Arg.Any<CancellationToken>())
            .Returns(3, 6);

        await service.ChangeWarningPoints(warningId, new ChangeWarningPoints { Points = 6, Reason = "Нарушение тяжелее" });

        await banService.Received(1).IssueAutomaticBan(targetUserId, Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// The request goes through the validator before anything is read or written;
    /// the bounds themselves are CreateWarningValidatorShould's.
    /// </summary>
    [Fact]
    public async Task ValidateTheWarningBeforeIssuingIt()
    {
        var createWarning = new CreateWarning
        {
            Username = "Target",
            Reason = "Very bad behavior",
            Points = WarningPolicy.MaxPointsPerWarning + 1
        };

        var act = () => service.CreateWarning(createWarning);

        await act.Should().ThrowAsync<ValidationException>();
        await warningRepository.DidNotReceive().Create(Arg.Any<CreateWarningEntity>(), Arg.Any<CancellationToken>());
    }
}
