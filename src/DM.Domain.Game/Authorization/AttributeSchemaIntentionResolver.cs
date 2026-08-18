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
            // Authorship, not game role, and deliberately so. The settings page
            // shows the schema editor next to the information form, which made
            // it look like the schema belongs to the game and should follow
            // GameIntention.EditSettings. It does not: a schema is its own
            // resource with its own author, PATCH names it by id and carries no
            // game, and a Public one is referenced by any number of games at
            // once. Delegating to the game intention would let the mentor of a
            // single game rewrite a schema every other game is built on, with
            // its author holding no say. Widening this needs a game-scoped
            // schema first, not a wider arm here.
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
