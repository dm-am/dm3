using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AwesomeAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// One filter is called by one name on every endpoint that takes it.
/// </summary>
/// <remarks>
/// Eighty-one distinct query parameters were published across nine documents,
/// and several of them were the same question asked under different words: the
/// two search endpoints sitting in one folder took the search string as
/// <c>query</c> and as <c>q</c> while thirteen other lists already called it
/// <c>search</c>; /v1/posts bounded a date with <c>createdAfter</c> where eight
/// neighbours used <c>createdFromUtc</c>; /v1/uploads paged with
/// <c>number</c>/<c>size</c> alone against twenty-eight lists on
/// <c>skip</c>/<c>take</c>.
///
/// The cost is not aesthetic. A query parameter the server does not know is not
/// refused, it is ignored — so a consumer carrying a filter over from the
/// neighbouring list gets 200 and the unfiltered set, which reads as "the filter
/// found everything" rather than as an error.
///
/// Read from the assembly rather than from the sources: the wire name is what
/// the binder derives from a parameter or a bound property, and deriving it
/// again from text would be a second implementation of the same rule. The
/// retired names are a list because neither of them is something the code could
/// re-derive; the other two rules are derived from the type, which is why they
/// hold for parameters nobody has written yet.
/// </remarks>
public class QueryVocabularyShould
{
    /// <summary>A query parameter as it appears on the wire, and where it came from.</summary>
    private sealed record WireParameter(string Name, Type Type, bool IsCollection, string Origin);

    /// <summary>
    /// A name the API retired, and the name it kept. Matching is exact and
    /// case-insensitive: the binder is case-insensitive too.
    /// </summary>
    private static readonly (string Retired, string Instead)[] RetiredNames =
    [
        ("q", "search — the free-text filter on every list endpoint"),
        ("query", "search"),
        ("filter", "the name of what is being filtered (statuses, role, activity)"),
        ("text", "search"),
        ("number", "skip — the page number is converted by the caller"),
        ("size", "take"),
        ("page", "skip"),
        ("pageSize", "take"),
        ("offset", "skip"),
        ("count", "take, or limit next to a cursor"),
        ("sort", "sortBy"),
        ("order", "sortOrder"),
        ("orderBy", "sortBy"),
        ("user", "username"),
        ("login", "username"),
        ("author", "authorUsername"),
        ("authors", "authorUsernames"),
        ("after", "<field>FromUtc, or cursor for a keyset page"),
        ("before", "<field>ToUtc"),
    ];

    /// <summary>
    /// Types the model binder reads straight from one query value instead of
    /// walking their properties. Everything else bound from the query is a
    /// container whose properties are the parameters.
    /// </summary>
    private static bool IsSimple(Type type)
    {
        var t = Nullable.GetUnderlyingType(type) ?? type;
        return t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal) ||
               t == typeof(DateTime) || t == typeof(DateTimeOffset) || t == typeof(TimeSpan) ||
               t == typeof(Guid) || t == typeof(Uri);
    }

    /// <summary>The element type of a bound sequence, or null when it is not one.</summary>
    private static Type? ElementOf(Type type)
    {
        if (type == typeof(string))
        {
            return null;
        }

        if (type.IsArray)
        {
            return type.GetElementType();
        }

        return type.GetInterfaces().Concat([type])
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            .Select(i => i.GetGenericArguments()[0])
            .FirstOrDefault();
    }

    /// <summary>Query parameter name of a member, honouring an explicit binder name.</summary>
    private static string WireName(string declared, BindingSource? explicitName) =>
        explicitName?.Name ?? char.ToLowerInvariant(declared[0]) + declared[1..];

    /// <summary>An explicitly given binder name, as the attributes state it.</summary>
    private sealed record BindingSource(string Name);

    private static BindingSource? ExplicitName(ICustomAttributeProvider member)
    {
        var name = member.GetCustomAttributes(typeof(FromQueryAttribute), inherit: true)
            .Cast<FromQueryAttribute>().FirstOrDefault()?.Name;
        return string.IsNullOrEmpty(name) ? null : new BindingSource(name);
    }

    /// <summary>Placeholders of the route this action answers, constraints stripped.</summary>
    /// <remarks>
    /// The controller's own [Route] carries the tokens shared by every action on
    /// it, and an action template starting with '~' replaces it rather than
    /// extending it — both matter, because a parameter named by either binds
    /// from the path and never from the query.
    /// </remarks>
    private static ISet<string> RouteTokens(Type controller, MethodInfo action)
    {
        var templates = action.GetCustomAttributes<HttpMethodAttribute>(inherit: true)
            .Select(a => a.Template)
            .Where(t => !string.IsNullOrEmpty(t))
            .Concat(controller.GetCustomAttributes<RouteAttribute>(inherit: true).Select(a => a.Template))
            .ToArray();

        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var template in templates)
        {
            foreach (System.Text.RegularExpressions.Match match in
                     System.Text.RegularExpressions.Regex.Matches(template!, @"\{([^}:?=]+)"))
            {
                tokens.Add(match.Groups[1].Value.TrimStart('*'));
            }
        }

        return tokens;
    }

    /// <summary>
    /// Every query parameter the API publishes, with the endpoint it belongs to.
    /// </summary>
    private static IReadOnlyCollection<WireParameter> Published()
    {
        var controllers = typeof(DM.Web.API.Startup).Assembly.GetTypes()
            .Where(t => t is { IsAbstract: false, IsPublic: true } &&
                        typeof(ControllerBase).IsAssignableFrom(t))
            .ToArray();

        controllers.Should().NotBeEmpty("the API assembly must expose controllers to read");

        var found = new List<WireParameter>();

        foreach (var controller in controllers)
        {
            foreach (var action in controller.GetMethods(BindingFlags.Public | BindingFlags.Instance |
                                                         BindingFlags.DeclaredOnly))
            {
                if (!action.GetCustomAttributes<HttpMethodAttribute>(inherit: true).Any())
                {
                    continue;
                }

                var origin = $"{controller.Name}.{action.Name}";
                var routeTokens = RouteTokens(controller, action);

                foreach (var parameter in action.GetParameters())
                {
                    // Only what the query binds. A body, a route value, a header,
                    // a form field and an injected service are other sources; a
                    // cancellation token is not bound at all. A name the route
                    // template holds is a path segment: the path has a vocabulary
                    // of its own and this rule is not it.
                    if (parameter.GetCustomAttributes(inherit: true).Any(a =>
                            a is FromBodyAttribute or FromRouteAttribute or FromHeaderAttribute or
                                FromFormAttribute or FromServicesAttribute) ||
                        parameter.ParameterType.FullName == "System.Threading.CancellationToken" ||
                        parameter.ParameterType.Namespace?.StartsWith("Microsoft.AspNetCore.Http") == true ||
                        routeTokens.Contains(parameter.Name!))
                    {
                        continue;
                    }

                    var explicitName = ExplicitName(parameter);
                    var type = parameter.ParameterType;

                    if (IsSimple(type) || Nullable.GetUnderlyingType(type) != null || ElementOf(type) != null)
                    {
                        var element = ElementOf(type);
                        if (element != null && !IsSimple(element))
                        {
                            continue;
                        }

                        found.Add(new WireParameter(
                            WireName(parameter.Name!, explicitName),
                            element ?? type,
                            element != null,
                            origin));
                        continue;
                    }

                    // A container: its properties are the parameters. Inherited
                    // ones included — skip/take arrive through PagingQuery.
                    foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
                    {
                        if (property.GetIndexParameters().Length > 0 || property.SetMethod == null)
                        {
                            continue;
                        }

                        var element = ElementOf(property.PropertyType);
                        if (!IsSimple(property.PropertyType) && element == null)
                        {
                            continue;
                        }

                        if (element != null && !IsSimple(element))
                        {
                            continue;
                        }

                        found.Add(new WireParameter(
                            WireName(property.Name, ExplicitName(property)),
                            element ?? property.PropertyType,
                            element != null,
                            $"{origin} ({type.Name}.{property.Name})"));
                    }
                }
            }
        }

        return found;
    }

    [Fact]
    public void UseNoRetiredParameterName()
    {
        var published = Published();
        published.Should().NotBeEmpty("the API takes query parameters");

        var offences = new List<string>();

        foreach (var (retired, instead) in RetiredNames)
        {
            foreach (var parameter in published.Where(p =>
                         string.Equals(p.Name, retired, StringComparison.OrdinalIgnoreCase)))
            {
                offences.Add($"{parameter.Origin} takes '{parameter.Name}'; the vocabulary says {instead}");
            }
        }

        offences.Should().BeEmpty(
            "API_DESIGN.md fixes one name per filter, and an unknown parameter is ignored rather " +
            "than refused — so a consumer that guesses the neighbour's name gets an unfiltered 200:" +
            Environment.NewLine + string.Join(Environment.NewLine, offences));
    }

    [Fact]
    public void NameEveryDateBoundForTheClockItIsIn()
    {
        var offences = Published()
            .Where(p => (Nullable.GetUnderlyingType(p.Type) ?? p.Type) is var t &&
                        (t == typeof(DateTime) || t == typeof(DateTimeOffset)))
            .Where(p => !p.Name.EndsWith("Utc", StringComparison.Ordinal))
            .Select(p => $"{p.Origin} takes '{p.Name}'")
            .ToArray();

        offences.Should().BeEmpty(
            "a date bound is '<field>FromUtc'/'<field>ToUtc' (API_DESIGN.md) — the suffix is what " +
            "tells the caller which clock the value is read in, and 'createdAfter' told them nothing:" +
            Environment.NewLine + string.Join(Environment.NewLine, offences));
    }

    [Fact]
    public void GiveOneNameOneArity()
    {
        var offences = Published()
            .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Select(p => p.IsCollection).Distinct().Count() > 1)
            .Select(g => $"'{g.Key}' is a list on {string.Join(", ", g.Where(p => p.IsCollection).Select(p => p.Origin))} " +
                         $"and a single value on {string.Join(", ", g.Where(p => !p.IsCollection).Select(p => p.Origin))}")
            .ToArray();

        offences.Should().BeEmpty(
            "the number of the name is the number of the values (API_DESIGN.md). One name with two " +
            "encodings is the worst case of the drift: the consumer copies the name from the " +
            "neighbour, gets it right, and still sends a value the binder reads as something else:" +
            Environment.NewLine + string.Join(Environment.NewLine, offences));
    }

    /// <summary>
    /// One filter, one name — read from the type the filter is written in.
    /// </summary>
    /// <remarks>
    /// The rows of the vocabulary table the other three facts cannot reach. The
    /// drift that opened this finding was between the two mirrored module lists:
    /// /v1/games took <c>statuses</c> and /v1/blogs took <c>status</c>, and
    /// <c>premoderationStatuses</c> against <c>premoderationStatus</c> the same
    /// way. Neither pair shares a name, so <see cref="GiveOneNameOneArity" />
    /// sees nothing, and neither name is retired anywhere, so
    /// <see cref="UseNoRetiredParameterName" /> sees nothing either. Left as it
    /// was, the table in API_DESIGN.md would have stated a rule for the two rows
    /// the finding is about and nothing would have held them.
    ///
    /// An enum is the one query type that says which filter it is: a
    /// <c>ModuleStatus</c> is the status of a module wherever it is taken, so the
    /// endpoints that take it agree on a name or one of them is wrong. Strings
    /// and numbers carry no such meaning — <c>string</c> is every filter at once
    /// — which is why the rule is enum-only rather than a rule about all types.
    /// </remarks>
    [Fact]
    public void GiveOneFilterOneName()
    {
        var offences = Published()
            .Where(p => (Nullable.GetUnderlyingType(p.Type) ?? p.Type).IsEnum)
            .GroupBy(p => Nullable.GetUnderlyingType(p.Type) ?? p.Type)
            .Where(g => g.Select(p => p.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            .Select(g => $"{g.Key.Name} is taken as " + string.Join(", ", g
                .GroupBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .Select(n => $"'{n.Key}' on {string.Join(" and ", n.Select(p => p.Origin))}")))
            .ToArray();

        offences.Should().BeEmpty(
            "one filter is called by one name on every endpoint that takes it (API_DESIGN.md). " +
            "The mirrored lists are where this breaks first, and an unknown parameter is ignored " +
            "rather than refused, so the consumer that carries the neighbour's name over is told " +
            "nothing and reads the unfiltered set as a filtered one:" +
            Environment.NewLine + string.Join(Environment.NewLine, offences));
    }
}
