using System;
using DM.Web.API.Dto.Users;

namespace DM.Web.API.Dto.Community;

/// <summary>
/// API DTO model for site review
/// </summary>
public class Review
{
    /// <summary>
    /// Identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Review author
    /// </summary>
    public User Author { get; set; }

    /// <summary>
    /// Author login (for creating reviews on behalf of users)
    /// </summary>
    public string AuthorLogin { get; set; }

    /// <summary>
    /// Creating moment
    /// </summary>
    public DateTimeOffset CreatedUtc { get; set; }

    /// <summary>
    /// Publish flag
    /// </summary>
    public bool? IsApproved { get; set; }

    /// <summary>
    /// Review text
    /// </summary>
    public string Text { get; set; }
}