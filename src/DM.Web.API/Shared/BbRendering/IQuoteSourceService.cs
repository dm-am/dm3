using DM.Web.API.Shared.Dto;

namespace DM.Web.API.Shared.BbRendering;

/// <summary>
/// Builds the markup of a quotation of one piece of content.
/// </summary>
/// <remarks>
/// One implementation for every surface that can be quoted. The alternative -
/// each feature composing the tag itself - is seven copies of a rule that has to
/// be the same everywhere, and the rule that matters most is the one about what
/// the quotation must not carry.
/// </remarks>
public interface IQuoteSourceService
{
    /// <summary>
    /// Quote the given text, attributed to <paramref name="authorName"/>.
    /// </summary>
    /// <remarks>
    /// The caller is responsible for having read the content through the service
    /// that authorizes reading it: whoever may not see the message may not see
    /// its quotation either, and that is decided by the read, not here.
    /// </remarks>
    /// <param name="text">
    /// The carrier the mapping profile built, holding raw BBCode and the
    /// provenance envelope
    /// </param>
    /// <param name="authorName">
    /// Name for the quotation header, or null for a quotation without one
    /// </param>
    Envelope<QuoteSource> Build(BbText text, string? authorName);
}
