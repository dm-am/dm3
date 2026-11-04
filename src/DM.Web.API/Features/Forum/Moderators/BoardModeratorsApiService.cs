using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Forum.Features.Boards;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Forum.Moderators;

public class BoardModeratorsApiService
{
    private readonly IBoardService _boardService;
    private readonly UserMapper _mapper;

    public BoardModeratorsApiService(
        IBoardService boardService,
        UserMapper mapper)
    {
        _boardService = boardService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<User>> GetModerators(string id)
    {
        var moderators = await _boardService.GetModerators(id);
        return new ListEnvelope<User>(moderators.Select(_mapper.ToUser));
    }

    public async Task<Envelope<User>> AddModerator(string id, string username)
    {
        var user = await _boardService.AddModerator(id, username);
        return new Envelope<User>(_mapper.ToUser(user));
    }

    public Task RemoveModerator(string id, string username) => _boardService.RemoveModerator(id, username);
}
