using System.ComponentModel.DataAnnotations;

namespace DM.Web.API.Dto.Users;

/// <summary>
/// User paging preferences for various list views
/// </summary>
/// <remarks>
/// All values must be between 5 and 100 items per page.
/// Default values are typically 10-20 depending on the content type.
/// </remarks>
public class PagingLimits
{
    /// <summary>
    /// Number of posts per page in game rooms
    /// </summary>
    [Range(5, 100, ErrorMessage = "Posts per page must be between 5 and 100")]
    public int PostsPerPage { get; set; }

    /// <summary>
    /// Number of comments per page on games or topics
    /// </summary>
    [Range(5, 100, ErrorMessage = "Comments per page must be between 5 and 100")]
    public int CommentsPerPage { get; set; }

    /// <summary>
    /// Number of topics per page on forum boards
    /// </summary>
    [Range(5, 100, ErrorMessage = "Topics per page must be between 5 and 100")]
    public int TopicsPerPage { get; set; }

    /// <summary>
    /// Number of messages per page in conversations
    /// </summary>
    [Range(5, 100, ErrorMessage = "Messages per page must be between 5 and 100")]
    public int MessagesPerPage { get; set; }

    /// <summary>
    /// Number of items per page for other entity lists (users, games, etc.)
    /// </summary>
    [Range(5, 100, ErrorMessage = "Entities per page must be between 5 and 100")]
    public int EntitiesPerPage { get; set; }
}
