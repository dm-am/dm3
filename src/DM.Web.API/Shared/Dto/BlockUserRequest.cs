using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Shared.Dto;

/// <summary>
/// Simple request to block a user (by username only)
/// </summary>
public class BlockUserRequest
{
    /// <summary>Username of user to block</summary>
    [Required(ErrorMessage = "Имя пользователя обязательно")]
    public string Username { get; set; } = string.Empty;
}
