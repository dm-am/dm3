using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Testing;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.Configuration;
using AwesomeAssertions;
using Xunit;
using DomainUsernameHistoryEntry = DM.Domain.Core.Users.UsernameHistoryEntry;

namespace DM.Web.API.Tests.Features.Community;

// A nested user is filled by the query of the resource and counts no aggregates, so every aggregate
// is nullable and the serializer drops nulls.
public class UserFidelityShould : UnitTestBase
{
    // Everything the base projection fills, and therefore the whole of what a nested user may put
    // on the wire.
    private static readonly string[] Projected =
    {
        "id", "username", "lastActivityUtc", "role", "isNewbie",
        "rating", "picture", "registeredUtc",
    };

    private static UserMapper CreateMapper() => new();

    /// <summary>
    /// A user exactly as a nested projection produces one, off the user row alone.
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

    // The JSON the API would actually write, under the API's own conventions — camel case, string
    // enums, and nulls omitted.
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
