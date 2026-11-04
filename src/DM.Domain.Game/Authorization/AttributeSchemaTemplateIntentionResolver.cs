using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;

namespace DM.Domain.Game.Authorization;

/// <inheritdoc />
internal class AttributeSchemaTemplateIntentionResolver : IIntentionResolver<AttributeSchemaTemplateIntention>
{
    /// <inheritdoc />
    /// <remarks>
    /// Rank alone: a template belongs to the site, not to whoever typed it in.
    /// </remarks>
    public bool IsAllowed(IAuthorizationSubject user, AttributeSchemaTemplateIntention intention) =>
        intention switch
        {
            AttributeSchemaTemplateIntention.Manage => user.Role >= UserRole.SeniorModerator,
            _ => false
        };
}
