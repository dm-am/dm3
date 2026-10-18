using System.Diagnostics.CodeAnalysis;
using DM.Domain.Core.Content;

namespace DM.Testing.Markup;

/// <summary>
/// The step of the save path that writes the names of tags in lower case, for
/// a test of a service that is not about it: the text comes back as it came.
/// </summary>
/// <remarks>
/// What the step does is held against the parser in BbTagNameCaseShould; a
/// service test that asks whether the service takes the step builds a
/// substitute of its own.
/// </remarks>
public sealed class TagNamesAsTyped : IBbTagNameCase
{
    /// <inheritdoc />
    [return: NotNullIfNotNull(nameof(source))]
    public string? ToLowerCase(string? source, BbTagSet tags) => source;
}
