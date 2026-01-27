using System;
using DM.Services.MessageQueuing.GeneralBus;
using FluentAssertions;
using Xunit;

namespace DM.Services.MessageQueuing.Tests;

public class InvokedEventExceptionShould
{
    [Fact]
    public void BeCreatedWithMessage()
    {
        var message = "Test error message";
        var exception = new InvokedEventException(message);

        exception.Message.Should().Be(message);
    }

    [Fact]
    public void InheritFromException()
    {
        var exception = new InvokedEventException("test");

        exception.Should().BeAssignableTo<Exception>();
    }

    [Fact]
    public void HaveCorrectMessageForMissingAttribute()
    {
        var message = "Отсутствует аттрибут EventRoutingKeyAttribute";
        var exception = new InvokedEventException(message);

        exception.Message.Should().Contain("EventRoutingKeyAttribute");
    }

    [Fact]
    public void HaveCorrectMessageForMissingEnumName()
    {
        var message = "Отсутствует имя перечисления Unknown";
        var exception = new InvokedEventException(message);

        exception.Message.Should().Contain("перечисления");
    }
}
