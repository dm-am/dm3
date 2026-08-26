using System;
using DM.Web.API.Shared.Dto;
using Riok.Mapperly.Abstractions;
using DomainAttributeSchema = DM.Domain.Game.Features.Games.AttributeSchema;
using DomainAttributeSpecification = DM.Domain.Game.Features.Games.AttributeSpecification;
using DomainCreateSchema = DM.Domain.Game.Features.Games.CreateAttributeSchema;
using DomainCreateSpecification = DM.Domain.Game.Features.Games.CreateAttributeSpecification;
using DomainUpdateSchema = DM.Domain.Game.Features.Games.UpdateAttributeSchema;
using DomainUpdateSpecification = DM.Domain.Game.Features.Games.UpdateAttributeSpecification;

namespace DM.Web.API.Features.Game.AttributeSchemas;

/// <summary>
/// Compile-time mapper for attribute schemata. An unsent specification list
/// stays null on the write path - null is "not sent", not "clear" - which is
/// what AllowNullCollections said under AutoMapper.
/// </summary>
[Mapper]
[UseStaticMapper(typeof(UserRefMappers))]
internal partial class AttributeSchemaMapper
{
    /// <summary>
    /// Domain schema to its response DTO
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial AttributeSchema ToSchema(DomainAttributeSchema schema);

    /// <summary>
    /// Schema DTO to the domain create command. Id and author stay behind:
    /// the id does not exist yet and the author is the caller.
    /// </summary>
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainCreateSchema ToCreateSchema(AttributeSchema schema);

    /// <summary>
    /// Update request to the domain command. SchemaId comes from the route,
    /// the service sets it.
    /// </summary>
    [MapperIgnoreTarget(nameof(DomainUpdateSchema.SchemaId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    public partial DomainUpdateSchema ToUpdateSchema(UpdateAttributeSchemaRequest request);

    // The read row drops Description, the create command drops the not yet
    // existing Id; both were target-validated narrowings under AutoMapper.
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial AttributeSpecification ToSpecification(DomainAttributeSpecification specification);

    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial DomainCreateSpecification ToCreateSpecification(AttributeSpecification specification);

    [MapProperty(
        nameof(AttributeSpecification.Id),
        nameof(DomainUpdateSpecification.Id),
        Use = nameof(ToOptionalSpecificationId))]
    [MapperRequiredMapping(RequiredMappingStrategy.Target)]
    private partial DomainUpdateSpecification ToUpdateSpecification(AttributeSpecification specification);

    // A zero Guid denotes a brand new specification (no persisted id yet);
    // the domain reads null as "create" and an id as "update in place".
    [UserMapping(Default = false)]
    private static Guid? ToOptionalSpecificationId(Guid id) => id == Guid.Empty ? null : id;
}
