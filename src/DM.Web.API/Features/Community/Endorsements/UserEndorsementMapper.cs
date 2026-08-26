using DM.Web.API.Features.Community.Users;
using Riok.Mapperly.Abstractions;
using DomainUserEndorsement = DM.Domain.Community.Features.UserEndorsements.UserEndorsement;

namespace DM.Web.API.Features.Community.Endorsements;

/// <summary>
/// Compile-time mapper for user endorsements. Author/TargetUser render
/// through the shared <see cref="UserMapper"/> - single source of truth for
/// avatar URLs.
/// </summary>
[Mapper]
internal partial class UserEndorsementMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public UserEndorsementMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain endorsement to its response DTO
    /// </summary>
    public partial UserEndorsement ToUserEndorsement(DomainUserEndorsement endorsement);
}
