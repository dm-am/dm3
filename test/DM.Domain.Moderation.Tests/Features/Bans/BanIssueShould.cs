using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Moderation.Features.Bans;
using DM.Domain.Moderation.Tests.Dsl;
using AwesomeAssertions;
using FluentValidation;
using NSubstitute;
using Xunit;

namespace DM.Domain.Moderation.Tests.Features.Bans;

/// <summary>
/// The issue of a ban by a senior moderator or by the user himself: its term,
/// its kind and its announcement.
/// </summary>
public class BanIssueShould : BanServiceTestBase
{
    [Fact]
    public async Task ThrowWhenModeratorTriesToCreateNonVoluntaryBan()
    {
        SetCurrentUser(UserRole.Moderator);

        var createBan = new CreateBan { Username = "Target", IsVoluntary = false, DurationHours = 24 };
        var act = () => service.CreateBan(createBan);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Forbidden)
            .WithMessage("Банить может только старший модератор");
    }

    [Fact]
    public async Task LetAUserBanThemselvesVoluntarily()
    {
        SetCurrentUser(UserRole.RegularUser);
        userLookupService.GetAsync("Self").Returns(new GeneralUser { UserId = moderatorUserId, Username = "Self" });
        banRepository.GetActiveBan(moderatorUserId, Arg.Any<CancellationToken>()).Returns((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        banRepository.Create(Arg.Do<CreateBanEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ban());

        await service.CreateBan(new CreateBan { Username = "Self", IsVoluntary = true, Comment = "Отдых" });

        capturedEntity.Should().NotBeNull();
        capturedEntity!.TargetUserId.Should().Be(moderatorUserId);
        capturedEntity.AuthorId.Should().Be(moderatorUserId);
        capturedEntity.IsVoluntary.Should().BeTrue();
    }

    [Fact]
    public async Task ThrowWhenCreatingBanForAlreadyBannedUser()
    {
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "Target" };
        var existingBan = new Ban { BanId = Guid.NewGuid(), EndedUtc = now.AddDays(1) };

        userLookupService.GetAsync("Target").Returns(targetUser);
        banRepository.GetActiveBan(targetUserId, Arg.Any<CancellationToken>()).Returns(existingBan);

        var createBan = new CreateBan { Username = "Target", DurationHours = 24 };
        var act = () => service.CreateBan(createBan);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict)
            .WithMessage("Пользователь Target уже забанен");
    }

    [Fact]
    public async Task CreateBanWithDurationInHours()
    {
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "Target" };
        userLookupService.GetAsync("Target").Returns(targetUser);
        banRepository.GetActiveBan(targetUserId, Arg.Any<CancellationToken>()).Returns((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        banRepository.Create(Arg.Do<CreateBanEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ban());

        var createBan = new CreateBan
        {
            Username = "Target",
            DurationHours = 24,
            Comment = "Spam",
            IsVoluntary = false
        };

        await service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.BanId.Should().Be(banId);
        capturedEntity.TargetUserId.Should().Be(targetUserId);
        capturedEntity.AuthorId.Should().Be(moderatorUserId);
        capturedEntity.StartedUtc.Should().Be(now);
        capturedEntity.EndedUtc.Should().Be(now.AddHours(24));
        capturedEntity.IsVoluntary.Should().BeFalse();
    }

    [Fact]
    public async Task AnnounceAnIssuedBan()
    {
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "Target" };
        userLookupService.GetAsync("Target").Returns(targetUser);
        banRepository.GetActiveBan(targetUserId, Arg.Any<CancellationToken>()).Returns((Ban?)null);
        banRepository.Create(Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = banId });

        await service.CreateBan(new CreateBan
        {
            Username = "Target",
            DurationHours = 24,
            Comment = "Spam",
            IsVoluntary = false
        });

        await eventProducer.Received(1).SendAsync(EventType.BanIssued, banId);
    }

    [Fact]
    public async Task NotAnnounceAVoluntarySelfBan()
    {
        // The ban notification exists to tell a person about something he did not
        // do. Its only recipient is the target, who here is the author as well.
        var targetUser = new GeneralUser { UserId = moderatorUserId, Username = "Moderator" };
        userLookupService.GetAsync("Moderator").Returns(targetUser);
        banRepository.GetActiveBan(moderatorUserId, Arg.Any<CancellationToken>()).Returns((Ban?)null);
        banRepository.Create(Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>())
            .Returns(new Ban { BanId = banId });

        await service.CreateBan(new CreateBan
        {
            Username = "Moderator",
            DurationHours = 48,
            Comment = "Перерыв",
            IsVoluntary = true
        });

        await eventProducer.DidNotReceive().SendAsync(Arg.Any<EventType>(), Arg.Any<Guid>());
    }

    /// <summary>
    /// The scope is written as asked, and an unset one falls to the DTO default.
    /// </summary>
    [Theory]
    [InlineData(AccessPolicy.DemocraticBan, AccessPolicy.DemocraticBan)]
    [InlineData(null, AccessPolicy.FullBan)]
    public async Task WriteTheScopeOfTheBan(AccessPolicy? requested, AccessPolicy written)
    {
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "Target" };
        userLookupService.GetAsync("Target").Returns(targetUser);
        banRepository.GetActiveBan(targetUserId, Arg.Any<CancellationToken>()).Returns((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        banRepository.Create(Arg.Do<CreateBanEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ban());

        var createBan = new CreateBan { Username = "Target", DurationHours = 24, Comment = "Spam" };
        if (requested is { } scope)
        {
            createBan.AccessRestrictionPolicy = scope;
        }

        await service.CreateBan(createBan);

        capturedEntity!.AccessRestrictionPolicy.Should().Be(written);
    }

    /// <summary>
    /// A scope that is neither of the two would be a ban that forbids nothing, and the
    /// validator is what refuses it; its other bounds are CreateBanValidatorShould's.
    /// </summary>
    [Fact]
    public async Task ValidateTheBanBeforeIssuingIt()
    {
        var validated = new BanService(banRepository, userLookupService, identityProvider,
            guidFactory, dateTimeProvider, new CreateBanValidator(dateTimeProvider), eventProducer);
        userLookupService.GetAsync("Target").Returns(new GeneralUser { UserId = targetUserId, Username = "Target" });
        banRepository.GetActiveBan(targetUserId, Arg.Any<CancellationToken>()).Returns((Ban?)null);
        banRepository.Create(Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>()).Returns(new Ban());

        var act = () => validated.CreateBan(new CreateBan
        {
            Username = "Target",
            DurationHours = 24,
            Comment = "Spam",
            AccessRestrictionPolicy = AccessPolicy.NotSpecified
        });

        await act.Should().ThrowAsync<ValidationException>()
            .Where(e => e.Errors.Any(f => f.PropertyName == nameof(CreateBan.AccessRestrictionPolicy)));
        await banRepository.DidNotReceive().Create(Arg.Any<CreateBanEntity>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreatePermanentBanWhenNoDurationProvided()
    {
        var targetUser = new GeneralUser { UserId = targetUserId, Username = "Target" };
        userLookupService.GetAsync("Target").Returns(targetUser);
        banRepository.GetActiveBan(targetUserId, Arg.Any<CancellationToken>()).Returns((Ban?)null);

        CreateBanEntity? capturedEntity = null;
        banRepository.Create(Arg.Do<CreateBanEntity>(e => capturedEntity = e), Arg.Any<CancellationToken>())
            .Returns(new Ban());

        var createBan = new CreateBan { Username = "Target", Comment = "Permanent ban" };
        await service.CreateBan(createBan);

        capturedEntity.Should().NotBeNull();
        capturedEntity!.EndedUtc.Should().Be(now.AddYears(Ban.PermanentYears));
    }
}
