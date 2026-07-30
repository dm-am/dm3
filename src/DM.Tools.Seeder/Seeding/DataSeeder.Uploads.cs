using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Account.Features.Security;
using DM.Domain.Community.Features.Polls;
using DM.Domain.Core.Dto;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using DM.Domain.Personal.Authorization;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Core.Storage;
using DM.Infrastructure.Persistence;
using DM.Infrastructure.Persistence.MongoIntegration;
using DM.Infrastructure.Persistence.Entities.Blog;
using DM.Infrastructure.Persistence.Entities.Forum;
using DM.Infrastructure.Persistence.Entities.Game.Characters;
using DM.Infrastructure.Persistence.Entities.Game.Links;
using DM.Infrastructure.Persistence.Entities.Game.Posts;
using DM.Infrastructure.Persistence.Entities.Messaging;
using DM.Infrastructure.Persistence.Entities.Moderation;
using DM.Infrastructure.Persistence.Entities.Personal.Notepads;
using DM.Infrastructure.Persistence.Entities.Shared;
using DM.Infrastructure.Persistence.Entities.Community;
using DM.Infrastructure.Persistence.Entities.Subscriptions;
using Microsoft.Extensions.Options;
using DbUser = DM.Infrastructure.Persistence.Entities.Account.User;
using DbGame = DM.Infrastructure.Persistence.Entities.Game.Game;
using DbAttributeSchema = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSchema;
using DbAttributeSpecification = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.AttributeSpecification;
using DbStringConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.StringAttributeConstraints;
using DbBbCodeConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.BbCodeAttributeConstraints;
using DbListConstraints = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeConstraints;
using DbListValueKind = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListValueKind;
using DbListAttributeValue = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.ListAttributeValue;
using DbCharacterAttribute = DM.Infrastructure.Persistence.Entities.Game.Characters.Attributes.CharacterAttribute;
using DbBlog = DM.Infrastructure.Persistence.Entities.Blog.Blog;
using DbComment = DM.Infrastructure.Persistence.Entities.Shared.Comment;
using DbUsernameHistory = DM.Infrastructure.Persistence.Entities.Account.UsernameHistory;
using DbUserContact = DM.Infrastructure.Persistence.Entities.Account.UserContact;
using Microsoft.EntityFrameworkCore;

namespace DM.Tools.Seeder.Seeding;

internal sealed partial class DataSeeder
{
    /// <summary>
    /// Runs a seed image through the real image pipeline:
    /// magic-byte → EXIF strip → resize → a single S3 PUT of the source object
    /// with Cache-Control: immutable. Returns an Upload entity ready
    /// for Add() into the DbContext.
    /// </summary>
    private async Task<DM.Infrastructure.Persistence.Entities.Shared.Upload> SeedAvatarFromBytesAsync(
        byte[] imageBytes,
        string declaredContentType,
        string sourceFileName,
        UploadType type,
        Guid uploadId,
        Guid userId,
        Guid? entityId,
        DateTimeOffset now)
    {
        // The same pipeline as user uploads: magic-byte
        // validation, EXIF strip, downscale to 1024 px. A single source file —
        // imgproxy makes thumbnails on the fly at serving time.
        ProcessedImage processed;
        await using (var input = new MemoryStream(imageBytes, writable: false))
        {
            processed = await _imageProcessing.ProcessAsync(input, declaredContentType);
        }

        var folder = type switch
        {
            UploadType.UserAvatar => "avatars",
            UploadType.CharacterAvatar => "characters",
            _ => "misc",
        };
        var objectKey = string.IsNullOrEmpty(_cdnConfig.Folder)
            ? $"{folder}/{userId:N}_{uploadId:N}{processed.Extension}"
            : $"{_cdnConfig.Folder}/{folder}/{userId:N}_{uploadId:N}{processed.Extension}";

        await PutSeedObjectAsync(objectKey, processed.Bytes, processed.ContentType);

        var upload = new DM.Infrastructure.Persistence.Entities.Shared.Upload
        {
            UploadId = uploadId,
            CreatedUtc = now,
            ConfirmedUtc = now,
            UserId = userId,
            Type = type,
            Status = UploadStatus.Confirmed,
            Original = true,
            ContentType = processed.ContentType,
            SizeBytes = processed.Bytes.LongLength,
            ObjectKey = objectKey,
            FilePath = BuildPublicUrl(objectKey),
            FileName = sourceFileName,
            IsRemoved = false,
        };
        switch (type)
        {
            case UploadType.UserAvatar: upload.TargetUserId = entityId; break;
            case UploadType.CharacterAvatar: upload.TargetCharacterId = entityId; break;
            case UploadType.PostAttachment: upload.TargetPostId = entityId; break;
        }
        return upload;
    }

    private async Task PutSeedObjectAsync(string objectKey, byte[] bytes, string contentType)
    {
        await _s3Client.PutObjectAsync(new Amazon.S3.Model.PutObjectRequest
        {
            BucketName = _cdnConfig.BucketName,
            Key = objectKey,
            InputStream = new MemoryStream(bytes, writable: false),
            ContentType = contentType,
            Headers = { CacheControl = "public, max-age=31536000, immutable" },
        });
    }

    private string BuildPublicUrl(string objectKey) =>
        new UriBuilder(new Uri(_cdnConfig.PublicUrl))
        {
            Path = $"{_cdnConfig.BucketName}/{objectKey}",
        }.ToString();

    /// <summary>
    /// Read UTF-8 text from an embedded assembly resource. Throws if
    /// no resource with that name is registered — that is a setup bug (a missing
    /// <c>&lt;EmbeddedResource&gt;</c> in the csproj); it must fail the seed
    /// loudly rather than silently writing an empty string into Info.
    /// </summary>
    private static async Task<string> LoadEmbeddedTextAsync(string resourceName)
    {
        var assembly = typeof(DataSeeder).Assembly;
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{resourceName}' not found — check DM.Tools.Seeder.csproj <EmbeddedResource> entry.");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    /// <summary>
    /// Read the bytes of an embedded seed avatar image resource. Throws
    /// <see cref="InvalidOperationException"/> if the resource is not embedded
    /// (deployment misconfig).
    /// </summary>
    private static byte[] ReadEmbeddedSeedBytes(string resourceName)
    {
        var assembly = typeof(DataSeeder).Assembly;
        using var resourceStream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource {resourceName} not found");

        using var ms = new MemoryStream();
        resourceStream.CopyTo(ms);
        return ms.ToArray();
    }
}
