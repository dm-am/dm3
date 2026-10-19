using System;

namespace DM.Domain.Community.Features.UserEndorsements;

public class UpdateUserEndorsement
{
    public Guid EndorsementId { get; set; }

    /// <summary>
    /// New endorsement text content (positive only)
    /// </summary>
    public string? Text { get; set; }
}
