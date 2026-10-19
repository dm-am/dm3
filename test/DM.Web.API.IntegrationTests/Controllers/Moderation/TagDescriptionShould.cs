using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// The description of a game tag is the hint surface: saved with the names of
/// its tags in lower case, refused with a hidden block in it, given to the card
/// of the tag rendered by the server and to the edit window as typed.
/// </summary>
public class TagDescriptionShould : IntegrationTestBase
{
    /// <summary>A group of the migration's catalogue, "Деликатный контент".</summary>
    private static readonly Guid GroupId = Guid.Parse("00000000-0000-0000-0005-000000000008");

    public TagDescriptionShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task SaveTheDescriptionAsTypedAndRenderTheHintForTheCard()
    {
        var title = $"Подсказка {Guid.NewGuid():N}"[..40];
        var created = await Send(HttpMethod.Post, "/v1/moderation/tags", new
        {
            groupId = GroupId,
            title,
            description = "Игра затрагивает [TIP TEXT=\"Политика & религия\"]спорные[/TIP] темы",
            sortOrder = 0
        });

        try
        {
            created.Status.Should().Be(HttpStatusCode.Created, "body was: {0}", created.Body);
            var tag = JsonDocument.Parse(created.Body).RootElement;
            tag.GetProperty("description").GetString().Should()
                .Be("Игра затрагивает <span class=\"bb-tip\" tabindex=\"0\" " +
                    "data-tip-text=\"Политика &amp; религия\">спорные</span> темы");

            var id = tag.GetProperty("id").GetGuid();
            var saved = await Send(HttpMethod.Get, $"/v1/moderation/tags/{id}", null, "author_edit");
            JsonDocument.Parse(saved.Body).RootElement.GetProperty("description").GetString().Should()
                .Be("Игра затрагивает [TIP TEXT=\"Политика & религия\"]спорные[/TIP] темы");

            var updated = await Send(HttpMethod.Put, $"/v1/moderation/tags/{id}", new
            {
                groupId = GroupId,
                title,
                description = "[Tip Img=\"/images/erp-tooltip.gif\"]эротические сцены[/Tip] и [TIP]слово[/TIP]",
                sortOrder = 0
            });

            updated.Status.Should().Be(HttpStatusCode.OK, "body was: {0}", updated.Body);
            JsonDocument.Parse(updated.Body).RootElement.GetProperty("description").GetString().Should()
                .Be("<span class=\"bb-tip\" tabindex=\"0\" data-tip-img=\"/images/erp-tooltip.gif\">" +
                    "эротические сцены</span> и [TIP]слово[/TIP]");

            // The edit window gets the text as it was saved.
            var forEditing = await Send(HttpMethod.Get, $"/v1/moderation/tags/{id}", null, "author_edit");
            forEditing.Status.Should().Be(HttpStatusCode.OK, "body was: {0}", forEditing.Body);
            JsonDocument.Parse(forEditing.Body).RootElement.GetProperty("description")
                .GetString().Should()
                .Be("[Tip Img=\"/images/erp-tooltip.gif\"]эротические сцены[/Tip] и [TIP]слово[/TIP]");
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Tags.Where(tag => tag.Title == title).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task RefuseAHiddenBlockNamingWhereItIs()
    {
        var title = $"Подсказка {Guid.NewGuid():N}"[..40];
        var created = await Send(HttpMethod.Post, "/v1/moderation/tags", new
        {
            groupId = GroupId,
            title,
            description = "Эротика [private]секрет[/private]",
            sortOrder = 0
        });

        try
        {
            created.Status.Should().Be(HttpStatusCode.BadRequest, "body was: {0}", created.Body);
            JsonDocument.Parse(created.Body).RootElement.GetProperty("errors").EnumerateObject()
                .Where(field => field.Name.Equals("description", StringComparison.OrdinalIgnoreCase))
                .SelectMany(field => field.Value.EnumerateArray().Select(message => message.GetString()))
                .Should().ContainSingle().Which.Should()
                .StartWith("Скрытый блок работает только в игровых постах: [private].");
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Tags.Where(tag => tag.Title == title).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// The edit window asks with the author_edit audience and gets the text as
    /// typed; asked otherwise, the description is the card's render.
    /// </summary>
    [Theory]
    [InlineData(null, "a &lt; b <span class=\"bb-tip\" tabindex=\"0\" data-tip-text=\"x\">слово</span>")]
    [InlineData("author_edit", "a < b [tip text=\"x\"]слово[/tip]")]
    public async Task GiveTheEditWindowTheSourceAndTheCardTheRender(string? audience, string expected)
    {
        var title = $"Подсказка {Guid.NewGuid():N}"[..40];
        var created = await Send(HttpMethod.Post, "/v1/moderation/tags", new
        {
            groupId = GroupId,
            title,
            description = "a < b [tip text=\"x\"]слово[/tip]",
            sortOrder = 0
        }, audience);

        try
        {
            created.Status.Should().Be(HttpStatusCode.Created, "body was: {0}", created.Body);
            JsonDocument.Parse(created.Body).RootElement.GetProperty("description").GetString().Should()
                .Be(expected);
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Tags.Where(tag => tag.Title == title).ExecuteDeleteAsync();
        }
    }

    private async Task<(HttpStatusCode Status, string Body)> Send(HttpMethod method, string url, object? body,
        string? audience = null)
    {
        var request = CreateAuthenticatedRequest(method, url, CustomWebApplicationFactory.CreateSeniorModeratorUser());
        if (audience != null) request.Headers.Add("X-Dm-Audience", audience);
        if (body != null) request.Content = JsonContent.Create(body);
        var response = await Client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
