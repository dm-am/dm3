using System;
using System.Reflection;
using AwesomeAssertions;
using DM.Domain.Messaging.Configuration;
using Xunit;
using static DM.Architecture.Tests.ClientCopies;

namespace DM.Architecture.Tests;

/// <summary>
/// The pencil is offered for as long as the server lets the author edit.
/// </summary>
/// <remarks>
/// The answers carry no "may edit" of their own, so the client counts the window on its clock
/// with a copy of the number, read out of its source here.
/// </remarks>
public class EditWindowShould
{
    private static readonly Assembly Community = typeof(DM.Domain.Community.Authorization.PollIntention).Assembly;
    private static readonly Assembly Game = typeof(DM.Domain.Game.Authorization.GameIntention).Assembly;

    private const string AuthorWindow = "shared/lib/utils/chat.ts";

    [Fact]
    public void OfferAMessageEditForTheMinutesTheServerAllows()
    {
        var client = Number(AuthorWindow, "EDIT_TIME_LIMIT_MINUTES");

        client.Should().Be(new MessagingConfiguration().EditTimeoutMinutes,
            "a message past the client's window shows no pencil the server would still honour");
        client.Should().Be(Setting(nameof(MessagingConfiguration), nameof(MessagingConfiguration.EditTimeoutMinutes)),
            "the deployed setting is the number the server answers with");
    }

    [Fact]
    public void OfferAPostReviewEditForTheMinutesTheServerAllows() =>
        Number(AuthorWindow, "EDIT_TIME_LIMIT_MINUTES").Should().Be(
            (long)Window(Game, "DM.Domain.Game.Features.PostReviews.PostReviewService").TotalMinutes,
            "PostReviewItem offers the pencil by this number, and the review service refuses past its own");

    [Fact]
    public void PromiseTheEndorsementWindowTheServerHolds()
    {
        Window(Community, "DM.Domain.Community.Features.UserEndorsements.UserEndorsementService")
            .Should().Be(TimeSpan.FromDays(1), "the form tells the author the window in words");

        Source("pages/profile/ProfileEndorsementForm.vue").Should().Contain("править ее можно сутки",
            "the words are the server's window of a day");
    }

    private static TimeSpan Window(Assembly assembly, string service) =>
        Member<TimeSpan>(assembly, service, "EditWindow");
}
