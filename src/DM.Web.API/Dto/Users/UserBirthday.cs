namespace DM.Web.API.Dto.Users;

/// <summary>
/// DTO model for user birthday with optional year visibility
/// </summary>
public class UserBirthday
{
    /// <summary>
    /// Day of birth (1-31)
    /// </summary>
    public int? Day { get; set; }

    /// <summary>
    /// Month of birth (1-12)
    /// </summary>
    public int? Month { get; set; }

    /// <summary>
    /// Year of birth (null if user chose to hide it)
    /// </summary>
    public int? Year { get; set; }
}
