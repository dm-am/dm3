using System.Threading.Tasks;

namespace DM.Web.API.Features.Personal.Preferences;

/// <summary>
/// Service for managing user display preferences
/// </summary>
public interface IPreferencesApiService
{
    /// <summary>
    /// Get current user's preferences
    /// </summary>
    Task<Preferences> GetMyPreferences();

    /// <summary>
    /// Update current user's preferences
    /// </summary>
    Task<Preferences> UpdateMyPreferences(Preferences preferences);
}
