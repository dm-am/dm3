using System;
using DM.Domain.Core.Comments;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Game.Features.Posts;
using DM.Web.API.Features.Game.Reviews;
using DM.Web.API.Features.Blog.Blogs;
using DM.Web.API.Features.Community.Endorsements;
using DM.Web.API.Features.Community.Polls;
using DM.Web.API.Features.Community.Users;
using DM.Web.API.Features.Forum.Topics;
using DM.Web.API.Features.Game.Games;

namespace DM.Web.API.Shared.Sorting;

/// <summary>
/// The sort fields each list endpoint accepts, in one place.
/// </summary>
/// <remarks>
/// An unknown sort field is a validation error, not a silently different order
/// (API_DESIGN). The values are the lowercase strings the repositories switch on,
/// keyed by the type the controller binds. The action filter refuses with them
/// and the swagger filter publishes them, so contract and behaviour cannot drift;
/// a list endpoint missing here fails
/// <c>OpenApiParametersShould.DeclareTheSortFieldsOfEveryListEndpoint</c>.
/// </remarks>
public static class SortVocabulary
{
    /// <summary>The property a query names its sort field with.</summary>
    public const string SortByProperty = "SortBy";

    /// <summary>The property a query names its sort direction with.</summary>
    public const string SortOrderProperty = "SortOrder";

    /// <summary>The only two directions, everywhere.</summary>
    public static readonly IReadOnlyList<string> SortOrders = ["asc", "desc"];

    private static readonly IReadOnlyDictionary<Type, IReadOnlyList<string>> Fields =
        new Dictionary<Type, IReadOnlyList<string>>
        {
            [typeof(BlogsQuery)] =
                ["created", "title", "status", "popularity", "activated", "closed"],
            // One query type for all four comment lists.
            [typeof(CommentsQuery)] = ["created", "likes"],
            [typeof(PollsQuery)] = ["status", "starts", "ends"],
            [typeof(UserEndorsementsQuery)] = ["created", "author"],
            // "author" is the other side of the pair: the review author for reviews
            // received, the game title for reviews written.
            [typeof(UserGameReviewsQuery)] = ["created", "author"],
            [typeof(WebsiteTestimonialsQuery)] = ["created", "author"],
            [typeof(TopicsQuery)] = ["lastActivity", "created", "title", "likes"],
            [typeof(GamesQuery)] =
            [
                "created", "recruitmentstarted", "title", "popularity",
                "status", "availableslots", "activated", "closed"
            ],
            [typeof(PostsQuery)] = ["rating", "created", "lastreview", "reviewcount"],
            [typeof(RoomPostsQuery)] = ["created", "rating", "reviewcount", "lastreview"],
            // The UserSort enum names the field, and model binding refuses an unknown
            // one; the entry holds the direction to asc/desc.
            [typeof(UsersQuery)] = []
        };

    /// <summary>
    /// The sort fields this query type accepts, or null when the type carries
    /// no sort at all. An empty list means the type sorts, but names its field
    /// with an enum of its own rather than with a sortBy string.
    /// </summary>
    public static IReadOnlyList<string>? FieldsOf(Type queryType) =>
        Fields.TryGetValue(queryType, out var fields) ? fields : null;

    /// <summary>Case-insensitive membership, the way the repositories read it.</summary>
    public static bool Allows(IReadOnlyList<string> allowed, string value) =>
        allowed.Contains(value, StringComparer.OrdinalIgnoreCase);
}
