using DM.Web.API.Features.Community.Users;
using Riok.Mapperly.Abstractions;
using DomainNote = DM.Domain.Moderation.Features.ProfileNotes.ModeratedProfileNote;

namespace DM.Web.API.Features.Moderation.Profiles;

/// <summary>
/// Compile-time mapper for moderator profile notes. The subject and the
/// author render through the shared <see cref="UserMapper"/>.
/// </summary>
[Mapper]
internal partial class ModeratedProfileNoteMapper
{
    [UseMapper]
    private readonly UserMapper _userMapper;

    public ModeratedProfileNoteMapper(UserMapper userMapper)
    {
        _userMapper = userMapper;
    }

    /// <summary>
    /// Domain note to its response DTO
    /// </summary>
    public partial ModeratedProfileNote ToNote(DomainNote note);
}
