using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// Input DTO for users filtering
/// </summary>
public class UsersQuery : PagingQuery
{
    /// <summary>
    /// User activity filter (Active, All, Pending)
    /// </summary>
    public UserActivityFilter Filter { get; set; } = UserActivityFilter.Active;

    /// <summary>
    /// Search by login prefix (optional)
    /// </summary>
    public string? Search { get; set; }
}