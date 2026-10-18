using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Boards;
using DM.Domain.Forum.Features.Topics;
using DM.Web.API.Shared.Dto;
using DomainBoard = DM.Domain.Forum.Features.Boards.Board;

namespace DM.Web.API.Features.Forum.Boards;

/// <inheritdoc />
internal class BoardApiService : IBoardApiService
{
    private readonly IBoardService _boardService;
    private readonly ITopicService _topicService;
    private readonly BoardMapper _mapper;

    /// <inheritdoc />
    public BoardApiService(
        IBoardService boardService,
        ITopicService topicService,
        BoardMapper mapper)
    {
        _boardService = boardService;
        _topicService = topicService;
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
        var mapped = _mapper.ToBoard(board);
        mapped.Viewer = ViewerOf(_topicService, board);
        return new Envelope<Board>(mapped);
    }

    /// <summary>
    /// What the viewer may do on the board, asked of the code that refuses the
    /// topic. Shared with the board a topic carries: the forum page takes that
    /// board for its own when a topic is opened first, and offers the new topic
    /// from it on the way back to the list.
    /// </summary>
    internal static BoardViewer ViewerOf(ITopicService topicService, DomainBoard board) => new()
    {
        CanCreateTopic = topicService.MayCreate(board),
        CanCreateTopicAfterSignIn = topicService.MayCreateAfterSignIn(board)
    };
}
