using System.Net;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Game;

/// <summary>
/// A [private] block in a listing of posts opens for its addressee and stays
/// closed for everybody else.
/// </summary>
/// <remarks>
/// The rule lives at serialization time: the mapper puts the frozen
/// addressee snapshot into the render-context envelope, and the JSON
/// converter combines it with the current viewer. Losing the envelope
/// anywhere on that path fails in one of two directions - the addressee
/// stops seeing what was written to them, or the whole room starts seeing
/// it - and neither direction is visible to a unit test of either half.
/// This is the real HTTP path over the seeded room: one post, read by its
/// addressee, by a signed-in stranger, and by nobody at all.
/// </remarks>
public class RoomPostPrivacyShould : IntegrationTestBase
{
    private const string PublicText = "Открытый текст поста, виден всем в комнате.";
    private const string PrivateText = "Тайное послание, адресовано одному Гориму.";

    public RoomPostPrivacyShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task ShowThePrivateBlockToItsAddresseeOnlyInTheRoomListing()
    {
        var postId = await CreatePrivatePost();
        try
        {
            // SecondUser owns the addressed character (the snapshot names
            // their user id), so the block opens for them.
            var addressee = CustomWebApplicationFactory.CreateSecondUser();
            var addresseeBody = await GetRoomPosts(addressee);
            addresseeBody.Should().Contain(PublicText);
            addresseeBody.Should().Contain(
                PrivateText, "the addressee-forever rule is what the snapshot exists for");

            // An authenticated reader of the same room who is neither the
            // author, nor a game lead, nor addressed: the public text is
            // theirs to read, the private block is not.
            var stranger = new GeneralUser
            {
                UserId = TestConstants.InactiveUser1Id,
                Username = TestConstants.InactiveUser1Username,
                Role = UserRole.RegularUser,
                AccessPolicy = AccessPolicy.NotSpecified
            };
            var strangerBody = await GetRoomPosts(stranger);
            strangerBody.Should().Contain(PublicText, "the post itself is public room content");
            strangerBody.Should().NotContain(
                PrivateText, "a [private] block must not serialize for a non-addressee");
        }
        finally
        {
            await PostTestHelper.DropPost(DatabaseFixture, postId);
        }
    }

    /// <summary>
    /// Nobody reads a [private] block by not signing in — on either listing,
    /// with either audience.
    /// </summary>
    /// <remarks>
    /// Both endpoints serve the same post through different projections, and
    /// only one of them was ever covered. The rated listing built its own post
    /// by hand: it filled the text and left the author, the game, the master,
    /// the assistants and both private-text overrides at their defaults, all of
    /// which are the empty Guid — which is the id an anonymous reader carries.
    /// The rule "a game lead sees every [private] block" then fired for every
    /// reader who was not signed in, and the same coincidence turned the
    /// author_edit header into a way to ask for the unfiltered source.
    ///
    /// The listings are enumerated rather than sampled: a post that reaches a
    /// reader one row at a time and a post that reaches them twenty at a time
    /// are the same post, and the reader has the same right to it.
    /// </remarks>
    [Fact]
    public async Task HideThePrivateBlockFromAnAnonymousReaderOnEveryPostListing()
    {
        var postId = await CreatePrivatePost();
        try
        {
            string[] listings =
            [
                $"/v1/rooms/{TestConstants.TestRoomId}/posts",
                $"/v1/posts?gameId={TestConstants.TestGameId}&sortBy=created&sortOrder=desc"
            ];

            foreach (var listing in listings)
            {
                foreach (var audience in new string?[] { null, "author_edit" })
                {
                    var body = await GetAnonymously(listing, audience);

                    body.Should().Contain(PublicText,
                        $"the post is public room content and has to be on {listing}");
                    body.Should().NotContain(PrivateText,
                        $"{listing} must not hand a [private] block to a reader who is not signed in");
                }
            }
        }
        finally
        {
            await PostTestHelper.DropPost(DatabaseFixture, postId);
        }
    }

    private async Task<string> GetAnonymously(string url, string? audience)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (audience is not null)
        {
            request.Headers.Add(BbAudienceHeader.HeaderName, audience);
        }

        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    private async Task<string> GetRoomPosts(GeneralUser viewer)
    {
        var request = CreateAuthenticatedRequest(
            HttpMethod.Get, $"/v1/rooms/{TestConstants.TestRoomId}/posts", viewer);
        var response = await Client.SendAsync(request);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// A post by the game master into the seeded open room, with one
    /// [private] block whose frozen snapshot resolves the addressed
    /// character name to SecondUser - exactly what the save path writes.
    /// The room keeps ViewPrivateText = false and the post does not share
    /// private text, so neither override opens the block.
    /// </summary>
    private Task<Guid> CreatePrivatePost() =>
        PostTestHelper.CreatePrivatePost(DatabaseFixture, PublicText, PrivateText);

}
