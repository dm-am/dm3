using System;
using System.Threading;
using System.Threading.Tasks;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.AttributeSchemaTemplates;

/// <summary>
/// API service for the catalogue of starting character sheets
/// </summary>
public interface IAttributeSchemaTemplateApiService
{
    /// <summary>
    /// Get every template, the default one first
    /// </summary>
    Task<ListEnvelope<AttributeSchemaTemplate>> Get(CancellationToken ct = default);

    /// <summary>
    /// Get one template
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task<Envelope<AttributeSchemaTemplate>> Get(Guid templateId, CancellationToken ct = default);

    /// <summary>
    /// Add a template to the catalogue
    /// </summary>
    /// <param name="request">Title and specifications</param>
    /// <param name="ct">Cancellation token</param>
    Task<Envelope<AttributeSchemaTemplate>> Create(
        AttributeSchemaTemplateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Replace the title and the specifications of a template
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="request">Title and specifications</param>
    /// <param name="ct">Cancellation token</param>
    Task<Envelope<AttributeSchemaTemplate>> Replace(
        Guid templateId, AttributeSchemaTemplateRequest request, CancellationToken ct = default);

    /// <summary>
    /// Hide a template
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task Delete(Guid templateId, CancellationToken ct = default);

    /// <summary>
    /// Make a template the one new games start from
    /// </summary>
    /// <param name="templateId">Template identifier</param>
    /// <param name="ct">Cancellation token</param>
    Task MakeDefault(Guid templateId, CancellationToken ct = default);
}
