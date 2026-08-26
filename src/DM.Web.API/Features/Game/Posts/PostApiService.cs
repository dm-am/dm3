using System;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Core.Dto;
using DM.Domain.Game.Features.Posts;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.Posts;

/// <inheritdoc />
internal class PostApiService : IPostApiService
{
    private readonly IPostService _postService;
    private readonly PostMapper _mapper;
    private readonly IQuoteSourceService _quoteSourceService;

    /// <inheritdoc />
    public PostApiService(
        IPostService postService,
        PostMapper mapper,
        IQuoteSourceService quoteSourceService)
    {
        _postService = postService;
        _mapper = mapper;
        _quoteSourceService = quoteSourceService;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Post>> Get(Guid roomId, PagingQuery query)
    {
        var (posts, paging) = await _postService.GetAllAsync(roomId, query);
        return new ListEnvelope<Post>(posts.Select(_mapper.ToPost), new PagingInfo(paging));
    }

    /// <inheritdoc />
    public async Task<Envelope<Post>> Get(Guid postId)
    {
        var post = await _postService.GetAsync(postId);
        return new Envelope<Post>(_mapper.ToPost(post));
    }

    /// <inheritdoc />
    public async Task<Envelope<QuoteSource>> GetQuote(Guid postId)
    {
        // Read through the same service the ordinary read goes through: whoever
        // is refused the post is refused its quotation by the same refusal, and
        // there is no second permission rule here to keep in step with the first.
        var post = await _postService.GetAsync(postId);
        var dto = _mapper.ToPost(post);
        return _quoteSourceService.Build(dto.GameText, QuoteAuthorName(post));
    }

    /// <summary>
    /// Whose name goes into the header of a quotation of a game post.
    /// </summary>
    /// <remarks>
    /// The same four steps the post header itself takes, and in the same order:
    /// the character, then the game role, then the login, then "Системное" for a
    /// post that has none of the three. Anything else and the quotation would
    /// attribute the line to somebody the page does not show as its author.
    ///
    /// It is a snapshot: what the name was when the quotation was made. Renaming
    /// a character afterwards does not go back and rewrite somebody else's reply.
    /// </remarks>
    private static string QuoteAuthorName(DM.Domain.Game.Features.Games.Post post)
    {
        if (!string.IsNullOrWhiteSpace(post.Character?.Name)) return post.Character.Name;
        if (!string.IsNullOrWhiteSpace(post.AuthorGameRole)) return post.AuthorGameRole;
        if (!string.IsNullOrWhiteSpace(post.Author?.Username)) return post.Author.Username;
        return SystemAuthorName;
    }

    /// <summary>
    /// Author of a post written by nobody, as the post header spells it.
    /// </summary>
    private const string SystemAuthorName = "Системное";

    /// <inheritdoc />
    public async Task<Envelope<Post>> Create(Guid roomId, CreatePostRequest post)
    {
        var createPost = _mapper.ToCreatePost(post);
        createPost.RoomId = roomId;
        var createdPost = await _postService.CreateAsync(createPost);
        return new Envelope<Post>(_mapper.ToPost(createdPost));
    }

    /// <inheritdoc />
    public async Task<Envelope<Post>> Update(Guid postId, UpdatePostRequest request)
    {
        var updatePost = _mapper.ToUpdatePost(request);
        updatePost.PostId = postId;
        var updatedPost = await _postService.UpdateAsync(updatePost);
        return new Envelope<Post>(_mapper.ToPost(updatedPost));
    }

    /// <inheritdoc />
    public Task Delete(Guid postId) => _postService.DeleteAsync(postId);

    /// <inheritdoc />
    public Task MarkAsRead(Guid roomId) => _postService.MarkAsReadAsync(roomId);

    /// <inheritdoc />
    public async Task<ListEnvelope<Post>> GetRated(PostsQuery query)
    {
        var (posts, paging) = await _postService.GetRatedAsync(query);
        return new ListEnvelope<Post>(posts.Select(_mapper.ToPost), new PagingInfo(paging));
    }
}
