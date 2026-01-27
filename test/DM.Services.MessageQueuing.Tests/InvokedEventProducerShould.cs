using DM.Services.MessageQueuing.GeneralBus;
using Xunit;

namespace DM.Services.MessageQueuing.Tests;

/// <summary>
/// Tests for InvokedEventProducer.
/// Note: InvokedEventProducer uses extension methods (BuildRabbit) which are difficult to mock.
/// Integration tests would be more appropriate for full testing of this class.
/// </summary>
public class InvokedEventProducerShould
{
    [Fact]
    public void ExistAsInternalClass()
    {
        // This test verifies that the InvokedEventProducer class exists and is accessible
        // via InternalsVisibleTo attribute for testing purposes
        var type = typeof(InvokedEventProducer);
        Assert.NotNull(type);
        Assert.Equal("InvokedEventProducer", type.Name);
    }
}
