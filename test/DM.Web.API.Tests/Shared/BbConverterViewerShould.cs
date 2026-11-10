using System.Text.Json;
using DM.Infrastructure.Core.Parsing;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Configuration;
using AwesomeAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DM.Web.API.Tests.Shared;

// The converter takes the viewer from the request scope, and a scope already gone renders for a
// guest.
public class BbConverterViewerShould
{
    [Fact]
    public void RenderForAGuestWhenTheRequestScopeIsDisposed()
    {
        var scope = new ServiceCollection().BuildServiceProvider().CreateScope();
        var disposed = scope.ServiceProvider;
        scope.Dispose();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = disposed } };
        var options = new JsonOptions();
        options.Setup(accessor, new BbParserProvider());

        var json = JsonSerializer.Serialize(new CommonBbText { Value = "[b]bold[/b]" }, options.JsonSerializerOptions);

        json.Should().Contain("bold", "a response serialized after its scope is still served");
    }
}
