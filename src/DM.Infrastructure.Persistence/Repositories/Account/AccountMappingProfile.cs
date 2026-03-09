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
        CreateMap<Entities.Account.Session, Session>();
    }
}
