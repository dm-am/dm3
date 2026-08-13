namespace DM.Domain.Core.Dto;

/// <summary>
/// One run of a search preview, and whether it is what was searched for.
/// </summary>
/// <remarks>
/// Segments rather than a marked-up string, and that is the whole point of the
/// shape. The database marks the match, and a string carrying those marks would
/// have to be rendered as markup by the reader of it — a field that is plain text
/// everywhere else in the contract, arriving from a document nobody escaped.
/// Split here, the client prints text nodes and the question does not arise.
/// </remarks>
/// <param name="Text">The run itself.</param>
/// <param name="IsMatch">Whether the search found its words in this run.</param>
public readonly record struct SnippetSegment(string Text, bool IsMatch);
