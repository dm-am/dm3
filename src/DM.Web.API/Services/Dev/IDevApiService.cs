using System.Collections.Generic;
using System.Threading.Tasks;
using DM.Services.Core.Dto.Enums;

namespace DM.Web.API.Services.Dev;

/// <summary>
/// Test account info for frontend display
/// </summary>
public class TestAccountInfo
{
    /// <summary>Login</summary>
    public string Login { get; set; }

    /// <summary>Password</summary>
    public string Password { get; set; }

    /// <summary>Role</summary>
    public UserRole Role { get; set; }
}

/// <summary>
/// Development-only API service for testing
/// </summary>
public interface IDevApiService
{
    /// <summary>
    /// Set role for current user (dev only)
    /// </summary>
    Task SetRole(UserRole role);

    /// <summary>
    /// Get all users from database
    /// </summary>
    Task<IReadOnlyList<TestAccountInfo>> GetAllUsers();
}
