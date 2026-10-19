using System;
using System.IO;
using ArchUnitNET.Loader;
using ArchitectureModel = ArchUnitNET.Domain.Architecture;

namespace DM.Architecture.Tests;

/// <summary>The assemblies of the solution as ArchUnit reads them, loaded once for every rule over the IL.</summary>
internal static class ArchitectureSolution
{
    internal static readonly ArchitectureModel Model = new ArchLoader()
        .LoadFilteredDirectory(AppContext.BaseDirectory, "DM.*.dll", SearchOption.TopDirectoryOnly)
        .Build();
}
