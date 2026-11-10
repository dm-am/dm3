using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DM.Architecture.Tests;

// The build copies every project's output next to this one, so the directory is the set.
internal static class ProductionAssemblies
{
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
