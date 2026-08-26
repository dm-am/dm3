using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Boards;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Forum.Boards;

/// <inheritdoc />
internal class BoardApiService : IBoardApiService
{
    private readonly IBoardService _boardService;
    private readonly BoardMapper _mapper;

    /// <inheritdoc />
    public BoardApiService(
        IBoardService boardService,
        BoardMapper mapper)
    {
        _boardService = boardService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<Board>> GetBoards()
    {
        var boards = await _boardService.GetBoardsList();
        return new ListEnvelope<Board>(boards.Select(_mapper.ToBoard));
    }

    /// <inheritdoc />
    public async Task<Envelope<Board>> GetBoard(string id)
    {
        var board = await _boardService.GetSingleBoard(id);
        return new Envelope<Board>(_mapper.ToBoard(board));
    }
}
