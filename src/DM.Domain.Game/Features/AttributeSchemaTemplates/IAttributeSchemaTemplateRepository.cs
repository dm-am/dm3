using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.AttributeSchemaTemplates;

/// <summary>
/// Repository for the catalogue of starting character sheets
/// </summary>
public interface IAttributeSchemaTemplateRepository
{
    #region Read

    /// <summary>
    /// Every template that is not hidden, the default one first and the rest by
    /// title
    /// </summary>
    /// <remarks>
    /// Carries the documents. The catalogue is small, the creation form needs
    /// the document of the default one the moment it opens, and the column is
    /// mapped as a document anyway, so counting attributes on the server would
    /// have loaded exactly the same bytes and cost the form a second request.
    /// </remarks>
    Task<IEnumerable<AttributeSchemaTemplate>> GetTemplates(CancellationToken ct = default);

    /// <summary>
    /// One template, or null when it is hidden or absent
    /// </summary>
    Task<AttributeSchemaTemplate?> GetTemplate(Guid templateId, CancellationToken ct = default);

    #endregion

    #region Write

    /// <summary>
    /// Add a template, handing every specification an identifier of its own
    /// </summary>
    /// <remarks>
    /// Throws <see cref="DM.Domain.Core.Exceptions.DuplicateEntityException" />
    /// when a live template already carries the title.
    /// </remarks>
    Task<AttributeSchemaTemplate> Create(
        CreateAttributeSchemaTemplate create, Guid createdByUserId, CancellationToken ct = default);

    /// <summary>
    /// Replace title and document, or null when the template is hidden or
    /// absent
    /// </summary>
    /// <remarks>
    /// Throws <see cref="DM.Domain.Core.Exceptions.DuplicateEntityException" />
    /// when another live template already carries the title.
    /// </remarks>
    Task<AttributeSchemaTemplate?> Replace(
        ReplaceAttributeSchemaTemplate replace, CancellationToken ct = default);

    /// <summary>
    /// Hide a template, unless it is the one new games start from
    /// </summary>
    Task<AttributeSchemaTemplateRemoval> Delete(Guid templateId, CancellationToken ct = default);

    /// <summary>
    /// Move the default flag onto this template, and off whichever one carries
    /// it now
    /// </summary>
    /// <returns>False when the template is hidden or absent, and nothing moved.</returns>
    /// <remarks>
    /// Throws <see cref="DM.Domain.Core.Exceptions.DuplicateEntityException" />
    /// when another assignment committed in between: the partial unique index is
    /// what serialises the two, and the loser has to look at the list again.
    /// </remarks>
    Task<bool> MakeDefault(Guid templateId, CancellationToken ct = default);

    #endregion
}
