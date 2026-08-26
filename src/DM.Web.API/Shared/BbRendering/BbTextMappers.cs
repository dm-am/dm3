using System.Diagnostics.CodeAnalysis;

namespace DM.Web.API.Shared.BbRendering;

/// <summary>
/// Shared BbText conversions for Mapperly mappers, consumed via
/// <c>[UseStaticMapper(typeof(BbTextMappers))]</c>. One overload per
/// concrete carrier, because DTO members declare the concrete type and
/// Mapperly matches user-implemented methods by the declared member type.
/// Null flows through unchanged in both directions - "no text" must not
/// become an empty carrier.
/// </summary>
public static class BbTextMappers
{
    /// <summary>Raw BBCode value out of a post text carrier</summary>
    [return: NotNullIfNotNull(nameof(text))]
    public static string? ToValue(PostBbText? text) => text?.Value;

    /// <summary>Raw BBCode value out of a common text carrier</summary>
    [return: NotNullIfNotNull(nameof(text))]
    public static string? ToValue(CommonBbText? text) => text?.Value;

    /// <summary>Raw BBCode value out of an info text carrier</summary>
    [return: NotNullIfNotNull(nameof(text))]
    public static string? ToValue(InfoBbText? text) => text?.Value;

    /// <summary>Raw BBCode into a post text carrier</summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static PostBbText? ToPostBbText(string? value) =>
        value == null ? null : new PostBbText { Value = value };

    /// <summary>Raw BBCode into a common text carrier</summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static CommonBbText? ToCommonBbText(string? value) =>
        value == null ? null : new CommonBbText { Value = value };

    /// <summary>Raw BBCode into an info text carrier</summary>
    [return: NotNullIfNotNull(nameof(value))]
    public static InfoBbText? ToInfoBbText(string? value) =>
        value == null ? null : new InfoBbText { Value = value };
}
