using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DM.Architecture.Tests;

/// <summary>
/// The shipped types, loaded out of the directory the test binary runs from.
/// </summary>
/// <remarks>The build copies every project's output next to this one, so the directory is the set.</remarks>
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
    }
}
