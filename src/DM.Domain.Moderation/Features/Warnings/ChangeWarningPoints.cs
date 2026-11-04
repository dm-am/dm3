namespace DM.Domain.Moderation.Features.Warnings;

/// <summary>
/// DTO for changing the points of a warning
/// </summary>
public class ChangeWarningPoints
{
    /// <summary>
    /// New points, 0 taking the warning off the violation scale; absent is refused
    /// </summary>
    public int? Points { get; set; }

    /// <summary>
    /// Why the points are being changed
    /// </summary>
    public string Reason { get; set; } = "";
}
