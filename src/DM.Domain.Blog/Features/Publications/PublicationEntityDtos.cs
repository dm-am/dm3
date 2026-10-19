using System;

namespace DM.Domain.Blog.Features.Publications;

public class CreatePublicationEntity
{
    public Guid PublicationId { get; set; }

    public Guid BlogId { get; set; }

    public Guid? RubricId { get; set; }

    public Guid AuthorId { get; set; }

    public string Title { get; set; } = null!;

    public string Content { get; set; } = null!;

    public string? Preview { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }
}

/// <summary>
/// A null field is left as it is.
/// </summary>
public class UpdatePublicationEntity
{
    public Guid PublicationId { get; set; }

    public Guid? RubricId { get; set; }

    public bool ClearRubric { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public string? Preview { get; set; }

    public DateTimeOffset ModifiedUtc { get; set; }

    /// <summary>
    /// Who is editing: not always the author, and the actor of the notification
    /// a reader's blacklist is held against.
    /// </summary>
    public Guid ModifiedByUserId { get; set; }
}
