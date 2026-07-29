using Autofac;
using DM.Domain.Core.Authorization;
using DM.Domain.Moderation.Authorization;

namespace DM.Domain.Moderation;

/// <summary>
/// Lifetimes this module's types need beyond the assembly scan's default.
/// </summary>
/// <remarks>
/// Same reason as AccountModule: the resolver is internal, so the host that
/// wanted it per scope had to name it by namespace string. The requirement now
/// lives in the assembly that owns the type.
/// </remarks>
public class ModerationModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder) =>
        builder.RegisterType<ModerationIntentionResolver>()
            .As<IIntentionResolver<ModerationIntention>>()
            .InstancePerLifetimeScope();
}
