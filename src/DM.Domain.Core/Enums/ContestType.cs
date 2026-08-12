namespace DM.Domain.Core.Enums;

/// <summary>
/// Contest type. Each type has its own sequential numbering (Literary #1-N,
/// Art #1-M etc.). Extended as new contest types are added.
/// </summary>
public enum ContestType
{
    /// <summary>Literary contest.</summary>
    Literary = 0,

    /// <summary>Art contest.</summary>
    Art = 1,
}
