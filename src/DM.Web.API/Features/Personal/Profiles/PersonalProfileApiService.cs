using System.Threading.Tasks;
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
    private readonly PersonalProfileMapper _mapper;

    /// <inheritdoc />
    public PersonalProfileApiService(
        IUserService userService,
        IIdentityProvider identityProvider,
        PersonalProfileMapper mapper)
    {
        _userService = userService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<PersonalProfile> GetMyProfile()
    {
        var currentUser = _identityProvider.Current.User;
        var user = await _userService.GetDetailsAsync(currentUser.Username);
        return WithWithheldPrivilege(_mapper.ToPersonalProfile(user), currentUser);
    }

    /// <inheritdoc />
    public async Task<PersonalProfile> UpdateMyProfile(UpdateProfile profile)
    {
        var currentUser = _identityProvider.Current.User;
        var updateUser = _mapper.ToUpdateUser(profile);
        updateUser.Username = currentUser.Username;

        var updatedUser = await _userService.UpdateAsync(updateUser);
        return WithWithheldPrivilege(_mapper.ToPersonalProfile(updatedUser), currentUser);
    }

    /// <summary>
    /// Put the one fact about the viewer that the user row does not hold onto
    /// the profile the viewer is reading about themselves.
    /// </summary>
    /// <remarks>
    /// The profile is read from the database, so its role is the recorded one -
    /// which is correct and required (INV-11), and which is also why the
    /// interface cannot tell from it that the rank is not in force. Without this
    /// line an administrator who has not set the factor up sees every moderation
    /// tab in place and a refusal on each page under them.
    ///
    /// Both answers set it, the read and the write: the write is what the
    /// settings form adopts as the viewer afterwards, and a profile saved while
    /// the rank was withheld must not come back saying otherwise.
    /// </remarks>
    private static PersonalProfile WithWithheldPrivilege(
        PersonalProfile profile, AuthenticatedUser viewer)
    {
        profile.PrivilegeWithheld = viewer.PrivilegeWithheld;
        return profile;
    }

    /// <inheritdoc />
    public Task RemoveMyAvatar()
    {
        var currentUser = _identityProvider.Current.User;
        return _userService.RemoveAvatarAsync(currentUser.UserId);
    }
}
