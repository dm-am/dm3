using AutoMapper;
using DM.Domain.Core.Uploads;
using DbUpload = DM.Infrastructure.Persistence.Entities.Shared.Upload;

namespace DM.Infrastructure.Persistence.Repositories.General;

/// <summary>
/// Profile for upload mapper
/// </summary>
internal class UploadMappingProfile : Profile
{
    /// <inheritdoc />
    public UploadMappingProfile()
    {
        CreateMap<DbUpload, Upload>()
            .ForMember(d => d.Id, s => s.MapFrom(u => u.UploadId))
            .ForMember(d => d.Owner, s => s.MapFrom(u => u.Owner));
    }
}
