namespace DM.Infrastructure.Persistence.Repositories.Search;

/// <summary>
/// The text-search dictionary every index and every query of this system uses.
/// </summary>
/// <remarks>
/// It has to be the same name on both sides and there is nothing that says so.
/// The stored vector is built under one configuration and the query is parsed
/// under another; when the two differ, the stems disagree and the query simply
/// returns nothing — no error, no warning, no failing test. Search stops finding
/// things and the page it serves looks exactly like a page with no matches.
///
/// Three places name it, and the third is the one that hides: the stored vector,
/// the query parsed against it, and ts_headline, which builds the preview. Left
/// unnamed there, ts_headline runs under the server's default_text_search_config
/// - english on a stand initialised with an English locale - and quietly marks
/// nothing in a Russian document. The search still finds the row, the preview
/// still comes back, and the highlight is simply never there.
///
/// One name, then, referenced from the model that builds the columns and from
/// every repository that queries them. The migration carries its own copies of
/// the generated SQL, and they are generated: a change here regenerates them, and
/// the schema-drift gate is what refuses a mismatch.
/// </remarks>
internal static class SearchTextConfiguration
{
    /// <summary>Name of the Postgres text-search configuration.</summary>
    public const string Name = "russian";
}
