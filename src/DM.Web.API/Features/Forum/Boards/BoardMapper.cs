using DM.Web.API.Features.Community.Users;
using DM.Web.API.Shared.BbRendering;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using DomainBoard = DM.Domain.Forum.Features.Boards.Board;

namespace DM.Web.API.Features.Forum.Boards;

/// <summary>
/// Compile-time mapper from service DTO to API DTO for boards. Authors of
/// the last topic and comment render through the shared user mappers.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(BbTextMappers))]
[UseStaticMapper(typeof(UserRefMappers))]
internal partial class BoardMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public BoardMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain board to its response DTO, last topic and comment included.
    /// Policies and moderator ids stay behind - the response answers what a
    /// reader sees, not how the board is governed.
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial Board ToBoard(DomainBoard board);
}
