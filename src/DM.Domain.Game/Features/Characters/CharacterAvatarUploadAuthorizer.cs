using System;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Uploads;
using DM.Domain.Game.Authorization;

namespace DM.Domain.Game.Features.Characters;

/// <inheritdoc />
internal class CharacterAvatarUploadAuthorizer : PublicUploadTargetAuthorizer
{
    private readonly ICharacterRepository _repository;
    private readonly IIntentionManager _intentionManager;

    public CharacterAvatarUploadAuthorizer(
        ICharacterRepository repository,
        IIntentionManager intentionManager)
    {
        _repository = repository;
        _intentionManager = intentionManager;
    }

    /// <inheritdoc />
    public override UploadType Type => UploadType.CharacterAvatar;

    /// <summary>
    /// Changing a character's portrait is editing the character.
    /// </summary>
    /// <remarks>
    /// The intention CharacterService.UpdateAsync asks, over the same projection, so the portrait
    /// opens exactly when the sheet does. Existence is settled before the image reaches the bucket.
    /// </remarks>
    public override async Task EnsureAllowedAsync(Guid targetId)
    {
        var character = await _repository.GetForUpdateOrThrow(targetId);
        _intentionManager.ThrowIfForbidden(CharacterIntention.Edit, character);
    }
}
