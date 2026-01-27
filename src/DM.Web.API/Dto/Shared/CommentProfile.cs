using AutoMapper;
using DM.Services.Common.Dto;

namespace DM.Web.API.Dto.Shared;

/// <summary>
/// Mapping profile from Service DTO to API DTO for commentaries
/// </summary>
internal class CommentProfile : Profile
{
    /// <inheritdoc />
    public CommentProfile()
    {
        CreateMap<DM.Services.Common.Dto.Comment, Comment>()
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(c => c.CreatedUtc))
            .ForMember(d => d.UpdatedUtc, s => s.MapFrom(c => c.ModifiedUtc));

        CreateMap<Comment, CreateComment>();
        CreateMap<Comment, UpdateComment>();
    }
}