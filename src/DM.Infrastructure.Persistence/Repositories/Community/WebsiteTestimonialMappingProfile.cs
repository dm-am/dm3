using System;
using System.Linq;
using AutoMapper;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Infrastructure.Persistence.Repositories.Personal;
using DbWebsiteTestimonial = DM.Infrastructure.Persistence.Entities.Community.WebsiteTestimonial;

namespace DM.Infrastructure.Persistence.Repositories.Community;

/// <inheritdoc />
internal class WebsiteTestimonialMappingProfile : Profile
{
    /// <inheritdoc />
    public WebsiteTestimonialMappingProfile()
    {
        CreateMap<DbWebsiteTestimonial, WebsiteTestimonial>()
            .ForMember(d => d.Id, s => s.MapFrom(t => t.WebsiteTestimonialId))
            .ForMember(d => d.Author, s => s.MapFrom(t => t.Author))
            .ForMember(d => d.CreatedUtc, s => s.MapFrom(t => t.CreatedUtc))
            .ForMember(d => d.ModifiedUtc, s => s.MapFrom(t => t.ModifiedUtc))
            .ForMember(d => d.Text, s => s.MapFrom(t => t.Text));
    }
}
