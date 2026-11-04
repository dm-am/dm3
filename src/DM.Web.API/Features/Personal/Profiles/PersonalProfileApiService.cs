using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;

namespace DM.Web.API.Features.Personal.Profiles;

/// <summary>
/// API service for managing current user's profile
/// </summary>
public class PersonalProfileApiService
{
    private readonly IUserService _userService;
    private readonly IIdentityProvider _identityProvider;
    private readonly PersonalProfileMapper _mapper;

    public PersonalProfileApiService(
        IUserService userService,
        IIdentityProvider identityProvider,
        PersonalProfileMapper mapper)
    {
        _userService = userService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    public async Task<PersonalProfile> GetMyProfile()
    {
        var currentUser = _identityProvider.Current.User;
        var user = await _userService.GetDetailsAsync(currentUser.Username);
        return _mapper.ToPersonalProfile(user);
    }

    public async Task<PersonalProfile> UpdateMyProfile(UpdateProfile profile)
    {
        var currentUser = _identityProvider.Current.User;
        var updateUser = PersonalProfileMapper.ToUpdateUser(profile);
        updateUser.Username = currentUser.Username;

        var updatedUser = await _userService.UpdateAsync(updateUser);
        return _mapper.ToPersonalProfile(updatedUser);
    }

    /// <summary>Idempotent: without an avatar it does nothing.</summary>
    public Task RemoveMyAvatar()
    {
        var currentUser = _identityProvider.Current.User;
        return _userService.RemoveAvatarAsync(currentUser.UserId);
    }
}
