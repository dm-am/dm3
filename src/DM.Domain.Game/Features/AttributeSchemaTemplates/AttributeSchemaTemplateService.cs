using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using FluentValidation;

namespace DM.Domain.Game.Features.AttributeSchemaTemplates;

/// <inheritdoc />
internal class AttributeSchemaTemplateService : IAttributeSchemaTemplateService
{
    private readonly IIntentionManager _intentionManager;
    private readonly IAttributeSchemaTemplateRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly IValidator<CreateAttributeSchemaTemplate> _createValidator;
    private readonly IValidator<ReplaceAttributeSchemaTemplate> _replaceValidator;

    public AttributeSchemaTemplateService(
        IIntentionManager intentionManager,
        IAttributeSchemaTemplateRepository repository,
        IIdentityProvider identityProvider,
        IValidator<CreateAttributeSchemaTemplate> createValidator,
        IValidator<ReplaceAttributeSchemaTemplate> replaceValidator)
    {
        _intentionManager = intentionManager;
        _repository = repository;
        _identityProvider = identityProvider;
        _createValidator = createValidator;
        _replaceValidator = replaceValidator;
    }

    /// <inheritdoc />
    /// <remarks>
    /// No gate: the catalogue is what the creation form offers, and the form is
    /// open to anyone who can reach the page behind it.
    /// </remarks>
    public Task<IEnumerable<AttributeSchemaTemplate>> GetAllAsync(CancellationToken ct = default) =>
        _repository.GetTemplates(ct);

    /// <inheritdoc />
    public async Task<AttributeSchemaTemplate> GetAsync(Guid templateId, CancellationToken ct = default) =>
        await _repository.GetTemplate(templateId, ct)
            ?? throw new HttpException(
                HttpStatusCode.NotFound, RefusalMessage.AttributeSchemaTemplateNotFound);

    /// <inheritdoc />
    /// <remarks>
    /// The rank is asked before the body is read. The attribute on the action
    /// answers the same question at the edge; this one answers it for every
    /// caller of the service, so a second door to the catalogue cannot be opened
    /// by forgetting an attribute.
    /// </remarks>
    public async Task<AttributeSchemaTemplate> CreateAsync(
        CreateAttributeSchemaTemplate create, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);
        await _createValidator.ValidateAndThrowAsync(create, ct);

        try
        {
            return await _repository.Create(create, _identityProvider.Current.User.UserId, ct);
        }
        catch (DuplicateEntityException)
        {
            throw TitleTaken(create.Title);
        }
    }

    /// <inheritdoc />
    public async Task<AttributeSchemaTemplate> ReplaceAsync(
        ReplaceAttributeSchemaTemplate replace, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);
        await _replaceValidator.ValidateAndThrowAsync(replace, ct);

        AttributeSchemaTemplate? replaced;
        try
        {
            replaced = await _repository.Replace(replace, ct);
        }
        catch (DuplicateEntityException)
        {
            throw TitleTaken(replace.Title);
        }

        return replaced ?? throw new HttpException(
            HttpStatusCode.NotFound, RefusalMessage.AttributeSchemaTemplateNotFound);
    }

    /// <inheritdoc />
    /// <remarks>
    /// The site is never left without a template to start from, so the removal
    /// of the default one is refused rather than performed and repaired.
    /// </remarks>
    public async Task DeleteAsync(Guid templateId, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);

        switch (await _repository.Delete(templateId, ct))
        {
            case AttributeSchemaTemplateRemoval.Hidden:
                return;
            case AttributeSchemaTemplateRemoval.RefusedAsDefault:
                throw new HttpException(
                    HttpStatusCode.Conflict, "Сначала назначьте другой шаблон по умолчанию");
            default:
                throw new HttpException(
                    HttpStatusCode.NotFound, RefusalMessage.AttributeSchemaTemplateNotFound);
        }
    }

    /// <inheritdoc />
    public async Task MakeDefaultAsync(Guid templateId, CancellationToken ct = default)
    {
        _intentionManager.ThrowIfForbidden(AttributeSchemaTemplateIntention.Manage);

        bool assigned;
        try
        {
            assigned = await _repository.MakeDefault(templateId, ct);
        }
        catch (DuplicateEntityException)
        {
            throw new HttpException(
                HttpStatusCode.Conflict, "Шаблон по умолчанию только что назначили. Обновите список");
        }

        if (!assigned)
        {
            throw new HttpException(
                HttpStatusCode.NotFound, RefusalMessage.AttributeSchemaTemplateNotFound);
        }
    }

    /// <summary>
    /// One title, one template: a second one under the same name is
    /// indistinguishable in the list the creation form offers.
    /// </summary>
    private static HttpException TitleTaken(string title) =>
        new HttpException(HttpStatusCode.Conflict, $"Шаблон схемы \"{title}\" уже есть");
}
