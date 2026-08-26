using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using DM.Web.API.Swagger;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.General;

/// <summary>
/// A 201 declares its Location, and the Location leads to what was created.
/// </summary>
/// <remarks>
/// API_DESIGN states it: the header points at the created resource and at
/// nothing else, and where a sub-resource has no address of its own the answer
/// is not a 201. Neither half held. Sixty-two operations declared a 201 and none
/// declared a header, so the address was on the wire and absent from the
/// contract; and nine of them pointed somewhere else — a like answered with the
/// address of the whole topic, so a consumer following the header to read what
/// it had created redrew the page instead of moving a counter, and a new rubric
/// answered with an empty Location, which resolves to the collection.
///
/// What is checked here is the declaration, because that is the part a consumer
/// reads — and the declaration is checked against the code, not against itself.
/// Reading the header back out of the published document proved nothing at all:
/// the filter writes it there unconditionally, so restoring the finding's own
/// defect (a like answering CreatedAtRoute(GetPublication)) left the walk green.
/// The pairing below is what carries it: an action that calls CreatedAtRoute or
/// its siblings sends a Location and may not be marked, an action that answers
/// 201 any other way sends none and has to be, and either way round the document
/// then says what the wire does.
///
/// The wire is checked on the endpoints the fix converted, so that a 201 with a
/// borrowed address cannot come back on them under any declaration.
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

        foreach (var group in SwaggerExtensions.ApiGroups)
        {
            var response = await Client.GetAsync($"/swagger/{group}/swagger.json");
            response.StatusCode.Should().Be(HttpStatusCode.OK);

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            foreach (var path in document.RootElement.GetProperty("paths").EnumerateObject())
            {
                foreach (var operation in path.Value.EnumerateObject())
                {
                    if (operation.Value.ValueKind != JsonValueKind.Object ||
                        !operation.Value.TryGetProperty("responses", out var responses) ||
                        !responses.TryGetProperty("201", out var created))
                    {
                        continue;
                    }

                    examined++;
                    var address = $"{operation.Name.ToUpperInvariant()} {path.Name}";
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
            }
        }

        examined.Should().BeGreaterThan(0, "the API creates resources");
        wrong.Should().BeEmpty(
            "a consumer reading the document has no other way to learn the address of what " +
            "it just created, and a header promised over an answer that never carries it is " +
            "worse than the silence it replaced: silence is visible");
    }

    /// <summary>
    /// The addresses of the operations marked as sending no Location.
    /// </summary>
    /// <remarks>
    /// Read off the routes the host registered, so the set is the same one the
    /// filter skips rather than a second list to keep in step with it.
    /// </remarks>
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

    /// <summary>
    /// The marker sits on exactly the actions that send no Location.
    /// </summary>
    /// <remarks>
    /// The two halves are read out of the source of each action: whether it calls
    /// one of the framework methods that set the header, and whether it carries
    /// CreatedWithoutLocation. Read from the source because the answer is in the
    /// body of the method and nowhere else — the attributes of an action that
    /// answers 201 through StatusCode are identical to those of one that answers
    /// through CreatedAtRoute, which is how eighteen operations came to promise a
    /// header they have never sent.
    /// </remarks>
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

        actions.Should().BeGreaterThan(40, "the API creates resources, and a walk finding few checks little");
        wrong.Should().BeEmpty(
            "the document declares Location over every 201 it is not told about, so a marker " +
            "out of step with the code is a header a consumer follows and never receives");
    }

    /// <summary>
    /// A like is not a resource, so liking answers 200 and sets no Location.
    /// </summary>
    [Theory]
    [InlineData("/v1/topics/{0}/likes")]
    [InlineData("/v1/forum/comments/{1}/likes")]
    public async Task NotBeSetWhereNothingAddressableWasCreated(string route)
    {
        var address = string.Format(
            route, TestConstants.SecondTopicId, TestConstants.TestCommentId);

        using var request = CreateAuthenticatedRequest(HttpMethod.Post, address);
        var response = await Client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.Created,
            "a like has no address of its own, so 201 has nothing to point at");
        response.Headers.Location.Should().BeNull(
            "the header pointed at the whole topic, so a consumer following it to read what it " +
            "had created redrew the page instead of moving a counter");
    }

    /// <summary>
    /// Actions of one controller: the name, and its own attributes, signature
    /// and body — nothing of its neighbour's.
    /// </summary>
    /// <remarks>
    /// Line based, and the attribute run is walked backwards from the signature,
    /// because the two things this compares live on opposite sides of it: the
    /// marker is an attribute and the call that sets the header is a statement.
    /// Splitting on the signature alone puts the attributes of one action in the
    /// block of the one above it, which reports every reader of the file.
    /// </remarks>
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

    /// <summary>The API project, found from the test binary rather than copied.</summary>
    private static string ApiSourceRoot =>
        Path.Combine(DM.Testing.RepositoryLayout.Root, "src", "DM.Web.API");
}
