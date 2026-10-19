using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DM.Domain.Account;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Infrastructure.Core;
using DM.Infrastructure.Core.Configuration;
using DM.Infrastructure.Core.Extensions;
using DM.Infrastructure.Persistence;
using DM.Tools.Seeder.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace DM.Tools.Seeder;

/// <summary>
/// Development data seeder, outside the API so that it is never reachable over HTTP.
/// </summary>
internal static class Program
{
    private const string UsersCommand = "users";
    private const string ContentCommand = "content";
    private const string AllCommand = "all";

    private static async Task<int> Main(string[] args)
    {
        var command = args.Length > 0 ? args[0].ToLowerInvariant() : AllCommand;
        if (command is "-h" or "--help" or "help")
        {
            PrintUsage();
            return 0;
        }

        if (command is not (AllCommand or UsersCommand or ContentCommand))
        {
            Console.Error.WriteLine($"Unknown command '{args[0]}'.");
            PrintUsage();
            return 2;
        }

        try
        {
            if (command is AllCommand or UsersCommand)
            {
                PrintUsers(await RunPhase(seeder => seeder.SeedTestUsers()));
            }

            // The content seed reads the users, so on "all" it runs second.
            if (command is AllCommand or ContentCommand)
            {
                PrintContent(await RunPhase(seeder => seeder.SeedComprehensiveData()));
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Seeding failed: {exception}");
            return 1;
        }
    }

    /// <summary>
    /// One phase on a host of its own, so that "all" lays out what "users" and then "content" lay out
    /// in two processes, as the stand scripts seed.
    /// </summary>
    private static async Task<T> RunPhase<T>(Func<DataSeeder, Task<T>> phase)
    {
        // Args are commands: Host.CreateDefaultBuilder would reject a bare positional argument.
        using var host = CreateHostBuilder().Build();
        await using var scope = host.Services.CreateAsyncScope();
        return await phase(scope.ServiceProvider.GetRequiredService<DataSeeder>());
    }

    /// <summary>
    /// The tool's composition, in one place so that the container it runs on is
    /// the container a test can build.
    /// </summary>
    internal static IHostBuilder CreateHostBuilder() => Host
        .CreateDefaultBuilder()
        // Scope validation only: the domain scans bring in API services this tool never resolves.
        // CompositionRootShould resolves every dependency of DataSeeder against this container instead.
        .UseDefaultServiceProvider(options => options.ValidateScopes = true)
        .WithDmConfiguration()
        .ConfigureServices((context, services) => services
            .AddOptions()
            .AddDmCoreConfiguration(context.Configuration)
            // The account assembly reads these sections, and IOptions of an unbound one hands out empty defaults.
            .AddDmAccountConfiguration(context.Configuration)
            .RequireRelationalStorage()
            .RequireObjectStorage()
            .AddDbContext<DmDbContext>(options => options.UseNpgsql(
                context.Configuration.GetConnectionString(nameof(ConnectionStrings.Rdb)),
                npgsql => npgsql.CommandTimeout(120)))
            // After the explicit registrations: the scans only fill gaps.
            .AddDmCore()
            .AddDmPersistence()
            // The hashing and the popularity processors are internal to their modules, so the scan picks them up.
            .AddDefaultTypes(typeof(ISecurityManager).Assembly)
            .AddDefaultTypes(typeof(DM.Domain.Game.Authorization.GameIntention).Assembly)
            .AddDefaultTypes(typeof(DM.Domain.Blog.Authorization.BlogIntention).Assembly)
            // Last, because the final registration wins. Singletons, so a resolve does not restart the stream.
            .AddSingleton<SeedDeterminism>()
            .AddSingleton<IGuidFactory, SeededGuidFactory>()
            .AddScoped<DataSeeder>());

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet DM.Tools.Seeder.dll [all|users|content]");
        Console.WriteLine("  users    base development accounts");
        Console.WriteLine("  content  forums, games, blogs, chats, reviews, polls");
        Console.WriteLine("  all      users, then content (default)");
        Console.WriteLine();
        Console.WriteLine("DM_SeedEpochUtc  ISO-8601 instant every seeded date is offset from, e.g.");
        Console.WriteLine("                 2026-06-15T12:00:00Z. Unset: the real clock, which is what");
        Console.WriteLine("                 keeps a development site looking alive. Pin it when the same");
        Console.WriteLine("                 dates have to come out of two different runs.");
        Console.WriteLine();
        Console.WriteLine("DM_SeedRandomSeed  integer the randomness and the identifiers are drawn");
        Console.WriteLine("                 from. Unset: a fixed default, so two runs already agree.");
        Console.WriteLine("                 Pin it to lay out a different, equally repeatable fixture.");
    }

    private static void PrintUsers(SeedResult result)
    {
        // Zeroes omitted, as on the content line.
        var counters = new (string Label, int Value)[]
        {
            ("created", result.Created),
            ("skipped", result.Skipped),
        };

        Console.WriteLine($"users: {Summarize(counters)}");
        if (result.CreatedUsernames.Count > 0)
        {
            Console.WriteLine($"  created: {string.Join(", ", result.CreatedUsernames)}");
        }

        if (result.SkippedUsernames.Count > 0)
        {
            Console.WriteLine($"  skipped: {string.Join(", ", result.SkippedUsernames)}");
        }
    }

    private static void PrintContent(ComprehensiveSeedResult result)
    {
        // Zeroes omitted: on a reseed almost everything is skipped.
        var counters = new (string Label, int Value)[]
        {
            ("games", result.GamesCreated),
            ("posts", result.PostsCreated),
            ("chars", result.CharactersCreated),
            ("topics", result.TopicsCreated),
            ("comments", result.CommentsCreated),
            ("blogs", result.BlogsCreated),
            ("pubs", result.PublicationsCreated),
            ("msgs", result.MessagesCreated),
            ("polls", result.PollsCreated),
            ("reviews", result.ReviewsCreated),
            ("testimonials", result.TestimonialsCreated),
            ("likes", result.LikesCreated),
            ("board moderators", result.BoardModeratorsAssigned),
            ("notifications", result.NotificationsCreated),
        };

        Console.WriteLine($"content: {Summarize(counters)}");
        foreach (var detail in result.Details)
        {
            Console.WriteLine($"  {detail}");
        }
    }

    /// <summary>The counters that moved, joined; the ones that did not are left out.</summary>
    private static string Summarize(IEnumerable<(string Label, int Value)> counters)
    {
        var created = counters
            .Where(counter => counter.Value > 0)
            .Select(counter => $"{counter.Value} {counter.Label}")
            .ToList();

        return created.Count > 0
            ? string.Join(", ", created)
            : "nothing new (already seeded)";
    }
}
