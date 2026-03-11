using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;
using ServiceUserSettings = DM.Domain.Core.Identity.UserSettings;
using DM.Domain.Core.Enums;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// API service for managing user display preferences
/// </summary>
internal class PreferencesApiService : IPreferencesApiService
{
    private readonly IUserService _userService;
    private readonly IIdentityProvider _identityProvider;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public PreferencesApiService(
        IUserService userService,
        IIdentityProvider identityProvider,
        IMapper mapper)
    {
        _userService = userService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<Preferences> GetMyPreferences()
    {
        var currentUser = _identityProvider.Current.User;
        var user = await _userService.GetDetailsAsync(currentUser.Username);

        var preferences = user.Settings != null
            ? _mapper.Map<Preferences>(user.Settings)
            : new Preferences { Theme = Theme.Light, Paging = new Paging() };

        return preferences;
    }

    /// <inheritdoc />
    public async Task<Preferences> UpdateMyPreferences(Preferences preferences)
    {
        var currentUser = _identityProvider.Current.User;

        var updateUser = new UpdateUser
        {
            Username = currentUser.Username,
            Settings = _mapper.Map<ServiceUserSettings>(preferences)
        };

        var updatedUser = await _userService.UpdateAsync(updateUser);
        return _mapper.Map<Preferences>(updatedUser.Settings);
    }
}
