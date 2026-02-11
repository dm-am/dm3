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
        // Configure GUID serialization for backward compatibility with existing data
        // Try to register only if not already registered (for test scenarios)
        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.CSharpLegacy));
        }
        catch (BsonSerializationException)
        {
            // Serializer already registered, ignore
        }

        builder.Register(ctx =>
            {
                var connectionString = MongoUrl.Create(ctx.Resolve<IOptions<ConnectionStrings>>().Value.Mongo);
                var settings = MongoClientSettings.FromUrl(connectionString);
                settings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
                settings.ConnectTimeout = TimeSpan.FromSeconds(10);
                settings.RetryWrites = true;
                settings.RetryReads = true;
                settings.ClusterConfigurator = cb => cb.Subscribe(
                    new DiagnosticsActivityEventSubscriber(new InstrumentationOptions { CaptureCommandText = true }));
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