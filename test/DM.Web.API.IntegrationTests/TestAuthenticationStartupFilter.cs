using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace DM.Web.API.IntegrationTests;

// Inserts TestAuthenticationMiddleware ahead of the pipeline: IStartupFilter runs before
// Startup.Configure().
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
