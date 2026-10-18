using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AwesomeAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace DM.Web.API.IntegrationTests.Controllers.Moderation;

/// <summary>
/// The description of a game tag is the hint surface: saved with the names of
/// its tags in lower case, refused with a hidden block in it, and given to the
/// card of the tag rendered by the server, next to the text the edit window is
/// filled with.
/// </summary>
public class TagDescriptionShould : IntegrationTestBase
{
    /// <summary>A group of the migration's catalogue, "Деликатный контент".</summary>
    private static readonly Guid GroupId = Guid.Parse("00000000-0000-0000-0005-000000000008");

    public TagDescriptionShould(DatabaseFixture databaseFixture) : base(databaseFixture)
    {
    }

    [Fact]
    public async Task SaveTheNamesInLowerCase_AndRenderTheHintForTheCard()
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
                .Be("Игра затрагивает [tip text=\"Политика & религия\"]спорные[/tip] темы");
            tag.GetProperty("descriptionHtml").GetString().Should()
                .Be("Игра затрагивает <span class=\"bb-tip\" tabindex=\"0\" " +
                    "data-tip-text=\"Политика &amp; религия\">спорные</span> темы");

            var id = tag.GetProperty("id").GetGuid();
            var updated = await Send(HttpMethod.Put, $"/v1/moderation/tags/{id}", new
            {
                groupId = GroupId,
                title,
                description = "[Tip Img=\"/images/erp-tooltip.gif\"]эротические сцены[/Tip] и [TIP]слово[/TIP]",
                sortOrder = 0
            });

            updated.Status.Should().Be(HttpStatusCode.OK, "body was: {0}", updated.Body);
            var edited = JsonDocument.Parse(updated.Body).RootElement;
            // A [tip] that shows nothing is text on the page, and text keeps its case.
            edited.GetProperty("description").GetString().Should()
                .Be("[Tip Img=\"/images/erp-tooltip.gif\"]эротические сцены[/Tip] и [TIP]слово[/TIP]");
            edited.GetProperty("descriptionHtml").GetString().Should()
                .Be("<span class=\"bb-tip\" tabindex=\"0\" data-tip-img=\"/images/erp-tooltip.gif\">" +
                    "эротические сцены</span> и [TIP]слово[/TIP]");
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Tags.Where(tag => tag.Title == title).ExecuteDeleteAsync();
        }
    }

    [Fact]
    public async Task RefuseAHiddenBlock_NamingWhereItIs()
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
                .StartWith("Скрытый текст работает только в игровом тексте поста: [private], символ 9.");
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Tags.Where(tag => tag.Title == title).ExecuteDeleteAsync();
        }
    }

    /// <summary>
    /// A description is drawn in a tooltip and nowhere else, and the catalogue
    /// that carries it is cached for everyone: it is rendered for display
    /// whatever rendering audience the request names.
    /// </summary>
    [Theory]
    [InlineData("plain_text")]
    [InlineData("embed_safe")]
    [InlineData("author_edit")]
    public async Task RenderTheHintForDisplay_WhateverAudienceIsAsked(string audience)
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
            JsonDocument.Parse(created.Body).RootElement.GetProperty("descriptionHtml").GetString().Should()
                .Be("a &lt; b <span class=\"bb-tip\" tabindex=\"0\" data-tip-text=\"x\">слово</span>");
        }
        finally
        {
            await using var db = DatabaseFixture.CreateDbContext();
            await db.Tags.Where(tag => tag.Title == title).ExecuteDeleteAsync();
        }
    }

    private async Task<(HttpStatusCode Status, string Body)> Send(HttpMethod method, string url, object body,
        string? audience = null)
    {
        var request = CreateAuthenticatedRequest(method, url, CustomWebApplicationFactory.CreateSeniorModeratorUser());
        if (audience != null) request.Headers.Add("X-Dm-Audience", audience);
        request.Content = JsonContent.Create(body);
        var response = await Client.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }
}
