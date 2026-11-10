using System.Collections.Generic;

namespace DM.Tools.Seeder.Seeding;

internal class SeedResult
{
    public int Created { get; set; }

    /// <summary>
    /// Number of users skipped (already exist)
    /// </summary>
    public int Skipped { get; set; }

    public List<string> CreatedUsernames { get; set; } = new();

    /// <summary>
    /// List of skipped user usernames (already exist)
    /// </summary>
    public List<string> SkippedUsernames { get; set; } = new();
}

internal class ComprehensiveSeedResult
{
    public int TopicsCreated { get; set; }

    public int CommentsCreated { get; set; }

    public int GamesCreated { get; set; }

    public int CharactersCreated { get; set; }

    public int PostsCreated { get; set; }

    public int BlogsCreated { get; set; }

    public int PublicationsCreated { get; set; }

    public int MessagesCreated { get; set; }

    /// <summary>
    /// Number of reviews created (user, game, post reviews)
    /// </summary>
    public int ReviewsCreated { get; set; }

    /// <summary>
    /// Number of testimonials created (website reviews)
    /// </summary>
    public int TestimonialsCreated { get; set; }

    public int PollsCreated { get; set; }

    public int LikesCreated { get; set; }

    public int BoardModeratorsAssigned { get; set; }

    public int NotificationsCreated { get; set; }

    public List<string> Details { get; set; } = new();
}
