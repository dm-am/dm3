using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using AwesomeAssertions;
using MailKit;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// MailKit ends a session on Disconnect or Dispose only, and a container releases only what is
/// disposable. Reflection: the holder is internal to a worker this project cannot see.
/// </remarks>
public class MailTransportOwnershipShould
{
    private static readonly IReadOnlyCollection<Type> TransportOwners = ProductionAssemblies.Types()
        .Where(IsWritten)
        .Where(HoldsAMailTransport)
        .ToArray();

    // Only types somebody wrote: DisposeAsync's generated state machine captures the same field.
    private static bool IsWritten(Type type) =>
        type.GetCustomAttribute<CompilerGeneratedAttribute>() is null &&
        !type.Name.Contains('<', StringComparison.Ordinal);

    // A rule that matches nothing passes.
    [Fact]
    public void FindTheTransportOwners() =>
        TransportOwners.Should().NotBeEmpty(
            "the mail worker holds a MailKit transport, so an empty match means the " +
            "assemblies were never loaded and the rule below checks nothing");

    [Fact]
    public void KeepEveryTransportOwnerDisposable() =>
        TransportOwners
            .Where(type => !typeof(IDisposable).IsAssignableFrom(type) &&
                           !typeof(IAsyncDisposable).IsAssignableFrom(type))
            .Select(type => type.FullName)
            .Should().BeEmpty(
                "an SMTP session is closed by Disconnect or Dispose and by nothing " +
                "else, so a holder the container cannot release leaks one open, " +
                "authenticated session per letter until the relay refuses the next " +
                "connection and mail stops being delivered at all");

    private static bool HoldsAMailTransport(Type type)
    {
        return type
            .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(field => Unwrap(field.FieldType))
            .Any(fieldType => typeof(IMailService).IsAssignableFrom(fieldType));
    }

    // Deferred construction hides the transport from a plain field-type check, and
    // the session leaks exactly the same way behind either wrapper
    private static Type Unwrap(Type type) =>
        type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Lazy<>) ||
                               type.GetGenericTypeDefinition() == typeof(Func<>))
            ? type.GetGenericArguments()[0]
            : type;
}
