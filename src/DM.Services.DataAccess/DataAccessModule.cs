using System;
using Autofac;
using DM.Services.Core.Configuration;
using DM.Services.DataAccess.MongoIntegration;
using DM.Services.DataAccess.RelationalStorage;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;
using MongoDB.Driver.Core.Extensions.DiagnosticSources;

namespace DM.Services.DataAccess;

/// <inheritdoc />
public class DataAccessModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        // Configure GUID serialization - use Unspecified to handle both Legacy and Standard formats
        BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Unspecified));

        builder.Register(ctx =>
            {
                var connectionString = MongoUrl.Create(ctx.Resolve<IOptions<ConnectionStrings>>().Value.Mongo);
                var settings = MongoClientSettings.FromUrl(connectionString);
                // TODO: revert it back when the library providing it gets updated
                /*settings.ClusterConfigurator = cb => cb.Subscribe(
                    new DiagnosticsActivityEventSubscriber(new InstrumentationOptions { CaptureCommandText = true }));*/
                return new DmMongoClient(settings, connectionString);
            })
            .AsSelf()
            .AsImplementedInterfaces();

        builder.RegisterType<UpdateBuilderFactory>()
            .AsSelf()
            .AsImplementedInterfaces()
            .SingleInstance();

        base.Load(builder);
    }
}