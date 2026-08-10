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

        // Один отправитель на scope. Причина та же, что у InvokedEventProducer:
        // канал берется из пула при первой отправке и возвращается только в
        // Dispose, а при InstancePerDependency возвращать его было некому.
        builder.RegisterType<MailSender>()
            .As<IMailSender>()
            .InstancePerLifetimeScope();

        // Register HtmlRenderer for Blazor template rendering
        builder.Register(ctx => EmailRendering.CreateHtmlRenderer(
                ctx.Resolve<ILoggerFactory>(),
                ctx.Resolve<IOptions<SiteAddressConfiguration>>().Value))
            .AsSelf()
            .SingleInstance();

        // Один рендерер на процесс. Карта шаблонов строится рефлексией по сборке и
        // не меняется, а HtmlRenderer под ним и так синглтон: сериализацию рендеров
        // обеспечивает его собственный Dispatcher, через который идет каждый вызов.
        builder.RegisterType<TemplateRenderer>()
            .As<ITemplateRenderer>()
            .SingleInstance();

        base.Load(builder);
    }
}
