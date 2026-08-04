using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AutoMapper;
using DM.Testing;
using DM.Web.API.Shared.Dto;
using FluentAssertions;
using Xunit;

namespace DM.Web.API.Tests;

/// <summary>
/// One configuration out of every profile there is, composed the way the
/// container composes it.
/// </summary>
/// <remarks>
/// Fifty-three files used to call AssertConfigurationIsValid over a hand-listed
/// subset of profiles, which meant a profile that gained a dependency had to be
/// added to each of them by hand, and a subset with a profile missing failed for
/// a reason that was not about the code. None of them could catch anything the
/// whole composition does not, and none of them composed what the application
/// composes: RegisterMapper sets AllowNullCollections, and the subsets did not.
///
/// The integration suite asserts the same thing over the container's own
/// configuration; that one proves the registration, this one proves the maps and
/// costs no container.
/// </remarks>
public class MappingProfileCompositionShould : UnitTestBase
{
    /// <summary>Every DM assembly reachable from the API, profiles included.</summary>
    private static IEnumerable<Assembly> ProductAssemblies()
    {
        var seen = new Dictionary<string, Assembly>(StringComparer.Ordinal);
        var queue = new Queue<Assembly>();
        queue.Enqueue(typeof(Envelope<int>).Assembly);

        while (queue.Count > 0)
        {
            var assembly = queue.Dequeue();
            if (!seen.TryAdd(assembly.GetName().Name!, assembly))
            {
                continue;
            }

            foreach (var reference in assembly.GetReferencedAssemblies()
                         .Where(name => name.Name!.StartsWith("DM.", StringComparison.Ordinal)))
            {
                queue.Enqueue(Assembly.Load(reference));
            }
        }

        return seen.Values;
    }

    [Fact]
    public void BeValidWithEveryProfileTheProductDeclares()
    {
        var profiles = ProductAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type is { IsAbstract: false, IsClass: true } &&
                           type.IsSubclassOf(typeof(Profile)))
            .ToArray();

        // Discovering none would assert an empty configuration and pass.
        profiles.Should().HaveCountGreaterThan(50,
            "the profiles are found by scanning, the same way the container finds them");

        var configuration = new MapperConfiguration(cfg =>
        {
            // The one option the registration sets, and the one the subsets
            // dropped: a null source collection stays null, which is how a PATCH
            // says "this field was not sent".
            cfg.AllowNullCollections = true;
            foreach (var profile in profiles)
            {
                cfg.AddProfile(profile);
            }
        });

        configuration.AssertConfigurationIsValid();
    }
}
