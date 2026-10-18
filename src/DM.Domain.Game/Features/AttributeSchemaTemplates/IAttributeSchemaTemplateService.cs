using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DM.Domain.Game.Features.AttributeSchemaTemplates;

/// <summary>
/// Service owning the catalogue of starting character sheets
/// </summary>
public interface IAttributeSchemaTemplateService
{
    /// <summary>
    /// Every template that is not hidden, the default one first
    /// </summary>
    Task<IEnumerable<AttributeSchemaTemplate>> GetAllAsync(CancellationToken ct = default);

    /// <summary>
    /// One template by identifier
    /// </summary>
    Task<AttributeSchemaTemplate> GetAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>
    /// Add a template to the catalogue
    /// </summary>
    Task<AttributeSchemaTemplate> CreateAsync(
        CreateAttributeSchemaTemplate create, CancellationToken ct = default);

    /// <summary>
    /// Replace the title and the document of a template
    /// </summary>
    Task<AttributeSchemaTemplate> ReplaceAsync(
        ReplaceAttributeSchemaTemplate replace, CancellationToken ct = default);

    /// <summary>
    /// Hide a template
    /// </summary>
    Task DeleteAsync(Guid templateId, CancellationToken ct = default);

    /// <summary>
    /// Make this template the one new games start from
    /// </summary>
    Task MakeDefaultAsync(Guid templateId, CancellationToken ct = default);
}
