using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Authorization;
using DM.Domain.Game.Features.AttributeSchemas;

namespace DM.Domain.Game.Authorization;
/// <inheritdoc />
internal class AttributeSchemaIntentionResolver : IIntentionResolver<AttributeSchemaIntention, AttributeSchema>
{
    /// <inheritdoc />
    public bool IsAllowed(IAuthorizationSubject user, AttributeSchemaIntention intention, AttributeSchema target) =>
        intention switch
        {
            AttributeSchemaIntention.Edit => target.Author?.UserId == user.UserId,
            AttributeSchemaIntention.Delete => target.Author?.UserId == user.UserId,
            _ => false
        };
}
