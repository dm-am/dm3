using System;
using System.Collections.Generic;
using DM.Services.Core.Dto;
using DM.Services.Core.Dto.Enums;

namespace DM.Services.Forum.Dto.Output;

/// <summary>
/// Forum (Board) DTO model
/// </summary>
public class Forum
{
    /// <summary>
    /// Forum identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Forum title
    /// </summary>
    public string Title { get; set; }

    /// <summary>
    /// Short description
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Create topic policy
    /// </summary>
    public ForumAccessPolicy CreateTopicPolicy { get; set; }

    /// <summary>
    /// View topic policy
    /// </summary>
    public ForumAccessPolicy ViewPolicy { get; set; }

    /// <summary>
    /// Moderator identifiers
    /// </summary>
    public IEnumerable<Guid> ModeratorIds { get; set; }

    /// <summary>
    /// Total number of topics in the forum
    /// </summary>
    public int TopicsCount { get; set; }

    /// <summary>
    /// Total number of comments in the forum
    /// </summary>
    public int CommentsCount { get; set; }

    /// <summary>
    /// Total number of forum topics that has unread commentaries
    /// </summary>
    public int UnreadTopicsCount { get; set; }

    /// <summary>
    /// Total number of unread commentaries across all topics
    /// </summary>
    public int UnreadCommentsCount { get; set; }

    /// <summary>
    /// Last comment in the forum
    /// </summary>
    public ForumLastComment LastComment { get; set; }
}

/// <summary>
/// Last comment in forum DTO model
/// </summary>
public class ForumLastComment
{
    /// <summary>
    /// Comment identifier
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Topic identifier
    /// </summary>
    public Guid TopicId { get; set; }

    /// <summary>
    /// Author
    /// </summary>
    public GeneralUser Author { get; set; }

    /// <summary>
    /// Creation moment
    /// </summary>
    public DateTimeOffset CreateDate { get; set; }
}