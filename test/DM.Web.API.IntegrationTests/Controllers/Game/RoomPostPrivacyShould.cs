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
/// The mapper puts the frozen snapshot into the envelope and the converter combines it with the viewer; one post
/// read over HTTP by its addressee, a stranger and nobody.
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

    // Both listings serve the post through different projections; a lead id left as the empty Guid
    // matches the anonymous reader. Enumerated rather than sampled.
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

    private Task<Guid> CreatePrivatePost() =>
        PostTestHelper.CreatePrivatePost(DatabaseFixture, PublicText, PrivateText);

}
