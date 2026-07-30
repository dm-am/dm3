using System.Collections.Generic;
using System.Linq;

namespace DM.Infrastructure.Mail.Rendering;

/// <summary>
/// Inline styles shared by the email templates.
/// </summary>
/// <remarks>
/// Inline and not a stylesheet because a mail client may drop a style element,
/// and most of them do; in one place and not in each template because the same
/// look has to hold across every letter the product sends.
///
/// The frontend theme cannot reach this far: its custom properties resolve in a
/// browser, against a document this markup never belongs to. The values here are
/// the ones the product already sent — they were spelled out inline in the
/// senders that built their HTML by hand — collected into one place.
/// </remarks>
public static class EmailConstants
{
    /// <summary>Body text colour</summary>
    public const string TextColor = "#333";

    /// <summary>Secondary text colour, for the footer</summary>
    public const string MutedColor = "#666";

    /// <summary>Accent colour, for the call to action</summary>
    public const string AccentColor = "#36a";

    /// <summary>Separator colour</summary>
    public const string RuleColor = "#eee";

    /// <summary>Font stack. Only what a mail client is sure to have.</summary>
    public const string FontFamily = "Arial, Helvetica, sans-serif";

    /// <summary>Outermost element: sets what every child inherits</summary>
    public static string BodyStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "0",
        ["padding"] = "24px 12px",
        ["font-family"] = FontFamily,
        ["font-size"] = "16px",
        ["line-height"] = "1.5",
        ["color"] = TextColor,
    });

    /// <summary>Reading column</summary>
    public static string ContainerStyles => Style(new Dictionary<string, string>
    {
        ["max-width"] = "600px",
        ["margin"] = "0 auto",
    });

    /// <summary>Basic paragraph style</summary>
    public static string ParagraphStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "16px 0",
    });

    /// <summary>Heading style</summary>
    public static string HeadingStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "0 0 16px",
        ["font-size"] = "20px",
    });

    /// <summary>Basic button style</summary>
    public static string ButtonStyles => Style(new Dictionary<string, string>
    {
        ["display"] = "block",
        ["width"] = "300px",
        ["margin"] = "24px auto",
        ["padding"] = "12px",
        ["border-radius"] = "4px",
        ["background-color"] = AccentColor,
        ["color"] = "#fff",
        ["font-size"] = "18px",
        ["text-decoration"] = "none",
        ["text-align"] = "center",
    });

    /// <summary>Logo above the letter</summary>
    public static string LogoStyles => Style(new Dictionary<string, string>
    {
        ["display"] = "block",
        ["margin"] = "0 auto 24px",
    });

    /// <summary>Separator above the footer</summary>
    public static string RuleStyles => Style(new Dictionary<string, string>
    {
        ["border"] = "none",
        ["border-top"] = $"1px solid {RuleColor}",
        ["margin"] = "24px 0 16px",
    });

    /// <summary>Footer note</summary>
    public static string FooterStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "0",
        ["color"] = MutedColor,
        ["font-size"] = "12px",
    });

    /// <summary>Label cell of the fact table in the suspicious-login letter</summary>
    public static string FactLabelStyles => Style(new Dictionary<string, string>
    {
        ["padding"] = "4px 12px 4px 0",
        ["color"] = MutedColor,
        ["vertical-align"] = "top",
    });

    /// <summary>Value cell of the same table</summary>
    public static string FactValueStyles => Style(new Dictionary<string, string>
    {
        ["padding"] = "4px 0",
    });

    private static string Style(IDictionary<string, string> properties) =>
        string.Join("; ", properties.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
}
