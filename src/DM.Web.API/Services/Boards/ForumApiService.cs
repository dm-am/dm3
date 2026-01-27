using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Services.Forum.BusinessProcesses.Boards;
using DM.Web.API.Dto.Contracts;
using DM.Web.API.Dto.Boards;

namespace DM.Web.API.Services.Boards;

/// <inheritdoc />
internal class BoardApiService : IBoardApiService
{
    private readonly IBoardReadingService boardService;
    private readonly IMapper mapper;

    /// <inheritdoc />
    public BoardApiService(
        IBoardReadingService boardService,
        IMapper mapper)
    {
        this.boardService = boardService;
        this.mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Forum>> Get()
    {
        var boards = await boardService.GetBoardsList();
        return new ListEnvelope<Forum>(boards.Select(mapper.Map<Forum>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Forum>> Get(string id)
    {
        var board = await boardService.GetSingleBoard(id);
        return new Envelope<Forum>(mapper.Map<Forum>(board));
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Board>> GetBoards()
    {
        var boards = await boardService.GetBoardsList();
        return new ListEnvelope<Board>(boards.Select(mapper.Map<Board>));
    }

    /// <inheritdoc />
    public async Task<Envelope<Board>> GetBoard(string id)
    {
        var board = await boardService.GetSingleBoard(id);
        return new Envelope<Board>(mapper.Map<Board>(board));
    }
}