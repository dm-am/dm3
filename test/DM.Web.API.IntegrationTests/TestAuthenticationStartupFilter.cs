using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Startup filter that inserts TestAuthenticationMiddleware early in the pipeline.
/// IStartupFilter runs BEFORE Startup.Configure(), so our middleware will be added
/// before the normal AuthenticationMiddleware.
/// </summary>
public class TestAuthenticationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            // Add our test middleware first
            app.UseMiddleware<TestAuthenticationMiddleware>();

            // Then continue with the rest of the pipeline
            next(app);
        };
    }
}
