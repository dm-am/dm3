using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using AutoMapper.QueryableExtensions;
using DM.Domain.Core.Uploads;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using DbUpload = DM.Infrastructure.Persistence.Entities.CrossDomain.Upload;

namespace DM.Infrastructure.Persistence.Repositories.General;

/// <summary>
/// Repository for public image uploads
/// </summary>
internal class PublicImageUploadRepository : IPublicImageUploadRepository
{
    private readonly DmDbContext _dbContext;
    private readonly IMapper _mapper;

    public PublicImageUploadRepository(
        DmDbContext dbContext,
        IMapper mapper)
    {
        _dbContext = dbContext;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Upload>> Create(IEnumerable<UploadEntity> uploads)
    {
        var uploadEntities = uploads.Select(u => new DbUpload
        {
            UploadId = u.UploadId,
            CreatedUtc = u.CreatedUtc,
            UserId = u.UserId,
            EntityId = u.EntityId,
            FileName = u.FileName,
            FilePath = u.FilePath,
            Original = u.Original,
            IsRemoved = u.IsRemoved
        }).ToArray();

        var uploadIds = uploadEntities.Select(u => u.UploadId).ToHashSet();

        await _dbContext.Uploads.AddRangeAsync(uploadEntities);
        await _dbContext.SaveChangesAsync();

        return await _dbContext.Uploads
            .Where(u => uploadIds.Contains(u.UploadId))
            .ProjectTo<Upload>(_mapper.ConfigurationProvider)
            .ToArrayAsync();
    }

    /// <inheritdoc />
    public async Task RemoveObsoleteUploads(Guid entityId)
    {
        var uploadsInfo = await _dbContext.Uploads
            .Where(u => u.EntityId == entityId && !u.IsRemoved)
            .OrderByDescending(u => u.CreatedUtc)
            .Select(u => new { u.CreatedUtc, u.UploadId })
            .ToArrayAsync();

        var obsoleteUploadIds = uploadsInfo
            .GroupBy(u => u.CreatedUtc)
            .OrderByDescending(g => g.Key)
            .Skip(1)
            .SelectMany(g => g.Select(u => u.UploadId))
            .ToHashSet();

        if (obsoleteUploadIds.Count > 0)
        {
            await _dbContext.Uploads
                .Where(u => obsoleteUploadIds.Contains(u.UploadId))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsRemoved, true));
        }
    }
}
