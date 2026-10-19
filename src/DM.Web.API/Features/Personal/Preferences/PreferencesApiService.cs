using System.Threading.Tasks;
using DM.Domain.Core.Identity;
using DM.Domain.Personal.Features.Profiles;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// API service for managing user display preferences
/// </summary>
public class PreferencesApiService
{
    private readonly IUserService _userService;
    private readonly IIdentityProvider _identityProvider;
    private readonly PreferencesMapper _mapper;

    public PreferencesApiService(
        IUserService userService,
        IIdentityProvider identityProvider,
        PreferencesMapper mapper)
    {
        _userService = userService;
        _identityProvider = identityProvider;
        _mapper = mapper;
    }

    public async Task<Preferences> GetMyPreferences()
    {
        var currentUser = _identityProvider.Current.User;
        var user = await _userService.GetDetailsAsync(currentUser.Username);

        return _mapper.ToPreferences(user.Settings ?? UserSettings.Default);
    }

    public async Task<Preferences> UpdateMyPreferences(UpdatePreferencesRequest request)
    {
        var currentUser = _identityProvider.Current.User;

        // The write replaces the settings document: a partial request is folded
        // onto what the user has now.
        var current = await GetMyPreferences();
        var merged = new Preferences
        {
            Theme = request.Theme ?? current.Theme,
            PageSize = request.PageSize ?? current.PageSize,
            // Omitted keeps what the account has, blank clears it to UTC.
            TimeZoneId = request.TimeZoneId is null
                ? current.TimeZoneId
                : string.IsNullOrWhiteSpace(request.TimeZoneId)
                    ? null
                    : request.TimeZoneId.Trim()
        };

        var updateUser = new UpdateUser
        {
            Username = currentUser.Username,
            Settings = _mapper.ToUserSettings(merged)
        };

        var updatedUser = await _userService.UpdateAsync(updateUser);
        return _mapper.ToPreferences(updatedUser.Settings ?? UserSettings.Default);
    }
}
