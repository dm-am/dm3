using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Web.API.Shared.Dto;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Features.General.Upload;

/// <summary>
/// API service for file upload management
/// </summary>
public interface IUploadApiService
{
    /// <summary>
    /// Get uploads with optional filters
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination</param>
    /// <param name="username">Optional username filter (admin only)</param>
    /// <param name="all">If true, returns all uploads (admin only)</param>
    Task<(IEnumerable<Shared.Dto.Upload> Uploads, PagingInfo Paging)> GetUploads(UploadsQuery query, string? username, bool all);

    /// <summary>
    /// Get upload by ID
    /// </summary>
    Task<Shared.Dto.Upload> GetUpload(Guid id);

    /// <summary>
    /// Delete upload (soft delete)
    /// </summary>
    Task DeleteUpload(Guid id);

    /// <summary>
    /// Request presigned URL for direct upload to storage
    /// </summary>
    Task<PresignResponse> RequestPresignedUrl(PresignRequest request);

    /// <summary>
    /// Confirm upload completion and trigger processing
    /// </summary>
    Task<Shared.Dto.Upload> ConfirmUpload(Guid id);

    /// <summary>
    /// Upload file directly with server-side processing (thumbnails for images)
    /// </summary>
    /// <param name="file">Uploaded file</param>
    /// <param name="type">Upload type/purpose</param>
    /// <param name="targetId">Optional target entity ID</param>
    Task<Shared.Dto.Upload> DirectUpload(IFormFile file, UploadType type, Guid? targetId);
}
