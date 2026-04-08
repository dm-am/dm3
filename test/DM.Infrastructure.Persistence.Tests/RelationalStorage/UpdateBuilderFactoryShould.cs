using System;
using System.ComponentModel.DataAnnotations;
using DM.Infrastructure.Persistence.RelationalStorage;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Persistence.Tests.RelationalStorage;

public class UpdateBuilderFactoryShould
{
    private readonly UpdateBuilderFactory _factory;

    public UpdateBuilderFactoryShould()
    {
        _factory = new UpdateBuilderFactory();
    }

    [Fact]
    public void CreateUpdateBuilderForEntity()
    {
        var entityId = Guid.NewGuid();

        var result = _factory.Create<TestEntity>(entityId);

        result.Should().NotBeNull();
        result.Should().BeAssignableTo<IUpdateBuilder<TestEntity>>();
    }

    [Fact]
    public void CreateUpdateBuilderWithoutChanges()
    {
        var entityId = Guid.NewGuid();

        var result = _factory.Create<TestEntity>(entityId);

        result.HasChanges().Should().BeFalse();
    }

    [Fact]
    public void CreateUpdateBuilderThatSupportsFieldUpdates()
    {
        var entityId = Guid.NewGuid();

        var builder = _factory.Create<TestEntity>(entityId);
        builder.Field(e => e.Name, "Test");

        builder.HasChanges().Should().BeTrue();
    }

    [Fact]
    public void CreateMultipleUpdateBuilders()
    {
        var entityId1 = Guid.NewGuid();
        var entityId2 = Guid.NewGuid();

        var builder1 = _factory.Create<TestEntity>(entityId1);
        var builder2 = _factory.Create<TestEntity>(entityId2);

        builder1.Should().NotBeSameAs(builder2);
    }

    private class TestEntity
    {
        [Key]
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
