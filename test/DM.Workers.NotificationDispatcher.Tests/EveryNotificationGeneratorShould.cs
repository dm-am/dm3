using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DM.Domain.Core.Enums;
using DM.Domain.Personal.Features.Notifications;
using DM.Infrastructure.Persistence;
using DM.Workers.NotificationDispatcher.Implementation.Notifiers;
using FluentAssertions;
using Moq;
using Xunit;

namespace DM.Workers.NotificationDispatcher.Tests;

/// <summary>
/// Every generator is run, against the schema the migration builds.
/// </summary>
/// <remarks>
/// Four thousand lines of worker had no test that executed a line of it. The
/// generators are EF queries over the relational model and nothing else, so the
/// failure they can carry is the one the compiler cannot see: a projection naming
/// a column the migration does not create, a navigation the model no longer has,
/// an expression Npgsql refuses to translate. Every one of those is a run-time
/// exception inside a consumer whose retry middleware replays the whole delivery,
/// so the shape it takes in production is a queue that never drains and a feature
/// that produces nothing.
///
/// Enumerated from the assembly rather than listed, the way the dispatcher derives
/// its own binding: a generator added tomorrow is covered without anybody
/// remembering this file.
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
                : Substitute(parameter.ParameterType))
            .ToArray();

        return (INotificationGenerator)constructor.Invoke(arguments);
    }

    private static object Substitute(Type type)
    {
        var mock = (Mock)Activator.CreateInstance(typeof(Mock<>).MakeGenericType(type))!;
        mock.DefaultValue = DefaultValue.Empty;
        return mock.Object;
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
    /// <remarks>
    /// Two properties at once, and the second is why the first is asserted rather
    /// than merely awaited. The query has to reach the database and come back —
    /// that is the whole coverage this tier adds — and it has to come back empty,
    /// because the events carry no idempotency key and are redelivered after the
    /// subject has been deleted. A generator that answers a missing row with a
    /// notification addressed to Guid.Empty writes an inbox row nobody can open.
    /// </remarks>
    [Fact]
    public async Task AnswerAnEventAboutAMissingEntityWithNothing()
    {
        var generators = GeneratorTypes;
        generators.Should().HaveCountGreaterThan(20,
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
