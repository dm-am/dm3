using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DM.Architecture.Tests;

/// <summary>
/// The shipped types, loaded out of the directory the test binary runs from.
/// </summary>
/// <remarks>
/// Several rules of this tier are about what the product declares rather than
/// about what it says in a file — a hosted service starting synchronously, a job
/// without a period, a type holding an SMTP session. Each of them needs every
/// production type there is, and none of them can name the assemblies: the set
/// grows with the solution, and a list would go stale by staying green.
///
/// The build copies every project's output next to this one, so the directory is
/// the set. One spelling of the walk, because four copies of it existed and the
/// next rule would have copied a fifth.
/// </remarks>
internal static class ProductionAssemblies
{
    /// <summary>Every type the product declares, test assemblies left out.</summary>
    public static IEnumerable<Type> Types() => Directory
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
            // Not an assembly this runtime can load, so it declares nothing a rule
            // of this tier is about
            return Array.Empty<Type>();
        }
    }
}
