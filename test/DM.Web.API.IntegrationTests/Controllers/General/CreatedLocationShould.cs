using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DM.Infrastructure.Persistence;
using DM.Web.API.IntegrationTests.Helpers;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <remarks>
/// API_DESIGN states the rule. The declaration is checked against the code: an action calling CreatedAtRoute
/// or its siblings sends a Location and is not marked, one answering 201 any other way sends none and is. The
/// wire is checked on the converted endpoints.
/// </remarks>
public class CreatedLocationShould : IntegrationTestBase
{
    public CreatedLocationShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task BeDeclaredByEveryCreatedResponseThatSendsIt()
    {
        var marked = MarkedRoutes();
        marked.Should().NotBeEmpty("the actions that answer 201 without a Location carry a marker");

        var wrong = new List<string>();
        var examined = 0;

        foreach (var operation in await SwaggerDocuments.OperationsAsync(Client))
        {
            if (operation.Value.ValueKind != JsonValueKind.Object ||
                !operation.Value.TryGetProperty("responses", out var responses) ||
                !responses.TryGetProperty("201", out var created))
            {
                continue;
            }

            examined++;
            var address = $"{operation.Address}";
            var declares = created.TryGetProperty("headers", out var headers) &&
                           headers.TryGetProperty("Location", out _);

            if (marked.Contains(address) && declares)
            {
                wrong.Add($"{address} declares a Location it never sends");
            }
            else if (!marked.Contains(address) && !declares)
            {
                wrong.Add($"{address} sends a Location the document does not name");
            }
        }

        examined.Should().BeGreaterThan(0, "the API creates resources");
        wrong.Should().BeEmpty(
            "a consumer reading the document has no other way to learn the address of what " +
            "it just created, and a header promised over an answer that never carries it is " +
            "worse than the silence it replaced: silence is visible");
    }

    // Read off the registered routes, the set the filter skips.
    private HashSet<string> MarkedRoutes() => DatabaseFixture.Factory.Services
        .GetRequiredService<IActionDescriptorCollectionProvider>()
        .ActionDescriptors.Items
        .OfType<ControllerActionDescriptor>()
        .Where(action => action.MethodInfo
            .GetCustomAttributes(inherit: false)
            .Any(attribute => attribute.GetType().Name == "CreatedWithoutLocationAttribute"))
        .Select(action =>
            $"{action.ActionConstraints?.OfType<HttpMethodActionConstraint>()
                .SelectMany(constraint => constraint.HttpMethods).FirstOrDefault() ?? "POST"} " +
            $"/{action.AttributeRouteInfo!.Template}")
        .ToHashSet(StringComparer.Ordinal);

    // Read from the source: the attributes of an action answering through StatusCode and through
    // CreatedAtRoute are the same.
    [Fact]
    public void MarkEveryCreatedResponseThatCarriesNoLocation()
    {
        var setters = new[] { "CreatedAtRoute(", "CreatedAtAction(", "return Created(" };
        var wrong = new List<string>();
        var actions = 0;

        foreach (var file in Directory.EnumerateFiles(
                     Path.Combine(ApiSourceRoot, "Features"), "*Controller.cs", SearchOption.AllDirectories))
        {
            foreach (var action in Actions(File.ReadAllText(file)))
            {
                if (!action.Body.Contains("Status201Created", StringComparison.Ordinal))
                {
                    continue;
                }

                actions++;
                var sends = setters.Any(setter => action.Body.Contains(setter, StringComparison.Ordinal));
                var marked = action.Body.Contains("[CreatedWithoutLocation]", StringComparison.Ordinal);

                if (sends && marked)
                {
                    wrong.Add($"{Path.GetFileName(file)}.{action.Name} sets a Location and is marked as sending none");
                }
                else if (!sends && !marked)
                {
                    wrong.Add($"{Path.GetFileName(file)}.{action.Name} answers 201 without a Location and is not marked");
                }
            }
        }

        actions.Should().BePositive("the API creates resources, and a walk finding none checks nothing");
        wrong.Should().BeEmpty(
            "the document declares Location over every 201 it is not told about, so a marker " +
            "out of step with the code is a header a consumer follows and never receives");
    }

    // A like is not a resource, so liking answers 200 and sets no Location.
    [Theory]
    [InlineData("topic")]
    [InlineData("comment")]
    public async Task NotBeSetWhereNothingAddressableWasCreated(string liked)
    {
        var target = liked == "topic" ? TestConstants.SecondTopicId : await SeedCommentAsync();
        var address = liked == "topic" ? $"/v1/topics/{target}/likes" : $"/v1/forum/comments/{target}/likes";

        try
        {
            using var request = CreateAuthenticatedRequest(HttpMethod.Post, address);
            var response = await Client.SendAsync(request);

            response.StatusCode.Should().Be(HttpStatusCode.OK,
                "a like has no address of its own, so 201 has nothing to point at; body was: {0}",
                await response.Content.ReadAsStringAsync());
            response.Headers.Location.Should().BeNull(
                "the header pointed at the whole topic, so a consumer following it to read what it " +
                "had created redrew the page instead of moving a counter");
        }
        finally
        {
            await DropAsync(target, ownComment: liked == "comment");
        }
    }

    // A comment of the second user on the seeded topic, written past the counters.
    private async Task<Guid> SeedCommentAsync()
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        var commentId = Guid.NewGuid();
        dbContext.Comments.Add(new DbComment
        {
            CommentId = commentId,
            EntityId = TestConstants.TestTopicId,
            AuthorId = TestConstants.SecondUserId,
            CreatedUtc = DateTimeOffset.UtcNow,
            Text = "A comment to like",
        });
        await dbContext.SaveChangesAsync();

        return commentId;
    }

    private async Task DropAsync(Guid target, bool ownComment)
    {
        using var scope = DatabaseFixture.Factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<DmDbContext>();

        await dbContext.Database.ExecuteSqlRawAsync(
            """DELETE FROM "Likes" WHERE "EntityId" = {0} AND "UserId" = {1}""",
            target, TestConstants.TestUserId);
        if (ownComment)
        {
            await dbContext.Database.ExecuteSqlRawAsync(
                """DELETE FROM "Comments" WHERE "CommentId" = {0}""", target);
        }
    }

    // The attribute run is walked backwards from the signature: the marker is above it, the call
    // below.
    private static IEnumerable<(string Name, string Body)> Actions(string source)
    {
        var signature = new Regex(@"^\s+public\s+(?:async\s+)?[\w<>,\[\]\? ]+\s+(\w+)\s*\(");
        var lines = source.Split('\n');

        var starts = new List<(int Line, string Name)>();
        for (var index = 0; index < lines.Length; index++)
        {
            var match = signature.Match(lines[index]);
            if (match.Success)
            {
                starts.Add((index, match.Groups[1].Value));
            }
        }

        // Where the attributes and documentation of an action begin.
        int Head(int at)
        {
            var head = at;
            while (head > 0)
            {
                var line = lines[head - 1].Trim();
                if (line.StartsWith('[') || line.StartsWith("///") || line.EndsWith(')') ||
                    line.EndsWith(']'))
                {
                    head--;
                    continue;
                }

                break;
            }

            return head;
        }

        for (var index = 0; index < starts.Count; index++)
        {
            var from = Head(starts[index].Line);
            var to = index + 1 < starts.Count ? Head(starts[index + 1].Line) : lines.Length;
            yield return (starts[index].Name, string.Join('\n', lines[from..to]));
        }
    }

    // The API project, found from the test binary rather than copied.
    private static string ApiSourceRoot =>
        Path.Combine(DM.Testing.RepositoryLayout.Root, "src", "DM.Web.API");
}
