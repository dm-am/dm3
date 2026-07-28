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
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Domain.Game.Tests.Features.AttributeSchemas;

public class AttributeSchemaServiceShould : UnitTestBase
{
    private readonly Mock<IIntentionManager> _intentionManager;
    private readonly Mock<IAttributeSchemaRepository> _repository;
    private readonly Mock<IIdentityProvider> _identityProvider;
    private readonly AttributeSchemaService _service;

    public AttributeSchemaServiceShould()
    {
        _intentionManager = Mock<IIntentionManager>();
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<GameIntention>()));
        _intentionManager.Setup(m => m.ThrowIfForbidden(It.IsAny<AttributeSchemaIntention>(), It.IsAny<AttributeSchema>()));

        _repository = Mock<IAttributeSchemaRepository>();

        _identityProvider = Mock<IIdentityProvider>();
        var userId = Guid.NewGuid();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(userId, UserRole.RegularUser));

        _service = new AttributeSchemaService(
            _intentionManager.Object,
            _repository.Object,
            _identityProvider.Object,
            new CreateAttributeSchemaValidator(),
            new UpdateAttributeSchemaValidator());
    }

    [Fact]
    public async Task AuthorizeCreateSchemaAction()
    {
        var createSchema = new CreateAttributeSchema { Title = "Test Schema" };
        var schema = new AttributeSchema { Id = Guid.NewGuid() };
        _repository.Setup(r => r.Create(It.IsAny<CreateAttributeSchema>(), It.IsAny<Guid>()))
            .ReturnsAsync(schema);

        await _service.CreateAsync(createSchema);

        _intentionManager.Verify(m => m.ThrowIfForbidden(GameIntention.Create), Times.Once);
    }

    [Fact]
    public async Task CreateSchemaWithCurrentUser()
    {
        var userId = Guid.NewGuid();
        _identityProvider.Setup(p => p.Current).Returns(Identities.User(userId, UserRole.RegularUser));
        var createSchema = new CreateAttributeSchema { Title = "Test Schema" };
        var schema = new AttributeSchema { Id = Guid.NewGuid() };
        _repository.Setup(r => r.Create(createSchema, userId))
            .ReturnsAsync(schema);

        var result = await _service.CreateAsync(createSchema);

        result.Should().Be(schema);
        _repository.Verify(r => r.Create(createSchema, userId), Times.Once);
    }

    [Fact]
    public async Task ThrowNotFoundWhenSchemaDoesNotExist()
    {
        var schemaId = Guid.NewGuid();
        _repository.Setup(r => r.GetSchema(schemaId)).ReturnsAsync((AttributeSchema?)null);

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
        _repository.Setup(r => r.GetSchema(schemaId)).ReturnsAsync(oldSchema);
        _repository.Setup(r => r.Update(updateSchema)).ReturnsAsync(updatedSchema);

        await _service.UpdateAsync(updateSchema);

        _intentionManager.Verify(m => m.ThrowIfForbidden(AttributeSchemaIntention.Edit, oldSchema), Times.Once);
    }

    [Fact]
    public async Task AuthorizeDeleteSchemaAction()
    {
        var schemaId = Guid.NewGuid();
        var schema = new AttributeSchema { Id = schemaId };
        _repository.Setup(r => r.GetSchema(schemaId)).ReturnsAsync(schema);
        _repository.Setup(r => r.Delete(schemaId)).Returns(Task.CompletedTask);

        await _service.DeleteAsync(schemaId);

        _intentionManager.Verify(m => m.ThrowIfForbidden(AttributeSchemaIntention.Delete, schema), Times.Once);
    }
}
