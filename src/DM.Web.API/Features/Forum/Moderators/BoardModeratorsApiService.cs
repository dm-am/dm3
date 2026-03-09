using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using DM.Domain.Forum.Features.Boards;
using DM.Web.API.Shared.Dto;
using DM.Web.API.Features.Community.Users;

namespace DM.Web.API.Features.Forum.Moderators;

/// <inheritdoc />
internal class BoardModeratorsApiService : IBoardModeratorsApiService
{
    private readonly IBoardService _boardService;
    private readonly IMapper _mapper;

    /// <inheritdoc />
    public BoardModeratorsApiService(
        IBoardService boardService,
        IMapper mapper)
    {
        _boardService = boardService;
        _mapper = mapper;
    }

    /// <inheritdoc />
    public async Task<ListEnvelope<User>> GetModerators(string id)
    {
        var moderators = await _boardService.GetModerators(id);
        return new ListEnvelope<User>(moderators.Select(_mapper.Map<User>));
    }
}
