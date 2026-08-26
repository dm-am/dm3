using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Configuration;
using AwesomeAssertions;
using Xunit;
using DomainUsernameHistoryEntry = DM.Domain.Core.Users.UsernameHistoryEntry;

namespace DM.Web.API.Tests.Features.Community;

/// <summary>
/// The user schema goes out at two fidelities, and the response has to say which
/// one it is.
/// </summary>
/// <remarks>
/// A user nested in someone else's resource — the author of a comment, the
/// people who liked it — is filled by the query that fetched that resource, and
/// that query counts no bans, no drops, no subscribers and no username history:
/// those are a separate batch of aggregates that only the user repository runs.
/// While the members were plain ints and empty lists they went out as zeros, and
/// a zero somebody counted read exactly like a zero nobody counted — a card
/// reporting "нарушений: 0" under the name of a user with ten bans was
/// describing the query, not the user.
///
/// The discriminator is absence: every aggregate is nullable and the API
/// serializer drops nulls. These two tests hold both ends of that. The first
/// fails the moment a member goes back to non-nullable, because the zero
/// reappears on the wire. The second fails if the pendulum swings the other way
/// and a counted zero stops being sent.
/// </remarks>
public class UserFidelityShould : UnitTestBase
{
    /// <summary>
    /// Everything the base projection fills, and therefore the whole of what a
    /// nested user may put on the wire.
    /// </summary>
    private static readonly string[] Projected =
    {
        "id", "username", "lastActivityUtc", "role", "isNewbie",
        "rating", "picture", "registeredUtc",
    };

    private UserMapper CreateMapper() => new(Mock<IImgproxyUrlBuilder>());

    /// <summary>
    /// A user exactly as a nested projection produces one: identity, the two
    /// rating columns, the avatar key and the registration date all come off the
    /// user row, and nothing else does.
    /// </summary>
    private static GeneralUser Projection() => new()
    {
        UserId = Guid.NewGuid(),
        Username = "author",
        Role = UserRole.RegularUser,
        LastActivityUtc = new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero),
        RegisteredUtc = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero),
        QuantityRating = 120,
        QualityRating = 7,
        Picture = new AvatarPicture(),
    };

    /// <summary>
    /// The JSON the API would actually write, under the API's own conventions —
    /// camel case, string enums, and nulls omitted.
    /// </summary>
    private static JsonObject Wire(User user) => JsonNode
        .Parse(JsonSerializer.Serialize(user, new JsonSerializerOptions().ApplyApiConventions()))!
        .AsObject();

    [Fact]
    public void OmitTheAggregatesTheProjectionNeverComputed()
    {
        var wire = Wire(CreateMapper().ToUser(Projection()));

        wire.Select(property => property.Key).Should().BeEquivalentTo(Projected,
            "a nested user may carry only what the query that fetched it filled");
    }

    [Fact]
    public void KeepACountedZero()
    {
        var hydrated = Projection();
        hydrated.BansReceivedCount = 0;
        hydrated.GamesHosting = 0;
        hydrated.GameDropsCount = 3;
        hydrated.SubscribersByCategory = new SubscribersByCategory();
        hydrated.Subscribers = new[] { new SubscriberInfo { Username = "fan" } };
        hydrated.UsernameHistory = new[]
        {
            new DomainUsernameHistoryEntry
            {
                OldUsername = "johnny",
                NewUsername = "author",
                ChangedUtc = new DateTimeOffset(2025, 5, 1, 0, 0, 0, TimeSpan.Zero),
            },
        };

        var wire = Wire(CreateMapper().ToUser(hydrated));

        wire["bansReceived"]!.GetValue<int>().Should().Be(0,
            "a zero somebody counted is a fact about the user and has to be sent");
        wire["gamesHosting"]!.GetValue<int>().Should().Be(0);
        wire["gameDrops"]!.GetValue<int>().Should().Be(3);
        wire.ContainsKey("subscribersByCategory").Should()
            .BeTrue("the three totals were computed, even though they are zero");
        wire.ContainsKey("subscribers").Should().BeTrue();
        wire.ContainsKey("usernameHistory").Should().BeTrue();
    }
}
