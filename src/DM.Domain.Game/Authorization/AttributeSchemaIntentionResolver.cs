using DM.Domain.Game.Features.Games;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
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

            // SchemaType carries the rule: Public is everyone's to use, Private
            // is the author's alone. A system schema has no author at all, so
            // its type is the only thing that can open it.
            AttributeSchemaIntention.Use =>
                target.Type == SchemaType.Public || target.Author?.UserId == user.UserId,

            _ => false
        };
}
