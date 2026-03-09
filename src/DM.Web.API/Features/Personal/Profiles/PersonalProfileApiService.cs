using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;

namespace DM.Web.API.Features.Personal.Profiles;

/// <summary>
/// API service for managing current user's profile
/// </summary>
internal class PersonalProfileApiService : IPersonalProfileApiService
{
    private readonly IUserService _userService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PersonalProfileApiService(
        IUserService userService,
        IIdentityProvider identityProvider,
        IMapper mapper)
    {
        _userService = userService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PersonalProfile> GetMyProfile()
    {
        var currentUser = _identityProvider.Current.User;
        var user = await _userService.GetDetails(currentUser.Username);
        return _mapper.Map<PersonalProfile>(user);
    }

    /// <inheritdoc />
    public async Task<PersonalProfile> UpdateMyProfile(UpdateProfile profile)
    {
        var currentUser = _identityProvider.Current.User;
        var updateUser = _mapper.Map<UpdateUser>(profile);
        updateUser.Username = currentUser.Username;

        var updatedUser = await _userService.Update(updateUser);
        return _mapper.Map<PersonalProfile>(updatedUser);
    }
}
