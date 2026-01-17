using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Forum.BusinessProcesses.Boards;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Boards;

namespace DM.Web.API.Services.Boards;

/// <inheritdoc />
internal class ForumApiService : IForumApiService
{
    private readonly IForumReadingService forumService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public ForumApiService(
        IForumReadingService forumService,
        IMapper mapper)
    {
        this.forumService = forumService;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Forum>> Get()
    {
        var fora = await forumService.GetForaList();
        return new ListEnvelope<Forum>(fora.Select(mapper.Map<Forum>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Forum>> Get(string id)
    {
        var forum = await forumService.GetSingleForum(id);
        return new Envelope<Forum>(mapper.Map<Forum>(forum));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Board>> GetBoards()
    {
        var fora = await forumService.GetForaList();
        return new ListEnvelope<Board>(fora.Select(mapper.Map<Board>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Board>> GetBoard(string id)
    {
        var forum = await forumService.GetSingleForum(id);
        return new Envelope<Board>(mapper.Map<Board>(forum));
    }
}