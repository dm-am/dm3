using Autofac;
using DM.Infrastructure.Core.Extensions;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace DM.Infrastructure.Mail;

/// <summary>
/// Autofac module for mail services registration
/// </summary>
public class MailModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterDefaultTypes();

        // Register HtmlRenderer for Blazor template rendering
        builder.Register(ctx =>
            {
                var loggerFactory = ctx.Resolve<ILoggerFactory>();
                var services = new ServiceCollection();
                services.AddLogging();
                var serviceProvider = services.BuildServiceProvider();
                return new HtmlRenderer(serviceProvider, loggerFactory);
            })
            .AsSelf()
            .SingleInstance();

        base.Load(builder);
    }
}
