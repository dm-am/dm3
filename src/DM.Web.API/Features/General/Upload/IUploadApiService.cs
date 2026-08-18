using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Features.General.Upload;

/// <summary>
/// API service for file upload management.
/// Direct upload only: the server validates, processes (for images — generates
/// thumbnails) and puts to S3 atomically. No presigned URLs.
/// </summary>
public interface IUploadApiService
{
    /// <summary>List uploads (current user; specific user — moderator+; all — admin).</summary>
    Task<(IEnumerable<Shared.Dto.Upload> Uploads, PagingInfo Paging)> GetUploads(
        UploadsQuery query, string? username, bool all);

    /// <summary>Get upload by ID (owner or moderator+).</summary>
    Task<Shared.Dto.Upload> GetUpload(Guid id);

    /// <summary>
    /// The file's bytes, for a caller entitled to them.
    /// </summary>
    /// <remarks>
    /// Answers <see cref="DM.Domain.Core.Exceptions.HttpException"/> with 404
    /// whenever the caller may not have the file, rather than 403: the closed
    /// prefixes exist so that an outsider learns nothing about what a private game
    /// contains, and "you may not have this" is something.
    /// </remarks>
    /// <param name="id">Upload identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UploadContent> GetUploadContent(Guid id, CancellationToken ct = default);

    /// <summary>Soft-delete upload (owner, moderator+, or an editor of the entity it hangs on).</summary>
    Task DeleteUpload(Guid id);

    /// <summary>
    /// Upload file directly with server-side processing.
    /// For images — magic-byte validation, EXIF strip, WebP thumbnail generation,
    /// atomic batch S3 PUT, extension normalization by the validated content-type.
    /// </summary>
    Task<Shared.Dto.Upload> DirectUpload(IFormFile file, UploadType type, Guid? targetId);
}

/// <summary>
/// A file being handed to a caller who is entitled to it.
/// </summary>
/// <param name="Content">
/// The bytes, streamed straight from the object store. The response owns it and
/// disposes it.
/// </param>
/// <param name="ContentType">MIME type the file is stored as.</param>
/// <param name="Length">Size in bytes, or null when the store did not report one.</param>
/// <param name="FileName">
/// Display name, as sanitized when the file was accepted — never the raw name the
/// uploader sent.
/// </param>
public sealed record UploadContent(
    Stream Content,
    string ContentType,
    long? Length,
    string FileName);
