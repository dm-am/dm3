using Autofac;
using DM.Infrastructure.Core.Extensions;
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
        base.Load(builder);
    }
}
