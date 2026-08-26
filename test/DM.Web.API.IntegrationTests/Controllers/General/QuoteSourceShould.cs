using System.Net;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// Who gets a quotation of a message, and what it contains.
/// </summary>
/// <remarks>
/// Two rules, and both are only provable over the real HTTP path. The first is
/// that a quotation is refused exactly where the message is refused - it is
/// meant to hold by construction, because the endpoint reads through the same
/// service the ordinary read goes through, and "by construction" is a claim
/// about code that a test has to check rather than repeat. The second is that a
/// private block never leaves in a quotation, for anyone, the author of the post
/// included: quoting is republication, and the addressees of the original block
/// do not travel with the text.
/// </remarks>
public class QuoteSourceShould : IntegrationTestBase
{
    private const string PublicText = "Открытый текст поста, виден всем в комнате.";
    private const string PrivateText = "Тайное послание, адресовано одному Гориму.";

    public QuoteSourceShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task RefuseAQuotationWhereverTheMessageItselfIsRefused()
    {
        // A reader who is in no chat at all: the message read answers 404, and
        // the quotation has to answer the same thing rather than something of
        // its own.
        var outsider = new GeneralUser
        {
            UserId = TestConstants.InactiveUser1Id,
            Username = TestConstants.InactiveUser1Username,
            Role = UserRole.RegularUser,
            AccessPolicy = AccessPolicy.NotSpecified
        };

        var read = await Send($"/v1/messages/{TestConstants.TestMessageId}", outsider);
        var quote = await Send($"/v1/messages/{TestConstants.TestMessageId}/quote", outsider);

        read.StatusCode.Should().NotBe(HttpStatusCode.OK,
            "the fixture is only meaningful while this reader cannot read the message");
        quote.StatusCode.Should().Be(read.StatusCode,
            "a quotation is refused by the same read that refuses the message");
    }

    [Fact]
    public async Task RefuseAQuotationOfAMessageThatDoesNotExist()
    {
        var unknown = Guid.NewGuid();

        var response = await Send($"/v1/messages/{unknown}/quote",
            CustomWebApplicationFactory.CreateTestUser());

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task LeaveThePrivateBlockOutOfTheQuotation_ForEveryReader()
    {
        var postId = await CreatePrivatePost();
        try
        {
            // The addressee is shown the block on the page. In a quotation it is
            // gone, because the new post would show it to a different room.
            var addressee = CustomWebApplicationFactory.CreateSecondUser();
            var addresseeQuote = await ReadQuote($"/v1/posts/{postId}/quote", addressee);
            addresseeQuote.Should().Contain(PublicText);
            addresseeQuote.Should().NotContain(PrivateText);

            // The author of the post, who sees his own block by the
            // author-forever rule and still does not get it back here.
            var author = CustomWebApplicationFactory.CreateTestUser();
            var authorQuote = await ReadQuote($"/v1/posts/{postId}/quote", author);
            authorQuote.Should().Contain(PublicText);
            authorQuote.Should().NotContain(PrivateText);

            // Anybody else, signed in or not.
            var stranger = new GeneralUser
            {
                UserId = TestConstants.InactiveUser1Id,
                Username = TestConstants.InactiveUser1Username,
                Role = UserRole.RegularUser,
                AccessPolicy = AccessPolicy.NotSpecified
            };
            var strangerQuote = await ReadQuote($"/v1/posts/{postId}/quote", stranger);
            strangerQuote.Should().Contain(PublicText);
            strangerQuote.Should().NotContain(PrivateText);

            var anonymousResponse = await Client.GetAsync($"/v1/posts/{postId}/quote");
            anonymousResponse.StatusCode.Should().Be(HttpStatusCode.OK);
            var anonymousQuote = await anonymousResponse.Content.ReadAsStringAsync();
            anonymousQuote.Should().Contain(PublicText);
            anonymousQuote.Should().NotContain(PrivateText);
        }
        finally
        {
            await PostTestHelper.DropPost(DatabaseFixture, postId);
        }
    }

    [Fact]
    public async Task WrapTheQuotationInATagCarryingTheAuthorName()
    {
        var postId = await CreatePrivatePost();
        try
        {
            var quote = await ReadQuote($"/v1/posts/{postId}/quote",
                CustomWebApplicationFactory.CreateTestUser());

            // The character speaks, so the character is named - the same order
            // the post header itself takes.
            quote.Should().Contain($"[quote=\\\"{TestConstants.TestCharacterName}\\\"]");
            quote.Should().Contain("[/quote]");
        }
        finally
        {
            await PostTestHelper.DropPost(DatabaseFixture, postId);
        }
    }

    [Fact]
    public async Task TellTheAuthorThatAPrivateBlockWasLeftOut()
    {
        var postId = await CreatePrivatePost();
        try
        {
            // The author is shown the block on the page, so losing it silently
            // is indistinguishable from losing text.
            var authorQuote = await ReadQuote($"/v1/posts/{postId}/quote",
                CustomWebApplicationFactory.CreateTestUser());
            authorQuote.Should().Contain("\"privateTextStripped\":true");

            // A reader who was never shown the block is not told it exists.
            var stranger = new GeneralUser
            {
                UserId = TestConstants.InactiveUser1Id,
                Username = TestConstants.InactiveUser1Username,
                Role = UserRole.RegularUser,
                AccessPolicy = AccessPolicy.NotSpecified
            };
            var strangerQuote = await ReadQuote($"/v1/posts/{postId}/quote", stranger);
            strangerQuote.Should().Contain("\"privateTextStripped\":false");
        }
        finally
        {
            await PostTestHelper.DropPost(DatabaseFixture, postId);
        }
    }

    private async Task<HttpResponseMessage> Send(string url, GeneralUser viewer) =>
        await Client.SendAsync(CreateAuthenticatedRequest(HttpMethod.Get, url, viewer));

    private async Task<string> ReadQuote(string url, GeneralUser viewer)
    {
        var response = await Send(url, viewer);
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>
    /// The same post RoomPostPrivacyShould builds: one [private] block whose
    /// frozen snapshot resolves the addressed character to SecondUser.
    /// </summary>
    private Task<Guid> CreatePrivatePost() =>
        PostTestHelper.CreatePrivatePost(DatabaseFixture, PublicText, PrivateText);

}
