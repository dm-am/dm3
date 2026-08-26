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
/// Development data seeder. Lives outside the API on purpose: it writes straight
/// to Postgres and the object storage, so it must never be reachable over
/// HTTP. Connection strings come from the same DM_* environment variables the
/// application workloads read.
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
            // Args are commands, not configuration: Host.CreateDefaultBuilder would
            // reject a bare positional argument as an unrecognized switch.
            using var host = CreateHostBuilder().Build();
            await using var scope = host.Services.CreateAsyncScope();

            var seeder = scope.ServiceProvider.GetRequiredService<DataSeeder>();

            if (command is AllCommand or UsersCommand)
            {
                PrintUsers(await seeder.SeedTestUsers());
            }

            // The content seed reads the users it attaches everything to, so on
            // "all" it always runs second.
            if (command is AllCommand or ContentCommand)
            {
                PrintContent(await seeder.SeedComprehensiveData());
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
    /// The tool's composition, in one place so that the container it runs on is
    /// the container a test can build.
    /// </summary>
    /// <remarks>
    /// Internal rather than private for exactly that: the tool composes inside its
    /// entry point rather than in a Startup class, so the architecture suite could
    /// read this file as text and never resolve anything out of it. A dependency
    /// the container cannot supply is not a build error here — it surfaces on a
    /// developer's machine as a seeding run that dies before it writes a row.
    /// </remarks>
    internal static IHostBuilder CreateHostBuilder() => Host
        .CreateDefaultBuilder()
        // Scope validation only. ValidateOnBuild is deliberately off here,
        // alone among the executables: the tool scans three domain assemblies
        // for a handful of internal types - the hashing, the popularity
        // processors - and the sweep brings in services wired for the API
        // (mail senders, the HIBP client, the realtime push) that this tool
        // never resolves and whose dependencies it has no business registering.
        // What the tool does resolve is guarded twice instead: the composition
        // rule resolves every constructor dependency of DataSeeder against
        // this container, and a seeding run is part of the local gates.
        .UseDefaultServiceProvider(options => options.ValidateScopes = true)
        .WithDmConfiguration()
        .ConfigureServices((context, services) => services
            .AddOptions()
            .AddDmCoreConfiguration(context.Configuration)
            // The scans below sweep the whole account assembly, whose types read
            // four option sections. IOptions of an unbound type hands out a default
            // rather than throwing, so the encryption key would have been empty at the
            // first call that needed it instead of missing at startup.
            .AddDmAccountConfiguration(context.Configuration)
            .RequireRelationalStorage()
            .RequireObjectStorage()
            .AddDbContext<DmDbContext>(options => options.UseNpgsql(
                context.Configuration.GetConnectionString(nameof(ConnectionStrings.Rdb)),
                npgsql => npgsql.CommandTimeout(120)))
            // The DI modules, after everything the tool wires explicitly: their
            // scans only fill gaps, so the DbContext above must already be on
            // the collection when they run.
            .AddDmCore()
            .AddDmPersistence()
            // Password hashing lives in the Account domain and its implementation is
            // internal, so the assembly scan is what picks it up. The same goes for
            // the two popularity processors: the fixture scores its games and blogs
            // by the site's definition rather than by a copy of it, and the classes
            // that hold that definition are internal to their modules.
            .AddDefaultTypes(typeof(ISecurityManager).Assembly)
            .AddDefaultTypes(typeof(DM.Domain.Game.Authorization.GameIntention).Assembly)
            .AddDefaultTypes(typeof(DM.Domain.Blog.Authorization.BlogIntention).Assembly)
            // Last, and deliberately last: MS.DI hands a single resolution to
            // the final registration, so this is what replaces the
            // infrastructure Guid.NewGuid() factory for the tool and only for
            // the tool. Single instances, because a per-dependency generator
            // restarts its stream on every resolve and hands out the same
            // identifiers twice.
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
        // Zeroes omitted, the way the content line next door already does it.
        // "22 created, 0 skipped" spends half its width saying that nothing was
        // skipped, on a line read after every run, and the two summaries of one
        // command read differently for no reason.
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
        // Same counters the HTTP endpoint returned, zeroes omitted: on a reseed
        // almost everything is skipped and the interesting part is what is not.
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
