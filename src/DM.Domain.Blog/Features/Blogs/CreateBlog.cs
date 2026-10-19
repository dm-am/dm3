using DM.Domain.Core.Enums;

namespace DM.Domain.Blog.Features.Blogs;

public class CreateBlog
{
    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    /// <summary>
    /// Private = only roles, Public = preview visible to all
    /// </summary>
    public DraftVisibility DraftVisibility { get; set; } = DraftVisibility.Public;

    /// <summary>
    /// Copy the creator's personal blacklist into the blog's on creation.
    /// Null leaves the choice to the creator's account setting "Переносить
    /// черный список в новые игры и блоги" (AutoPopulateContentBlacklist).
    /// </summary>
    /// <remarks>
    /// Copying shows who is on the creator's personal list to whoever manages the
    /// blog's list with them, and tells the people on it that they are on the
    /// blog's list the first time they try to write there. That is the price
    /// of the switch, and the reason it is off unless the creator turns it on.
    /// </remarks>
    public bool? CopyBlacklist { get; set; }
}
