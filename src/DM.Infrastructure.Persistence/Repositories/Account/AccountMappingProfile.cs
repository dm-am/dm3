using System;
using AutoMapper;
using DM.Domain.Core.Identity;

namespace DM.Infrastructure.Persistence.Repositories.Account;

/// <summary>
/// AutoMapper profile for Account entities
/// </summary>
internal class AccountMappingProfile : Profile
{
    public AccountMappingProfile()
    {
        CreateMap<Entities.Account.Session, Session>()
            .ForMember(d => d.IsCurrent, opt => opt.Ignore())
            .ForMember(d => d.ExpirationUtc, opt => opt.MapFrom(s => new DateTimeOffset(s.ExpirationUtc, TimeSpan.Zero)))
            .ForMember(d => d.CreatedUtc, opt => opt.MapFrom(s => new DateTimeOffset(s.CreatedUtc, TimeSpan.Zero)));
    }
}
