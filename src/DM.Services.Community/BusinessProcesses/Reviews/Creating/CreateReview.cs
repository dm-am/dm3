namespace DM.Services.Community.BusinessProcesses.Reviews.Creating;

/// <summary>
/// DTO model for review creating
/// </summary>
public class CreateReview
{
    /// <summary>
    /// Review text
    /// </summary>
    public string Text { get; set; }

    /// <summary>
    /// Author login (only for admin-created reviews)
    /// </summary>
    public string AuthorLogin { get; set; }
}