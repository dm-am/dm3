namespace DM.Web.API.Shared.BbRendering;

/// <summary>
/// The markup of a quotation, ready to be put into a composer as it stands.
/// </summary>
/// <remarks>
/// Deliberately not a <see cref="BbText"/>. Every BbText field of every response
/// is rendered to HTML on the way out, and this one carries the opposite: BBCode
/// source, which the client puts into an editor rather than onto a page.
/// </remarks>
public class QuoteSource
{
    /// <summary>
    /// Whole quotation, opening tag through closing tag, in BBCode
    /// </summary>
    public string Text { get; set; } = null!;

    /// <summary>
    /// Whether a private block this reader can see on the page was left out of
    /// the quotation
    /// </summary>
    /// <remarks>
    /// Always false for a reader the page does not show one to, so the flag
    /// never tells its holder that a block they were not meant to see exists.
    /// </remarks>
    public bool PrivateTextStripped { get; set; }
}
