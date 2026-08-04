using System;
using System.Collections.Generic;
using System.Linq;
using DM.Domain.Blog.Features.Comments;
using DM.Domain.Blog.Features.PublicationComments;
using DM.Domain.Community.Features.WebsiteTestimonials;
using DM.Domain.Forum.Features.Comments;
using DM.Domain.Game.Features.Comments;
using DM.Domain.Game.Features.Posts;
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
/// API_DESIGN says an unknown sort field is a validation error and not a
/// silently different order, and that the allowed fields are declared beside
/// the endpoint rather than listed in the document. One endpoint kept that
/// promise — <c>GET /v1/games</c>, through GamesQueryValidator — and the other
/// twelve answered 200 with the default order, because the repositories all
/// end their sort switch in a default arm and nothing rejects the input
/// before it gets there.
///
/// A whitelist per bound query type is what the repositories actually
/// implement: their switches are over lowercase strings, so the values here
/// are those strings and nothing has to be kept in step by hand beyond
/// adding a case. Two consumers read this table — the action filter that
/// answers 400, and the swagger parameter filter that publishes the same
/// values as the parameter's <c>enum</c> — so the contract and the behaviour
/// cannot drift apart.
///
/// The list is keyed by the type the controller binds, which is sometimes an
/// API DTO and sometimes the domain query itself. Adding a list endpoint
/// without adding it here is caught by the contract test: a sortBy parameter
/// whose schema declares no enum fails
/// <c>OpenApiContractShould.DeclareTheSortFieldsOfEveryListEndpoint</c>.
/// </remarks>
public static class SortVocabulary
{
    /// <summary>The only two directions, everywhere.</summary>
    public static readonly IReadOnlyList<string> SortOrders = ["asc", "desc"];

    /// <summary>Comment lists are the same three ways round wherever they hang.</summary>
    private static readonly string[] CommentFields = ["created", "likes"];

    private static readonly IReadOnlyDictionary<Type, IReadOnlyList<string>> Fields =
        new Dictionary<Type, IReadOnlyList<string>>
        {
            // GET /v1/blogs — BlogRepository.ApplySorting
            [typeof(BlogsQuery)] =
                ["created", "title", "status", "popularity", "activated", "closed"],
            // GET /v1/blogs/{id}/comments — BlogCommentRepository.ApplySorting
            [typeof(BlogCommentsQuery)] = CommentFields,
            // GET /v1/publications/{id}/comments — PublicationCommentRepository.ApplySorting
            [typeof(PublicationCommentsQuery)] = CommentFields,
            // GET /v1/topics/{id}/comments — TopicCommentRepository.ApplySorting
            [typeof(CommentsQuery)] = CommentFields,
            // GET /v1/games/{id}/comments — GameCommentRepository.ApplySorting
            [typeof(GameCommentsQuery)] = CommentFields,
            // GET /v1/polls — PollRepository.BuildSort (plus the "status" order it
            // handles before the switch)
            [typeof(PollsQuery)] = ["status", "starts", "ends"],
            // GET /v1/users/{username}/endorsements and .../written-endorsements
            // — UserEndorsementRepository.ApplySort
            [typeof(UserEndorsementsQuery)] = ["created", "author"],
            // GET /v1/testimonials — WebsiteTestimonialRepository.ApplySorting
            [typeof(WebsiteTestimonialsQuery)] = ["created", "author"],
            // GET /v1/topics and GET /v1/boards/{id}/topics — TopicRepository
            [typeof(TopicsQuery)] = ["lastActivity", "created", "title", "likes"],
            // GET /v1/games — GameRepository.ApplySorting, the same eight values
            // GamesQueryValidator has always enforced
            [typeof(GamesQuery)] =
            [
                "created", "recruitmentstarted", "title", "popularity",
                "status", "availableslots", "activated", "closed"
            ],
            // GET /v1/posts — PostRepository.GetRatedPosts
            [typeof(PostsQuery)] = ["rating", "created", "lastreview", "reviewcount"],
            // GET /v1/users names its sort field with the UserSort enum, which
            // model binding already refuses an unknown value for; the direction
            // beside it is a plain string like everywhere else, and this entry is
            // what holds it to asc/desc.
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
        allowed.Any(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase));
}
