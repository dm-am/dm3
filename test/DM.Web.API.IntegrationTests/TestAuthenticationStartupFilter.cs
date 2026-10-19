using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DM.Web.API.IntegrationTests;

/// <summary>
/// Inserts TestAuthenticationMiddleware ahead of the pipeline: IStartupFilter runs before Startup.Configure().
/// </summary>
public class TestAuthenticationStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            app.UseMiddleware<TestAuthenticationMiddleware>();
            next(app);
        };
    }
}
