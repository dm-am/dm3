using System;
using DM.Services.Core.Dto.Enums;
using DM.Services.MessageQueuing.GeneralBus;
using FluentAssertions;
using Xunit;

namespace DM.Services.MessageQueuing.Tests;

public class InvokedEventShould
{
    [Fact]
    public void SetTypeProperty()
    {
        var invokedEvent = new InvokedEvent
        {
            Type = EventType.NewForumTopic
        };

        invokedEvent.Type.Should().Be(EventType.NewForumTopic);
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

        var forumEvent = new InvokedEvent { Type = EventType.NewForumTopic, EntityId = entityId };
        var gameEvent = new InvokedEvent { Type = EventType.NewGame, EntityId = entityId };
        var userEvent = new InvokedEvent { Type = EventType.NewUser, EntityId = entityId };

        forumEvent.Type.Should().Be(EventType.NewForumTopic);
        gameEvent.Type.Should().Be(EventType.NewGame);
        userEvent.Type.Should().Be(EventType.NewUser);
    }
}
