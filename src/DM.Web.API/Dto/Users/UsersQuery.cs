using DM.Services.Core.Dto;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// Input DTO for users filtering
/// </summary>
public class UsersQuery : PagingQuery
{
    /// <summary>
    /// Filter active/inactive users
    /// </summary>
    public bool Inactive { get; set; }

    /// <summary>
    /// Search by login prefix (optional)
    /// </summary>
    public string? Search { get; set; }
}