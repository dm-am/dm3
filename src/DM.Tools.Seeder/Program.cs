using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using DM.Domain.Account;
using DM.Domain.Account.Features.Security;
using DM.Domain.Core.Abstractions;
using DM.Domain.Core.Configuration;
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
/// to Postgres, Mongo and the object storage, so it must never be reachable over
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

    private static IHostBuilder CreateHostBuilder() => Host
        .CreateDefaultBuilder()
        .UseServiceProviderFactory(new AutofacServiceProviderFactory())
        .WithDmConfiguration()
        .ConfigureServices((context, services) => services
            .AddOptions()
            .AddDmCoreConfiguration(context.Configuration)
            // The container below scans the whole account assembly, whose types read
            // four option sections. IOptions of an unbound type hands out a default
            // rather than throwing, so the encryption key would have been empty at the
            // first call that needed it instead of missing at startup.
            .AddDmAccountConfiguration(context.Configuration)
            .RequireRelationalStorage()
            .RequireDocumentStorage()
            .RequireObjectStorage()
            .AddDbContext<DmDbContext>(options => options.UseNpgsql(
                context.Configuration.GetConnectionString(nameof(ConnectionStrings.Rdb)),
                npgsql => npgsql.CommandTimeout(120))))
        .ConfigureContainer<ContainerBuilder>(builder =>
        {
            builder.RegisterModuleOnce<CoreModule>();
            builder.RegisterModuleOnce<PersistenceModule>();

            // Password hashing lives in the Account domain and its implementation is
            // internal, so the assembly scan is what picks it up.
            builder.RegisterDefaultTypes(typeof(ISecurityManager).Assembly);

            // Last, and deliberately last: Autofac takes the final registration
            // as the default, so this is what replaces the infrastructure
            // Guid.NewGuid() factory for the tool and only for the tool. Single
            // instances, because a per-dependency generator restarts its stream
            // on every resolve and hands out the same identifiers twice.
            builder.RegisterType<SeedDeterminism>()
                .AsSelf()
                .SingleInstance();

            builder.RegisterType<SeededGuidFactory>()
                .As<IGuidFactory>()
                .SingleInstance();

            builder.RegisterType<DataSeeder>()
                .AsSelf()
                .InstancePerLifetimeScope();
        });

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
        Console.WriteLine($"users: {result.Created} created, {result.Skipped} skipped");
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

        var created = counters
            .Where(counter => counter.Value > 0)
            .Select(counter => $"{counter.Value} {counter.Label}")
            .ToList();

        Console.WriteLine($"content: {Summarize(created)}");
        foreach (var detail in result.Details)
        {
            Console.WriteLine($"  {detail}");
        }
    }

    private static string Summarize(IReadOnlyCollection<string> created) => created.Count > 0
        ? string.Join(", ", created)
        : "nothing new (already seeded)";
}
