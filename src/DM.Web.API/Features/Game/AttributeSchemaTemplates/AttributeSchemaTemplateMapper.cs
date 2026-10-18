using DM.Web.API.Features.Game.AttributeSchemas;
using Riok.Mapperly.Abstractions;
using DomainTemplate = DM.Domain.Game.Features.AttributeSchemaTemplates.AttributeSchemaTemplate;
using DomainCreateTemplate = DM.Domain.Game.Features.AttributeSchemaTemplates.CreateAttributeSchemaTemplate;
using DomainReplaceTemplate = DM.Domain.Game.Features.AttributeSchemaTemplates.ReplaceAttributeSchemaTemplate;

namespace DM.Web.API.Features.Game.AttributeSchemaTemplates;

/// <summary>
/// Compile-time mapper for the catalogue of starting character sheets
/// </summary>
[Mapper]
internal partial class AttributeSchemaTemplateMapper
{
    [UseMapper]
    private readonly AttributeSchemaMapper _specificationMapper;

    public AttributeSchemaTemplateMapper(AttributeSchemaMapper specificationMapper)
    {
        _specificationMapper = specificationMapper;
    }

    /// <summary>
    /// Domain template to its response DTO
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial AttributeSchemaTemplate ToTemplate(DomainTemplate template);

    /// <summary>
    /// Request body to the domain creation command
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainCreateTemplate ToCreateTemplate(AttributeSchemaTemplateRequest request);

    /// <summary>
    /// Request body to the domain replacement command. The template comes from
    /// the route, the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainReplaceTemplate.TemplateId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainReplaceTemplate ToReplaceTemplate(AttributeSchemaTemplateRequest request);
}
