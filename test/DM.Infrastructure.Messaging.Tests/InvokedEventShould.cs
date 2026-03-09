using System;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Messaging.GeneralBus;
using FluentAssertions;
using Xunit;

namespace DM.Infrastructure.Messaging.Tests;

public class InvokedEventShould
{
    [Fact]
    public void SetTypeProperty()
    {
        var invokedEvent = new InvokedEvent
        {
            Type = EventType.NewTopic
        };

        invokedEvent.Type.Should().Be(EventType.NewTopic);
    }

    [Fact]
    public void SetEntityIdProperty()
    {
        var entityId = Guid.NewGuid();
        var invokedEvent = new InvokedEvent
        {
            EntityId = entityId
        };

        invokedEvent.EntityId.Should().Be(entityId);
    }

    [Fact]
    public void SetBothPropertiesCorrectly()
    {
        var entityId = Guid.NewGuid();
        var eventType = EventType.NewCharacter;

        var invokedEvent = new InvokedEvent
        {
            Type = eventType,
            EntityId = entityId
        };

        invokedEvent.Type.Should().Be(eventType);
        invokedEvent.EntityId.Should().Be(entityId);
    }

    [Fact]
    public void AcceptDifferentEventTypes()
    {
        var entityId = Guid.NewGuid();

        var forumEvent = new InvokedEvent { Type = EventType.NewTopic, EntityId = entityId };
        var gameEvent = new InvokedEvent { Type = EventType.NewGame, EntityId = entityId };
        var userEvent = new InvokedEvent { Type = EventType.NewUser, EntityId = entityId };

        forumEvent.Type.Should().Be(EventType.NewTopic);
        gameEvent.Type.Should().Be(EventType.NewGame);
        userEvent.Type.Should().Be(EventType.NewUser);
    }
}
