using Autofac;
using DM.Services.Core.Extensions;
using DM.Services.MessageQueuing.Outbox;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using System;

namespace DM.Services.MessageQueuing;

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

        builder.RegisterType<OutboxProcessor>()
            .As<IHostedService>()
            .SingleInstance();

        builder.RegisterDefaultTypes();
        base.Load(builder);
    }
}