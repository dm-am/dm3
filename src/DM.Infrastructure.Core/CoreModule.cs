using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.S3;
using Autofac;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Search;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Authorization;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Correlation;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Search;
using DM.Infrastructure.Core.Storage;
using Microsoft.Extensions.Options;
using OpenSearch.Client;

namespace DM.Infrastructure.Core;

/// <inheritdoc />
public class CoreModule : Module
{
    /// <inheritdoc />
    protected override void Load(ContainerBuilder builder)
    {
        builder.RegisterDefaultTypes();

        builder.RegisterType<IntentionManager>()
            .As<IIntentionManager>()
            .InstancePerLifetimeScope();

        builder.RegisterType<CorrelationTokenProvider>()
            .AsSelf()
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        builder.RegisterType<CursorService>()
            .As<ICursorService>()
            .SingleInstance();

        // S3 Client registration (uses IAmazonS3ClientProvider implementations)
        builder.Register(ctx =>
            {
                var amazonS3ClientProviders = ctx
                    .Resolve<IEnumerable<IAmazonS3ClientProvider>>();
                return amazonS3ClientProviders
                    .First(p => p.CanBeUsed())
                    .GetClient();
            })
            .AsSelf()
            .As<IAmazonS3>()
            .SingleInstance();

        // OpenSearch Client registration
        builder.Register(x =>
            {
                var configuration = x.Resolve<IOptions<SearchEngineConfiguration>>().Value;
                return new ConnectionSettings(new Uri(configuration.Endpoint))
                    .ServerCertificateValidationCallback((sender, cert, chain, errs) => true)
                    .BasicAuthentication(configuration.Username, configuration.Password)
                    .DefaultMappingFor<SearchEntity>(m => m
                        .IndexName(SearchEngineConfiguration.IndexName));
            })
            .SingleInstance();

        builder.Register(x => new OpenSearchClient(x.Resolve<ConnectionSettings>()))
            .AsImplementedInterfaces()
            .InstancePerLifetimeScope();

        // Storage services
        builder.RegisterType<Uploader>()
            .As<IUploader>()
            .InstancePerLifetimeScope();

        builder.RegisterType<NameGenerator>()
            .As<INameGenerator>()
            .InstancePerLifetimeScope();

        builder.RegisterType<UploadFactory>()
            .As<IUploadFactory>()
            .InstancePerLifetimeScope();

        builder.RegisterType<PublicImageService>()
            .As<IPublicImageService>()
            .As<IObsoleteUploadsCleanup>()
            .InstancePerLifetimeScope();

        builder.RegisterType<ImageProcessingService>()
            .As<IImageProcessingService>()
            .InstancePerLifetimeScope();

        // Search services
        builder.RegisterType<SearchService>()
            .As<ISearchService>()
            .InstancePerLifetimeScope();

        builder.RegisterType<SearchEngineRepository>()
            .As<ISearchEngineRepository>()
            .InstancePerLifetimeScope();

        base.Load(builder);
    }
}