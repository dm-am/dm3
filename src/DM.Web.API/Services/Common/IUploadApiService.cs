using System;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;
using DM.Web.API.Dto.Common;
using DM.Web.API.Dto.Contracts;
using Microsoft.AspNetCore.Http;

namespace DM.Web.API.Services.Common;

/// <summary>
/// API service for file upload management
/// </summary>
public interface IUploadApiService
{
    /// <summary>
    /// Get uploads with optional filters
    /// </summary>
    /// <param name="query">Query parameters for filtering and pagination</param>
    /// <param name="userLogin">Optional user login filter (admin only)</param>
    /// <param name="all">If true, returns all uploads (admin only)</param>
    Task<ListEnvelope<Upload>> GetUploads(UploadsQuery query, string? userLogin, bool all);

    /// <summary>
    /// Get upload by ID
    /// </summary>
    Task<Envelope<Upload>> GetUpload(Guid id);

    /// <summary>
    /// Delete upload (soft delete)
    /// </summary>
    Task DeleteUpload(Guid id);

    /// <summary>
    /// Request presigned URL for direct upload to storage
    /// </summary>
    Task<Envelope<PresignResponse>> RequestPresignedUrl(PresignRequest request);

    /// <summary>
    /// Confirm upload completion and trigger processing
    /// </summary>
    Task<Envelope<Upload>> ConfirmUpload(Guid id);

    /// <summary>
    /// Upload file directly with server-side processing (thumbnails for images)
    /// </summary>
    /// <param name="file">Uploaded file</param>
    /// <param name="type">Upload type/purpose</param>
    /// <param name="targetId">Optional target entity ID</param>
    Task<Envelope<Upload>> DirectUpload(IFormFile file, UploadType type, Guid? targetId);
}
