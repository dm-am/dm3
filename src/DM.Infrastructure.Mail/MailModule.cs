using Autofac;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Mail;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Mail.Rendering;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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

        // One sender per scope, for the same reason as InvokedEventProducer: the
        // channel is taken from the pool on the first send and returned only in
        // Dispose, and under InstancePerDependency there was nobody to return it.
        builder.RegisterType<MailSender>()
            .As<IMailSender>()
            .InstancePerLifetimeScope();

        // Register HtmlRenderer for Blazor template rendering
        builder.Register(ctx => EmailRendering.CreateHtmlRenderer(
                ctx.Resolve<ILoggerFactory>(),
                ctx.Resolve<IOptions<SiteAddressConfiguration>>().Value))
            .AsSelf()
            .SingleInstance();

        // One renderer per process. The template map is built by reflection over the
        // assembly and does not change, and the HtmlRenderer under it is a singleton
        // anyway: its own Dispatcher, which every call goes through, serializes the
        // renders.
        builder.RegisterType<TemplateRenderer>()
            .As<ITemplateRenderer>()
            .SingleInstance();

        base.Load(builder);
    }
}
