using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// 
/// </summary>
public class UserSettings
{
    /// <summary>
    /// Website color schema
    /// </summary>
    public ColorSchema ColorSchema { get; set; }

    /// <summary>
    /// Message that user's newbies will receive once they are connected
    /// </summary>
    public string MentorGreetingsMessage { get; set; }

    /// <summary>
    /// Paging limits settings
    /// </summary>
    public PagingLimits PagingLimits { get; set; }
}