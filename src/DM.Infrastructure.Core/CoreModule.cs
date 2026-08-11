using System;
using System.Collections.Generic;
using System.Linq;
using Amazon.S3;
using Autofac;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Identity;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Authorization;
using DM.Infrastructure.Core.Clock;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Correlation;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Core.Identifiers;
using DM.Infrastructure.Core.Paging;
using DM.Infrastructure.Core.Randomness;
using DM.Infrastructure.Core.Storage;
using Microsoft.Extensions.Options;

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

        // The single avatar image processing service: resize+crop
        // into Medium/Small WebP thumbnails, EXIF strip of the original, decompression-
        // bomb protection, magic-byte content-type detection, atomic
        // S3 batch upload with compensating cleanup on failure.
        builder.RegisterType<ImageProcessingService>()
            .As<IImageProcessingService>()
            .InstancePerLifetimeScope();

        // GC of obsolete uploads (UploadGarbageCollector) lives in
        // Infrastructure.Persistence (needs DmDbContext) and is registered
        // there via PersistenceModule.

        // PublicId encoding/decoding service (stateless, singleton)
        builder.RegisterType<PublicIdService>()
            .As<IPublicIdService>()
            .SingleInstance();

        base.Load(builder);
    }
}
