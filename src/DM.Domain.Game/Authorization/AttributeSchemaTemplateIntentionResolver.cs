using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc />
internal class AttributeSchemaTemplateIntentionResolver : IIntentionResolver<AttributeSchemaTemplateIntention>
{
    /// <inheritdoc />
    /// <remarks>
    /// Rank and nothing else, and no target: a template belongs to the site
    /// rather than to whoever typed it in, so there is no row whose author
    /// could widen the answer. The role compared here is the acting one, which
    /// is what a session without a second factor lowers.
    /// </remarks>
    public bool IsAllowed(IAuthorizationSubject user, AttributeSchemaTemplateIntention intention) =>
        intention switch
        {
            AttributeSchemaTemplateIntention.Manage => user.Role >= UserRole.SeniorModerator,
            _ => false
        };
}
