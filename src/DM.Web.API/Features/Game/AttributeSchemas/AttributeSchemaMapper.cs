using System;
using Riok.Mapperly.Abstractions;
using DomainAttributeSpecification = DM.Domain.Game.Features.AttributeSchemas.AttributeSpecification;
using DomainCreateSpecification = DM.Domain.Game.Features.AttributeSchemas.CreateAttributeSpecification;
using DomainUpdateSpecification = DM.Domain.Game.Features.AttributeSchemas.UpdateAttributeSpecification;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <summary>
/// Compile-time mapper for one attribute specification, in every direction the
/// host needs it.
/// </summary>
/// <remarks>
/// The sheet of a game and a template of the catalogue carry the same
/// specifications, and both of their mappers reach these through
/// <c>[UseMapper]</c>: a second spelling of the conversion would be a second
/// answer about what a specification is.
/// </remarks>
[Mapper]
public partial class AttributeSchemaMapper
{
    // The create command drops the not yet existing Id.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial AttributeSpecification ToSpecification(DomainAttributeSpecification specification);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainCreateSpecification ToCreateSpecification(AttributeSpecification specification);

    [MapProperty(
        nameof(AttributeSpecification.Id),
        nameof(DomainUpdateSpecification.Id),
        Use = nameof(ToOptionalSpecificationId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainUpdateSpecification ToUpdateSpecification(AttributeSpecification specification);

    // A zero Guid denotes a brand new specification (no persisted id yet);
    // the domain reads null as "create" and an id as "keep this one if the
    // stored sheet still holds it".
    [UserMapping(Default = false)]
    private static Guid? ToOptionalSpecificationId(Guid id) => id == Guid.Empty ? null : id;
}
