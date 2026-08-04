using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using FluentAssertions;
using MailKit;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// Whoever opens an SMTP session has to be able to close it.
/// </summary>
/// <remarks>
/// The mail processor is resolved from its own scope for every delivered letter
/// and opens its transport lazily on first use. MailKit ends a session on
/// Disconnect or Dispose and on nothing else, and a container releases only what
/// it can see as disposable: a holder that is neither IDisposable nor
/// IAsyncDisposable leaves one connected, authenticated session behind per letter,
/// alive until a finalizer happens to reach the socket. Relays cap concurrent
/// connections per address, so past the cap every further letter is retried, dead
/// lettered and never delivered — activation and password reset mail included.
///
/// A rule rather than one test, because the shape repeats: any type that holds a
/// MailKit service owns a socket only it can give back. It is the same slip the
/// producer rule next door exists for.
///
/// Reflection rather than the ArchUnit model used elsewhere in this suite: the
/// holder is internal to a worker executable this project cannot see at compile
/// time, and the transport hides inside a generic argument.
/// </remarks>
public class MailTransportOwnershipShould
{
    private static readonly IReadOnlyCollection<Type> TransportOwners = ProductionTypes()
        .Where(IsWritten)
        .Where(HoldsAMailTransport)
        .ToArray();

    /// <summary>
    /// Only types somebody wrote. Making the holder disposable gives its
    /// DisposeAsync a compiler-generated state machine that captures the same
    /// field and is itself neither disposable nor anyone's responsibility — so
    /// without this the rule reported the fix as the violation.
    /// </summary>
    private static bool IsWritten(Type type) =>
        type.GetCustomAttribute<CompilerGeneratedAttribute>() is null &&
        !type.Name.Contains('<', StringComparison.Ordinal);

    /// <summary>
    /// A rule that matches nothing passes.
    /// </summary>
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

    private static IEnumerable<Type> ProductionTypes() => Directory
        .EnumerateFiles(AppContext.BaseDirectory, "DM.*.dll", SearchOption.TopDirectoryOnly)
        .Where(path => !Path.GetFileNameWithoutExtension(path)
            .EndsWith(".Tests", StringComparison.Ordinal))
        .SelectMany(TypesOf);

    private static IEnumerable<Type> TypesOf(string assemblyPath)
    {
        try
        {
            return Assembly.LoadFrom(assemblyPath).GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            // One dependency that did not resolve must not hide the types that did
            return e.Types.Where(type => type is not null).Select(type => type!);
        }
        catch (Exception)
        {
            // Not an assembly this runtime can load, so it holds no transport
            return Array.Empty<Type>();
        }
    }

    private static bool HoldsAMailTransport(Type type)
    {
        try
        {
            return type
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Select(field => Unwrap(field.FieldType))
                .Any(fieldType => typeof(IMailService).IsAssignableFrom(fieldType));
        }
        catch (Exception)
        {
            // A field whose type cannot be loaded is not a MailKit service
            return false;
        }
    }

    // Deferred construction hides the transport from a plain field-type check, and
    // the session leaks exactly the same way behind either wrapper
    private static Type Unwrap(Type type) =>
        type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(Lazy<>) ||
                               type.GetGenericTypeDefinition() == typeof(Func<>))
            ? type.GetGenericArguments()[0]
            : type;
}
