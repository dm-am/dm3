using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Uploads;
using DM.Domain.Game.Authorization;

namespace DM.Domain.Game.Features.Characters;

/// <inheritdoc />
internal class CharacterAvatarUploadAuthorizer : IUploadTargetAuthorizer
{
    private readonly ICharacterRepository _repository;
    private readonly IIntentionManager _intentionManager;

    /// <inheritdoc />
    public CharacterAvatarUploadAuthorizer(
        ICharacterRepository repository,
        IIntentionManager intentionManager)
    {
        _repository = repository;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public UploadType Type => UploadType.CharacterAvatar;

    /// <summary>
    /// Changing a character's portrait is editing the character.
    /// </summary>
    /// <remarks>
    /// The same intention CharacterService.UpdateAsync goes through, over the same
    /// projection, so the owner, the master and the assistants get the portrait
    /// exactly when they get the rest of the sheet — including the parts that are
    /// easy to miss stated separately: an owner cannot edit while the game is not
    /// active, and a master needs either an NPC or the character's EditAllowed flag.
    ///
    /// Existence is settled here rather than left to the write: before this check
    /// the same request answered 500 from the foreign key, and only after the
    /// image had been written to the bucket.
    /// </remarks>
    public async Task EnsureAllowedAsync(Guid targetId)
    {
        var character = await _repository.GetForUpdate(targetId);
        if (character == null)
        {
            throw new HttpException(HttpStatusCode.NotFound, RefusalMessage.CharacterNotFound);
        }

        _intentionManager.ThrowIfForbidden(CharacterIntention.Edit, character);
    }
}
