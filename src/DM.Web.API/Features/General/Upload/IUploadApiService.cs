using System;
using System.Collections.Generic;
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

    /// <summary>Soft-delete upload (owner or moderator+).</summary>
    Task DeleteUpload(Guid id);

    /// <summary>
    /// Upload file directly with server-side processing.
    /// For images — magic-byte validation, EXIF strip, WebP thumbnail generation,
    /// atomic batch S3 PUT, extension normalization by the validated content-type.
    /// </summary>
    Task<Shared.Dto.Upload> DirectUpload(IFormFile file, UploadType type, Guid? targetId);
}
