using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DM.Domain.Game.Features.AttributeSchemaTemplates;
using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Features.Game.AttributeSchemaTemplates;

public class AttributeSchemaTemplateApiService
{
    private readonly IAttributeSchemaTemplateService _templateService;
    private readonly AttributeSchemaTemplateMapper _mapper;

    public AttributeSchemaTemplateApiService(
        IAttributeSchemaTemplateService templateService,
        AttributeSchemaTemplateMapper mapper)
    {
        _templateService = templateService;
        _mapper = mapper;
    }

    public async Task<ListEnvelope<AttributeSchemaTemplate>> Get(CancellationToken ct = default)
    {
        var templates = await _templateService.GetAllAsync(ct);
        return new ListEnvelope<AttributeSchemaTemplate>(templates.Select(_mapper.ToTemplate));
    }

    public async Task<Envelope<AttributeSchemaTemplate>> Get(
        Guid templateId, CancellationToken ct = default)
    {
        var template = await _templateService.GetAsync(templateId, ct);
        return new Envelope<AttributeSchemaTemplate>(_mapper.ToTemplate(template));
    }

    public async Task<Envelope<AttributeSchemaTemplate>> Create(
        AttributeSchemaTemplateRequest request, CancellationToken ct = default)
    {
        var created = await _templateService.CreateAsync(_mapper.ToCreateTemplate(request), ct);
        return new Envelope<AttributeSchemaTemplate>(_mapper.ToTemplate(created));
    }

    public async Task<Envelope<AttributeSchemaTemplate>> Replace(
        Guid templateId, AttributeSchemaTemplateRequest request, CancellationToken ct = default)
    {
        var replace = _mapper.ToReplaceTemplate(request);
        replace.TemplateId = templateId;
        var replaced = await _templateService.ReplaceAsync(replace, ct);
        return new Envelope<AttributeSchemaTemplate>(_mapper.ToTemplate(replaced));
    }

    public Task Delete(Guid templateId, CancellationToken ct = default) =>
        _templateService.DeleteAsync(templateId, ct);

    public Task MakeDefault(Guid templateId, CancellationToken ct = default) =>
        _templateService.MakeDefaultAsync(templateId, ct);
}
