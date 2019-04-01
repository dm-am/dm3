using AutoMapper;

namespace DM.Web.API.Dto.Fora
{
    /// <summary>
    /// Mapping profile from Service DTO to API DTO for fora
    /// </summary>
    public class ForumProfile : Profile
    {
        /// <inheritdoc />
        public ForumProfile()
        {
            CreateMap<DM.Services.Forum.Dto.Forum, Forum>()
                .ForMember(d => d.Id, s => s.MapFrom(f => f.Title));
        }
    }
}