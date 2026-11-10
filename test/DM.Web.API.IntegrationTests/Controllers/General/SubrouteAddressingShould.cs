using System.Text.RegularExpressions;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;

namespace DM.Web.API.IntegrationTests.Controllers.General;

// The public id is resolved in three places that disagree, so the table holds what each subroute
// answers for every caller; read off the registered routes, so a new subroute without a row fails.
public class SubrouteAddressingShould : IntegrationTestBase
{
    private const string UnknownPublicId = "zzzzz";

    private static readonly IReadOnlyDictionary<string, string> Matrix = new Dictionary<string, string>
    {
        ["v1/games/{id}"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/blacklist"] = "200 200 | 404 404 | 403 403 | 401 401",
        ["v1/games/{id}/characters"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/chat-rooms"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/comments"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/comments/first-unread"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/details"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/invitations"] = "200 200 | 404 404 | 403 403 | 401 401",
        ["v1/games/{id}/notepad"] = "200 200 | 404 404 | 403 403 | 401 401",
        ["v1/games/{id}/posts/first-unread"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/readers"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/reviews"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/rooms"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/users"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/games/{id}/users/assistants"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/blogs/{id}"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/blogs/{id}/blacklist"] = "200 200 | 404 404 | 403 403 | 401 401",
        ["v1/blogs/{id}/comments"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/blogs/{id}/invitations"] = "200 200 | 404 404 | 403 403 | 401 401",
        ["v1/blogs/{blogId}/notepad"] = "200 200 | 404 404 | 403 403 | 401 401",
        ["v1/blogs/{blogId}/publications"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/blogs/{id}/readers"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/blogs/{id}/users"] = "200 200 | 404 404 | 200 200 | 200 200",
        ["v1/blogs/{id}/users/assistants"] = "200 200 | 404 404 | 200 200 | 200 200",
        // A chat is not there for anybody outside it, signed in or not.
        ["v1/chats/{id}"] = "200 200 | 404 404 | 404 404 | 401 401",
        ["v1/chats/{id}/messages"] = "200 200 | 404 404 | 404 404 | 401 401",
    };

    // Subroutes that address a second resource inside the first: their answer is about the entry or
    // the review they name, which the table above does not.
    private static readonly string[] AddressSomethingInside =
    [
        "v1/games/{id}/notepad/{entryId:guid}",
        "v1/games/{id}/reviews/{reviewId:guid}",
        "v1/blogs/{blogId}/notepad/{entryId:guid}",
    ];

    private static readonly Regex Owner = new(@"^(v1/(?:games|blogs|chats)/)\{[^}]+\}", RegexOptions.Compiled);

    public SubrouteAddressingShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    public static TheoryData<string> Templates => new(Matrix.Keys);

    [Fact]
    public void CoverEveryGetSubrouteOfAGameABlogAndAChat()
    {
        var registered = DatabaseFixture.Factory.Services
            .GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items
            .Where(action => action.ActionConstraints?
                .OfType<HttpMethodActionConstraint>()
                .Any(constraint => constraint.HttpMethods.Contains("GET")) == true)
            .Select(action => action.AttributeRouteInfo?.Template)
            .Where(template => template is not null && Owner.IsMatch(template))
            .Select(template => template!)
            .Distinct()
            .ToList();

        registered.Should().NotBeEmpty("the host registers the subroutes of games, blogs and chats");
        registered.Should().BeEquivalentTo(Matrix.Keys.Concat(AddressSomethingInside),
            "a subroute missing from the table is one whose answers nothing holds while the " +
            "resolution of its identifier moves, and a row whose route is gone describes nothing");
    }

    [Theory]
    [MemberData(nameof(Templates))]
    public async Task AnswerAsItDoesByEitherIdentifier(string template)
    {
        var (identifier, publicId) = template switch
        {
            _ when template.StartsWith("v1/games/", StringComparison.Ordinal) =>
                (TestConstants.TestGameId.ToString(), TestConstants.TestGamePublicId),
            _ when template.StartsWith("v1/blogs/", StringComparison.Ordinal) =>
                (TestConstants.TestBlogId.ToString(), TestConstants.TestBlogPublicId),
            _ => (TestConstants.TestChatId.ToString(), TestConstants.TestChatPublicId),
        };

        // The test user masters the game, writes the blog and talks in the chat.
        var owner = CustomWebApplicationFactory.CreateTestUser();
        var stranger = await StrangerAsync();

        var answers = string.Join(" | ",
            $"{await Code(template, identifier, owner)} {await Code(template, publicId, owner)}",
            $"{await Code(template, UnknownPublicId, owner)} {await Code(template, Guid.NewGuid().ToString(), owner)}",
            $"{await Code(template, identifier, stranger)} {await Code(template, publicId, stranger)}",
            $"{await Code(template, identifier, null)} {await Code(template, publicId, null)}");

        answers.Should().Be(Matrix[template],
            "owner by identifier and by public id | unknown public id and identifier | " +
            "a reader who takes no part | a guest");
    }

    private async Task<int> Code(string template, string id, GeneralUser? user)
    {
        var path = "/" + Owner.Replace(template, match => match.Groups[1].Value + id);

        using var request = user is null
            ? new HttpRequestMessage(HttpMethod.Get, path)
            : CreateAuthenticatedRequest(HttpMethod.Get, path, user);
        using var response = await Client.SendAsync(request);

        return (int)response.StatusCode;
    }

    private async Task<GeneralUser> StrangerAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var userId = Guid.NewGuid();
        // Username is varchar(20) and uniquely indexed.
        var username = $"str{userId:N}"[..20];
        dbContext.Users.Add(new DbUser
        {
            UserId = userId,
            Username = username,
            Email = $"{userId:N}@example.com",
            PasswordHash = "hash",
            Salt = "salt",
        });
        await dbContext.SaveChangesAsync();

        return new GeneralUser { UserId = userId, Username = username, Role = UserRole.RegularUser };
    }
}
