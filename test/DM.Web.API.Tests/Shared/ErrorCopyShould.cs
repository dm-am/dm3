using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Web.API.Tests.Shared;

/// <summary>
/// A validation message is read by a person, and this interface is Russian.
/// </summary>
/// <remarks>
/// Keyed on alphabets: an all-English message is translated with its form, a message in two alphabets is
/// wrong on any. Latin technical tokens stay allowed.
/// </remarks>
public class ErrorCopyShould
{
    /// <summary>Latin tokens that belong in Russian copy as they stand.</summary>
    private static readonly Regex AllowedLatinTokens =
        new("email", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex Cyrillic = new("[а-яА-Я]", RegexOptions.Compiled);

    private static readonly Regex Latin = new("[a-zA-Z]", RegexOptions.Compiled);

    private const BindingFlags DeclaredInstance =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

    [Fact]
    public void NotMixAlphabetsInOneValidationMessage()
    {
        var offenders = new List<string>();
        var messages = 0;

        foreach (var type in typeof(Startup).Assembly.GetTypes().Where(t => t.IsClass))
        {
            foreach (var property in type.GetProperties(DeclaredInstance))
            {
                foreach (var attribute in property.GetCustomAttributes<ValidationAttribute>())
                {
                    var message = attribute.ErrorMessage;
                    if (string.IsNullOrEmpty(message))
                    {
                        continue;
                    }

                    messages++;

                    var remainder = AllowedLatinTokens.Replace(message, string.Empty);
                    if (Cyrillic.IsMatch(remainder) && Latin.IsMatch(remainder))
                    {
                        offenders.Add(type.Name + "." + property.Name + ": " + message);
                    }
                }
            }
        }

        messages.Should().BeGreaterThan(0, "the request DTOs carry validation messages");
        offenders.Should().BeEmpty(
            "a message in two alphabets is one that was translated halfway and shipped");
    }
}
