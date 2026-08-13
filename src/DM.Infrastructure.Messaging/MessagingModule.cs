using Autofac;
using DM.Domain.Core.Events;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Messaging.GeneralBus;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;

namespace DM.Infrastructure.Messaging;

/// <inheritdoc />
public class MessagingModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(ctx => ctx.Resolve<IOptions<RabbitMqConfiguration>>().Value.CreateConnectionFactory())
            .As<IAsyncConnectionFactory>()
            .SingleInstance();

        builder.RegisterDefaultTypes();

        // One producer per scope rather than per resolution. The producer takes an
        // AMQP channel from the pool on its first send and returns it only in
        // Dispose, so under InstancePerDependency every resolution that published
        // anything left a channel open until the process restarted. Not
        // SingleInstance: one channel may not be published to from several threads,
        // and a singleton would share it across every concurrent request.
        builder.RegisterType<InvokedEventProducer>()
            .As<IEventProducer>()
            .InstancePerLifetimeScope();

        base.Load(builder);
    }
}
