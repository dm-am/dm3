using System;

namespace DM.Infrastructure.Messaging.GeneralBus;

/// <inheritdoc />
public class InvokedEventException : Exception
{
    /// <inheritdoc />
    public InvokedEventException(string message) : base(message)
    {
    }
}