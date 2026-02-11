using Autofac;
using DM.Services.Mail.Rendering.Assets;
using DM.Services.Mail.Rendering.Rendering;
using Microsoft.AspNetCore.Components.Web;

namespace DM.Services.Mail.Rendering;

/// <inheritdoc />
public class RenderingModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterType<HtmlRenderer>()
            .AsSelf()
            .SingleInstance();

        builder.RegisterType<TemplateRenderer>()
            .AsImplementedInterfaces()
            .SingleInstance();

        builder.RegisterType<EmailAssetsProvider>()
            .AsImplementedInterfaces()
            .SingleInstance();

        base.Load(builder);
    }
}