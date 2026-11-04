using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence;
using DM.Workers.NotificationDispatcher.Notifiers;
using AwesomeAssertions;
using NSubstitute;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// Every generator is run, against the schema the migration builds.
/// </summary>
/// <remarks>
/// A projection naming a column the migration lacks fails only at run time, as a queue that never drains.
/// Enumerated from the assembly, as the dispatcher derives its binding.
/// </remarks>
[Collection(NotificationDatabaseCollection.Name)]
public class EveryNotificationGeneratorShould
{
    private readonly NotificationDatabaseFixture _fixture;

    public EveryNotificationGeneratorShould(NotificationDatabaseFixture fixture) => _fixture = fixture;

    internal static Type[] GeneratorTypes => typeof(Startup).Assembly
        .GetTypes()
        .Where(type => type is { IsAbstract: false, IsClass: true })
        .Where(typeof(INotificationGenerator).IsAssignableFrom)
        .OrderBy(type => type.FullName, StringComparer.Ordinal)
        .ToArray();

    /// <summary>
    /// Builds a generator the way the container builds it: the context is the real
    /// one, and a collaborator that is not a database is stood in for.
    /// </summary>
    internal static INotificationGenerator Construct(Type type, DmDbContext context)
    {
        var constructor = type.GetConstructors().Single();
        var arguments = constructor.GetParameters()
            .Select(parameter => parameter.ParameterType == typeof(DmDbContext)
                ? context
                : SubstituteFor(parameter.ParameterType))
            .ToArray();

        return (INotificationGenerator)constructor.Invoke(arguments);
    }

    /// <summary>A substitute for a constructor parameter the test does not care about.</summary>
    /// <remarks>The type is known at run time only, so the non-generic overload is used.</remarks>
    private static object SubstituteFor(Type type) => Substitute.For(new[] { type }, null);

    /// <summary>A clock that answers with the moment it was given.</summary>
    internal static IDateTimeProvider ClockAt(DateTimeOffset moment)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.Now.Returns(moment);
        return clock;
    }

    internal static async Task<List<CreateNotification>> DrainAsync(
        INotificationGenerator generator, Guid entityId)
    {
        var produced = new List<CreateNotification>();
        await foreach (var notification in generator.Generate(entityId))
        {
            produced.Add(notification);
        }

        return produced;
    }

    /// <summary>
    /// An event about an entity that is not there answers with nothing.
    /// </summary>
    /// <remarks>Events are redelivered after the subject is deleted, and an inbox row for Guid.Empty nobody opens.</remarks>
    [Fact]
    public async Task AnswerAnEventAboutAMissingEntityWithNothing()
    {
        var generators = GeneratorTypes;
        generators.Should().NotBeEmpty(
            "the dispatcher derives its queue binding from these types, and finding none " +
            "would leave this asserting nothing at all");

        await using var context = _fixture.CreateContext();
        var missing = Guid.NewGuid();

        var failures = new List<string>();
        var executed = 0;

        foreach (var type in generators)
        {
            var generator = Construct(type, context);
            foreach (var eventType in Enum.GetValues<EventType>().Where(generator.CanResolve))
            {
                executed++;
                try
                {
                    var produced = await DrainAsync(generator, missing);
                    if (produced.Count != 0)
                    {
                        failures.Add($"{type.Name} answered {eventType} about a missing entity " +
                                     $"with {produced.Count} notification(s)");
                    }
                }
                catch (Exception exception)
                {
                    failures.Add($"{type.Name} threw on {eventType}: " +
                                 $"{exception.GetType().Name}: {exception.Message}");
                }
            }
        }

        executed.Should().BeGreaterThan(generators.Length,
            "some generators answer several events, and every pair has to be run");
        failures.Should().BeEmpty(
            "an exception here is a delivery the retry middleware replays forever, and a " +
            "notification here is an inbox row about an entity that no longer exists");
    }
}
