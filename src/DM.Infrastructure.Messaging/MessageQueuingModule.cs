using Autofac;
using DM.Domain.Core.Events;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Messaging.GeneralBus;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;

namespace DM.Infrastructure.Messaging;

/// <inheritdoc />
public class MessageQueuingModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        builder.Register(ctx =>
            {
                var parameters = ctx.Resolve<IOptions<RabbitMqConfiguration>>().Value;
                return new ConnectionFactory
                {
                    Endpoint = new AmqpTcpEndpoint(new Uri(parameters.Endpoint)),
                    UserName = parameters.Username,
                    Password = parameters.Password,
                    VirtualHost = parameters.VirtualHost,
                };
            })
            .As<IAsyncConnectionFactory>()
            .SingleInstance();

        builder.RegisterDefaultTypes();

        // Один продюсер на scope, а не на каждое разрешение. Продюсер берет
        // AMQP-канал из пула при первой отправке и возвращает его только в
        // Dispose, поэтому при InstancePerDependency каждое разрешение, что-то
        // опубликовавшее, оставляло канал открытым до перезапуска процесса. Не
        // SingleInstance: публиковать в один канал из нескольких потоков нельзя,
        // а singleton разделил бы его между всеми одновременными запросами.
        builder.RegisterType<InvokedEventProducer>()
            .As<IEventProducer>()
            .InstancePerLifetimeScope();

        base.Load(builder);
    }
}
