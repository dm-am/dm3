using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using DM.Testing;
using FluentValidation;
using FluentValidation.Validators;

namespace DM.Architecture.Tests;

/// <remarks>
/// The browser cannot ask the server for a bound, so the client spells it once and a gate holds
/// the spelling to the server. A member the server keeps private or internal is read by name:
/// a rename fails the gate instead of passing it.
/// </remarks>
internal static class ClientCopies
{
    public static string Source(string file) =>
        File.ReadAllText(Path.Combine(
            [RepositoryLayout.Root, "src", "DM.Web.Client", "src", .. file.Split('/')]));

    /// <summary>The number a client constant is declared with, products of numbers included.</summary>
    public static long Number(string file, string constant)
    {
        var match = Regex.Match(Source(file),
            @"\bconst\s+" + Regex.Escape(constant) + @"(?:\s*:\s*\w+)?\s*=\s*(?<value>[\d_\s*]+);");
        if (!match.Success)
        {
            throw new InvalidOperationException($"{file} declares no numeric {constant}");
        }

        return match.Groups["value"].Value
            .Split('*', StringSplitOptions.TrimEntries)
            .Select(factor => long.Parse(factor.Replace("_", "", StringComparison.Ordinal), CultureInfo.InvariantCulture))
            .Aggregate(1L, (product, factor) => product * factor);
    }

    public static int Stated(string file, string pattern)
    {
        var match = Regex.Match(Source(file), pattern);
        if (!match.Success)
        {
            throw new InvalidOperationException($"{file} has no text matching {pattern}");
        }

        return int.Parse(match.Groups["n"].Value, CultureInfo.InvariantCulture);
    }

    public static int Setting(string section, string key)
    {
        using var settings = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, "src", "DM.Web.API", "appsettings.json")));
        return settings.RootElement.GetProperty(section).GetProperty(key).GetInt32();
    }

    /// <summary>A constant, field or property of a server type, whatever its visibility.</summary>
    public static T Member<T>(Assembly assembly, string type, string member)
    {
        var owner = assembly.GetType(type, throwOnError: true)!;
        const BindingFlags any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;

        var field = owner.GetField(member, any);
        if (field != null)
        {
            return (T)(field.IsLiteral ? field.GetRawConstantValue()! : field.GetValue(Instance(owner, field.IsStatic))!);
        }

        var property = owner.GetProperty(member, any)
            ?? throw new InvalidOperationException($"{type} has no {member}");
        return (T)property.GetValue(Instance(owner, property.GetMethod!.IsStatic))!;
    }

    private static object? Instance(Type owner, bool isStatic) =>
        isStatic ? null : Activator.CreateInstance(owner, nonPublic: true);

    public static int Maximum(Type contract, string property)
    {
        var member = Property(contract, property);
        return member.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength
               ?? member.GetCustomAttribute<MaxLengthAttribute>()?.Length
               ?? throw new InvalidOperationException($"{contract.Name}.{property} states no maximum");
    }

    public static int Minimum(Type contract, string property)
    {
        var member = Property(contract, property);
        return member.GetCustomAttribute<StringLengthAttribute>()?.MinimumLength
               ?? member.GetCustomAttribute<MinLengthAttribute>()?.Length
               ?? throw new InvalidOperationException($"{contract.Name}.{property} states no minimum");
    }

    public static int ValidatorMaximum(Assembly assembly, string validatorType, string member)
    {
        var validator = (IValidator)Activator.CreateInstance(
            assembly.GetType(validatorType, throwOnError: true)!, nonPublic: true)!;

        return validator.CreateDescriptor().GetValidatorsForMember(member)
            .Select(rule => rule.Validator)
            .OfType<ILengthValidator>()
            .Single().Max;
    }

    private static PropertyInfo Property(Type contract, string property) =>
        contract.GetProperty(property) ?? throw new InvalidOperationException($"{contract.Name} has no {property}");
}
