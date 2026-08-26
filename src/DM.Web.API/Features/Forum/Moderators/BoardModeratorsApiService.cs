using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Boards;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Forum.Moderators;

/// <inheritdoc />
internal class BoardModeratorsApiService : IBoardModeratorsApiService
{
    private readonly IBoardService _boardService;
    private readonly UserMapper _mapper;

    /// <inheritdoc />
    public BoardModeratorsApiService(
        IBoardService boardService,
        UserMapper mapper)
    {
        _boardService = boardService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<User>> GetModerators(string id)
    {
        var moderators = await _boardService.GetModerators(id);
        return new ListEnvelope<User>(moderators.Select(_mapper.ToUser));
    }

    /// <inheritdoc />
    public async Task<Envelope<User>> AddModerator(string id, string username)
    {
        var user = await _boardService.AddModerator(id, username);
        return new Envelope<User>(_mapper.ToUser(user));
    }

    /// <inheritdoc />
    public async Task RemoveModerator(string id, string username)
    {
        await _boardService.RemoveModerator(id, username);
    }
}
