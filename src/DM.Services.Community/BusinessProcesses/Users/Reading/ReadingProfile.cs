using System.Linq;
using AutoMapper;
using DM.Services.Core.Dto;
using DM.Services.DataAccess.BusinessObjects.Users;

namespace DM.Services.Community.BusinessProcesses.Users.Reading;

/// <inheritdoc />
internal class ReadingProfile : Profile
{
    /// <inheritdoc />
    public ReadingProfile()
    {
        CreateMap<User, UserDetails>()
            .IncludeBase<User, GeneralUser>()
            .ForMember(d => d.Contacts, s => s.MapFrom(u =>
                u.Contacts.OrderBy(c => c.SortOrder).Select(c => new UserContactDto
                {
                    ContactType = c.ContactType,
                    ContactValue = c.ContactValue,
                    SortOrder = c.SortOrder
                })));

        CreateMap<UserContact, UserContactDto>();
    }
}