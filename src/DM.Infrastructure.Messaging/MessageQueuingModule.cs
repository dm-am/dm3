using Autofac;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Messaging.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;

namespace DM.Infrastructure.Messaging;

/// <inheritdoc />
public class MessageQueuingModule : Module
{
    private readonly bool _enableOutboxProcessor;

    /// <summary>
    /// Creates a new MessageQueuingModule with default settings (OutboxProcessor disabled)
    /// </summary>
    public MessageQueuingModule() : this(enableOutboxProcessor: false)
    {
    }

    /// <summary>
    /// Creates a new MessageQueuingModule
    /// </summary>
    /// <param name="enableOutboxProcessor">Whether to enable the outbox processor (requires DmDbContext)</param>
    public MessageQueuingModule(bool enableOutboxProcessor)
    {
        _enableOutboxProcessor = enableOutboxProcessor;
    }

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

        if (_enableOutboxProcessor)
        {
            builder.RegisterType<OutboxProcessor>()
                .As<IHostedService>()
                .SingleInstance();
        }

        builder.RegisterDefaultTypes();
        base.Load(builder);
    }
}