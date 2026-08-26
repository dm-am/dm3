using System.Threading.Tasks;
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
    private readonly PreferencesMapper _mapper;

    /// <inheritdoc />
    public PreferencesApiService(
        IUserService userService,
        IIdentityProvider identityProvider,
        PreferencesMapper mapper)
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
            ? _mapper.ToPreferences(user.Settings)
            : new Preferences { Theme = Theme.Light, Paging = new Paging() };

        return preferences;
    }

    /// <inheritdoc />
    public async Task<Preferences> UpdateMyPreferences(UpdatePreferencesRequest request)
    {
        var currentUser = _identityProvider.Current.User;

        // The write is a wholesale replace of the settings document, so a partial
        // request has to be folded onto what the user has now — otherwise every
        // field the request omits is written as its default.
        var current = await GetMyPreferences();
        var merged = new Preferences
        {
            Theme = request.Theme ?? current.Theme,
            Paging = new Paging
            {
                PostsPerPage = request.Paging?.PostsPerPage ?? current.Paging.PostsPerPage,
                CommentsPerPage = request.Paging?.CommentsPerPage ?? current.Paging.CommentsPerPage,
                TopicsPerPage = request.Paging?.TopicsPerPage ?? current.Paging.TopicsPerPage,
                MessagesPerPage = request.Paging?.MessagesPerPage ?? current.Paging.MessagesPerPage,
                EntitiesPerPage = request.Paging?.EntitiesPerPage ?? current.Paging.EntitiesPerPage,
            }
        };

        var updateUser = new UpdateUser
        {
            Username = currentUser.Username,
            Settings = _mapper.ToUserSettings(merged)
        };

        var updatedUser = await _userService.UpdateAsync(updateUser);
        return _mapper.ToPreferences(updatedUser.Settings);
    }
}
