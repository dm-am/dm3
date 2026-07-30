using System.Collections.Generic;

namespace DM.Tools.Seeder.Seeding;

/// <summary>
/// Result of seeding test users
/// </summary>
internal class SeedResult
{
    /// <summary>
    /// Number of users created
    /// </summary>
    public int Created { get; set; }

    /// <summary>
    /// Number of users skipped (already exist)
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// List of created user usernames
    /// </summary>
    public List<string> CreatedUsernames { get; set; } = new();

    /// <summary>
    /// List of skipped user usernames (already exist)
    /// </summary>
    public List<string> SkippedUsernames { get; set; } = new();
}

/// <summary>
/// Result of comprehensive data seeding
/// </summary>
internal class ComprehensiveSeedResult
{
    /// <summary>
    /// Number of topics created
    /// </summary>
    public int TopicsCreated { get; set; }

    /// <summary>
    /// Number of comments created
    /// </summary>
    public int CommentsCreated { get; set; }

    /// <summary>
    /// Number of games created
    /// </summary>
    public int GamesCreated { get; set; }

    /// <summary>
    /// Number of characters created
    /// </summary>
    public int CharactersCreated { get; set; }

    /// <summary>
    /// Number of posts created
    /// </summary>
    public int PostsCreated { get; set; }

    /// <summary>
    /// Number of blogs created
    /// </summary>
    public int BlogsCreated { get; set; }

    /// <summary>
    /// Number of publications created
    /// </summary>
    public int PublicationsCreated { get; set; }

    /// <summary>
    /// Number of messages created
    /// </summary>
    public int MessagesCreated { get; set; }

    /// <summary>
    /// Number of reviews created (user, game, post reviews)
    /// </summary>
    public int ReviewsCreated { get; set; }

    /// <summary>
    /// Number of testimonials created (website reviews)
    /// </summary>
    public int TestimonialsCreated { get; set; }

    /// <summary>
    /// Number of polls created
    /// </summary>
    public int PollsCreated { get; set; }

    /// <summary>
    /// Number of likes created
    /// </summary>
    public int LikesCreated { get; set; }

    /// <summary>
    /// Number of board moderators assigned
    /// </summary>
    public int BoardModeratorsAssigned { get; set; }

    /// <summary>
    /// Skipped items (already existed)
    /// </summary>
    public int Skipped { get; set; }

    /// <summary>
    /// Details about what was created
    /// </summary>
    public List<string> Details { get; set; } = new();
}
