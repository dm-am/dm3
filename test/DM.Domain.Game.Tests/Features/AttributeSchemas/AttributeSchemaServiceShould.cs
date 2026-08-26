using System;
using System.Net;
using System.Threading.Tasks;
using DM.Domain.Core.Authorization;
using DM.Domain.Core.Enums;
using DM.Domain.Core.Exceptions;
using DM.Domain.Core.Identity;
using DM.Domain.Game.Authorization;
using DM.Domain.Game.Features.AttributeSchemas;
using DM.Domain.Game.Features.Games;
using DM.Testing.Dsl;
using DM.Testing;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Domain.Game.Tests.Features.AttributeSchemas;

public class AttributeSchemaServiceShould : UnitTestBase
{
    private readonly IIntentionManager _intentionManager;
    private readonly IAttributeSchemaRepository _repository;
    private readonly IIdentityProvider _identityProvider;
    private readonly AttributeSchemaService _service;

    public AttributeSchemaServiceShould()
    {
        _intentionManager = Mock<IIntentionManager>();

        _repository = Mock<IAttributeSchemaRepository>();

        _identityProvider = Mock<IIdentityProvider>();
        var userId = Guid.NewGuid();
        _identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));

        _service = new AttributeSchemaService(
            _intentionManager,
            _repository,
            _identityProvider,
            new CreateAttributeSchemaValidator(),
            new UpdateAttributeSchemaValidator());
    }

    [Fact]
    public async Task AuthorizeCreateSchemaAction()
    {
        var createSchema = new CreateAttributeSchema { Title = "Test Schema" };
        var schema = new AttributeSchema { Id = Guid.NewGuid() };
        _repository.Create(Arg.Any<CreateAttributeSchema>(), Arg.Any<Guid>()).Returns(schema);

        await _service.CreateAsync(createSchema);

        _intentionManager.Received(1).ThrowIfForbidden(GameIntention.Create);
    }

    [Fact]
    public async Task CreateSchemaWithCurrentUser()
    {
        var userId = Guid.NewGuid();
        _identityProvider.Current.Returns(Identities.User(userId, UserRole.RegularUser));
        var createSchema = new CreateAttributeSchema { Title = "Test Schema" };
        var schema = new AttributeSchema { Id = Guid.NewGuid() };
        _repository.Create(createSchema, userId).Returns(schema);

        var result = await _service.CreateAsync(createSchema);

        result.Should().Be(schema);
        await _repository.Received(1).Create(createSchema, userId);
    }

    [Fact]
    public async Task ThrowNotFoundWhenSchemaDoesNotExist()
    {
        var schemaId = Guid.NewGuid();
        _repository.GetSchema(schemaId).Returns((AttributeSchema?)null);

        var act = async () => await _service.GetAsync(schemaId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AuthorizeUpdateSchemaAction()
    {
        var schemaId = Guid.NewGuid();
        var updateSchema = new UpdateAttributeSchema { SchemaId = schemaId, Title = "Updated Schema" };
        var oldSchema = new AttributeSchema { Id = schemaId };
        var updatedSchema = new AttributeSchema { Id = schemaId };
        _repository.GetSchema(schemaId).Returns(oldSchema);
        _repository.Update(updateSchema).Returns(updatedSchema);

        await _service.UpdateAsync(updateSchema);

        _intentionManager.Received(1).ThrowIfForbidden(AttributeSchemaIntention.Edit, oldSchema);
    }

    [Fact]
    public async Task AuthorizeDeleteSchemaAction()
    {
        var schemaId = Guid.NewGuid();
        var schema = new AttributeSchema { Id = schemaId };
        _repository.GetSchema(schemaId).Returns(schema);
        _repository.Delete(schemaId).Returns(Task.CompletedTask);

        await _service.DeleteAsync(schemaId);

        _intentionManager.Received(1).ThrowIfForbidden(AttributeSchemaIntention.Delete, schema);
    }

    /// <summary>
    /// A removed schema disappears from the lists a master picks from, so nothing can
    /// refuse this delete: the service is the only place the reference exists.
    /// A public schema is anyone's to build a game on, which makes the game that
    /// breaks somebody else's.
    /// </summary>
    [Fact]
    public async Task RefuseToDeleteASchemaSomeGameStillReferences()
    {
        var schemaId = Guid.NewGuid();
        var schema = new AttributeSchema { Id = schemaId };
        _repository.GetSchema(schemaId).Returns(schema);
        _repository.IsUsedByAnyGame(schemaId).Returns(true);

        var act = async () => await _service.DeleteAsync(schemaId);

        await act.Should().ThrowAsync<HttpException>()
            .Where(e => e.StatusCode == HttpStatusCode.Conflict);
        await _repository.DidNotReceive().Delete(Arg.Any<Guid>());
    }

    [Fact]
    public async Task DeleteASchemaNoGameReferences()
    {
        var schemaId = Guid.NewGuid();
        _repository.GetSchema(schemaId).Returns(new AttributeSchema { Id = schemaId });
        _repository.IsUsedByAnyGame(schemaId).Returns(false);

        await _service.DeleteAsync(schemaId);

        await _repository.Received(1).Delete(schemaId);
    }
}
