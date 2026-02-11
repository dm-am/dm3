namespace DM.Services.Community.BusinessProcesses.Blogs;

/// <summary>
/// Blog-related intentions
/// </summary>
public enum BlogIntention
{
    /// <summary>
    /// Create a new blog
    /// </summary>
    Create,

    /// <summary>
    /// Edit blog settings
    /// </summary>
    Edit,

    /// <summary>
    /// Delete a blog
    /// </summary>
    Delete,

    /// <summary>
    /// Create rubric in blog
    /// </summary>
    CreateRubric,

    /// <summary>
    /// Create publication in blog
    /// </summary>
    CreatePublication,

    /// <summary>
    /// View private blog
    /// </summary>
    ViewPrivate,

    /// <summary>
    /// Manage participants
    /// </summary>
    ManageParticipants,

    /// <summary>
    /// Manage blacklist
    /// </summary>
    ManageBlacklist,

    /// <summary>
    /// Approve or reject publications (for mentors)
    /// </summary>
    ApprovePublications,

    /// <summary>
    /// Assign a mentor to the blog
    /// </summary>
    AssignMentor
}
