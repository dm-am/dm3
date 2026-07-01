using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Features.General.Upload;

/// <summary>
/// API service for file upload management.
/// Direct upload only: server валидирует, процессит (для изображений — генерирует
/// thumbnails), кладет в S3 атомарно. Никаких presigned URLs.
/// </summary>
public interface IUploadApiService
{
    /// <summary>List uploads (current user, specific user, or all — admin gating).</summary>
    Task<(IEnumerable<Shared.Dto.Upload> Uploads, PagingInfo Paging)> GetUploads(
        UploadsQuery query, string? username, bool all);

    /// <summary>Get upload by ID (owner или admin).</summary>
    Task<Shared.Dto.Upload> GetUpload(Guid id);

    /// <summary>Soft-delete upload (owner или admin).</summary>
    Task DeleteUpload(Guid id);

    /// <summary>
    /// Upload file directly with server-side processing.
    /// Для изображений — magic-byte валидация, EXIF-strip, генерация WebP thumbnails,
    /// атомарный batch S3 PUT, нормализация расширения по validated content-type.
    /// </summary>
    Task<Shared.Dto.Upload> DirectUpload(IFormFile file, UploadType type, Guid? targetId);
}
