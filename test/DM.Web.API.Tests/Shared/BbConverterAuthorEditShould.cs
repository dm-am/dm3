using System;
using System.Linq;
using System.Text.Json;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Core.Parsing;
using DM.Testing.Dsl;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Configuration;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// The author who asks for their own text to edit it gets the BBCode as it was
/// saved, and nobody else gets it.
/// </summary>
/// <remarks>
/// The editor works on BBCode (BBCODE_RENDERING.md, "Исходник на правку"): HTML
/// handed to it would have to be turned back, and the way back loses what the
/// author typed.
/// </remarks>
public class BbConverterAuthorEditShould
{
    private static readonly Guid AuthorId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid StrangerId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    /// <summary>Every kind of text that is not a plain word: markup, a hidden block, a dash, a CR LF.</summary>
    private const string Source =
        "[b]раз[/b] -- два\r\n[private=\"Вася\"]тайна[/private]\n[quote]три[/quote] a < b & [noparse][i][/noparse]";

    [Fact]
    public void GiveTheAuthorTheSourceAsTyped() =>
        Serialize(Source, viewer: AuthorId).Should().Be(Source);

    [Fact]
    public void GiveAnyoneElseTheRenderInstead()
    {
        var shown = Serialize(Source, viewer: StrangerId);

        shown.Should().Contain("<strong>раз</strong>").And.NotContain("тайна").And.NotContain("[b]");
    }

    /// <summary>
    /// A text nested too deep to render is the author's to repair, so it comes
    /// back to them as typed while the page shows nothing.
    /// </summary>
    [Fact]
    public void GiveTheAuthorEvenATextTooDeepToRender()
    {
        var deep = string.Concat(Enumerable.Repeat("[b]", 600)) + "текст";

        Serialize(deep, viewer: AuthorId).Should().Be(deep);
        Serialize(deep, viewer: StrangerId).Should().BeEmpty();
    }

    /// <summary>
    /// Whoever may edit the text gets it as its author does: the editor saves what
    /// it was handed. The check is asked of the request's own intention manager.
    /// </summary>
    [Fact]
    public void GiveAnEditorWhoIsNotTheAuthorTheSource()
    {
        const string plain = "[b]раз[/b] -- два";

        Serialize(plain, viewer: StrangerId, mayEdit: true).Should().Be(plain);
        Serialize(plain, viewer: StrangerId, mayEdit: false).Should().Contain("<strong>раз</strong>");
    }

    /// <summary>
    /// The edit right is the one question: whether an editor who is not the author
    /// reads every hidden block is that right's own (PostIntentionResolverShould), so
    /// the source goes exactly where the edit would be taken and nowhere else.
    /// </summary>
    [Fact]
    public void HandTheSourceOfAHiddenBlockExactlyWhereTheEditRightGoes()
    {
        Serialize(Source, viewer: StrangerId, mayEdit: true).Should().Be(Source);
        Serialize(Source, viewer: StrangerId, mayEdit: false).Should().NotContain("тайна");
    }

    /// <summary>The JSON string the converter writes for a post text by <see cref="AuthorId"/>.</summary>
    private static string Serialize(string bbCode, Guid viewer, bool? mayEdit = null)
    {
        var subject = new TestSubject { UserId = viewer, Role = UserRole.RegularUser, IsAuthenticated = true };
        var authorization = Substitute.For<IAuthorizationContextProvider>();
        authorization.CurrentSubject.Returns(subject);

        var intentions = Substitute.For<IIntentionManager>();
        var services = new ServiceCollection()
            .AddSingleton(authorization)
            .AddSingleton(intentions)
            .BuildServiceProvider();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Headers[BbAudienceHeader.HeaderName] = BbAudienceHeader.Serialize(RenderAudience.AuthorEdit);
        var options = new JsonOptions();
        options.Setup(new HttpContextAccessor { HttpContext = context }, new BbParserProvider());

        var text = new PostBbText
        {
            Value = bbCode,
            Context = new RenderContextEnvelope
            {
                Surface = BbSurface.GamePost,
                PostAuthorUserId = AuthorId,
                EditableBy = mayEdit is { } allowed
                    ? manager => manager == intentions && allowed
                    : null
            }
        };
        return JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(text, options.JsonSerializerOptions))!;
    }
}
