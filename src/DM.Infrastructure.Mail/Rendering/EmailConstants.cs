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
/// A letter is the guest card of the site, read in a mailbox: the logo above a
/// framed card of the same width, the same light theme, the same title in
/// capitals, the same filled button, the same footnote under a rule, everything
/// centred the way a status card is. The frontend theme cannot reach this far -
/// its custom properties resolve in a browser, against a document this markup
/// never belongs to - so the values are copied from ThemeVariables.css,
/// Variables.sass and AuthSurface.sass by hand, and EmailThemeShould keeps the
/// copy equal to its source.
/// </remarks>
public static class EmailConstants
{
    /// <summary>Body text colour: --text</summary>
    public const string TextColor = "#333";

    /// <summary>Secondary text colour, for notes, the footnote and fact labels: --text-muted</summary>
    public const string MutedColor = "#737373";

    /// <summary>Title colour: --heading</summary>
    public const string HeadingColor = "#630";

    /// <summary>Links and the filled button: --link</summary>
    public const string AccentColor = "#304060";

    /// <summary>Frame of the card and the rule above the footnote: --border</summary>
    public const string RuleColor = "#ccc";

    /// <summary>Page and card colour, and the text on the filled button: --bg-page</summary>
    public const string PageColor = "#fff";

    /// <summary>
    /// The site's face first, then what every mail client is sure to have.
    /// </summary>
    /// <remarks>
    /// The face itself is not fetched: a letter that loads a font from the
    /// site would report being opened, the same way a hosted logo would, and
    /// that is what the inline logo exists to avoid. A reader with PT Sans
    /// installed sees it; everyone else sees the fallback.
    /// </remarks>
    public const string FontFamily = "\"PT Sans\", Arial, Helvetica, sans-serif";

    /// <summary>Text step of the guest card: $auth-font-size</summary>
    public const int FontSize = 15;

    /// <summary>Caption step of the filled button: $font-size</summary>
    public const int ButtonFontSize = 16;

    /// <summary>Title step: $title-font-size</summary>
    public const int TitleFontSize = 20;

    /// <summary>Step of the footnote: $secondary-font-size</summary>
    public const int SecondaryFontSize = 14;

    /// <summary>Step of the addresses under the logo: $tertiary-font-size</summary>
    public const int TertiaryFontSize = 12;

    /// <summary>Corner of the card and of a control: $border-radius, two grid steps</summary>
    public const int BorderRadius = 8;

    /// <summary>Height of a guest control: $auth-control-height</summary>
    public const int ControlHeight = 52;

    /// <summary>Width of the card, in pixels: $auth-card-width</summary>
    public const int CardWidth = 432;

    /// <summary>Inset of the card: the padding of .auth-card</summary>
    public const int CardPadding = 24;

    /// <summary>Outermost element: sets what every child inherits</summary>
    public static string BodyStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "0",
        ["padding"] = "24px 12px",
        ["font-family"] = FontFamily,
        ["font-size"] = FontSize + "px",
        ["line-height"] = "1.5",
        ["color"] = TextColor,
        ["background-color"] = PageColor,
        ["text-align"] = "center",
    });

    /// <summary>Reading column: the logo and the card under it</summary>
    public static string ContainerStyles => Style(new Dictionary<string, string>
    {
        ["max-width"] = CardWidth + "px",
        ["margin"] = "0 auto",
        ["text-align"] = "center",
    });

    /// <summary>
    /// Opens the column for Outlook on Windows, which ignores margin: auto on a
    /// div and would pin the column to the left edge. A conditional comment only
    /// that client reads; every other client sees a comment and nothing else.
    /// </summary>
    public static string OutlookColumnOpen =>
        "<!--[if mso]><table role=\"presentation\" align=\"center\" " +
        $"width=\"{CardWidth}\" cellpadding=\"0\" cellspacing=\"0\" border=\"0\"><tr><td><![endif]-->";

    /// <summary>Closes what <see cref="OutlookColumnOpen" /> opened</summary>
    public const string OutlookColumnClose = "<!--[if mso]></td></tr></table><![endif]-->";

    /// <summary>The card: .auth-card, framed the way a page from a letter frames it</summary>
    public static string CardStyles => Style(new Dictionary<string, string>
    {
        ["box-sizing"] = "border-box",
        ["padding"] = CardPadding + "px",
        ["background-color"] = PageColor,
        ["border"] = $"1px solid {RuleColor}",
        ["border-radius"] = BorderRadius + "px",
        ["text-align"] = "center",
    });

    /// <summary>Logo above the card</summary>
    public static string LogoStyles => Style(new Dictionary<string, string>
    {
        ["display"] = "block",
        ["margin"] = "0 auto 16px",
    });

    /// <summary>Title of the letter: .auth-title</summary>
    public static string HeadingStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "0 0 16px",
        ["font-size"] = TitleFontSize + "px",
        ["font-weight"] = "700",
        ["letter-spacing"] = ".5px",
        ["text-transform"] = "uppercase",
        ["color"] = HeadingColor,
        ["text-align"] = "center",
    });

    /// <summary>A paragraph of the letter: .auth-text</summary>
    public static string ParagraphStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "0 0 16px",
        ["text-align"] = "center",
    });

    /// <summary>The one action of the letter: .auth-button, full width of the card</summary>
    /// <remarks>
    /// Height and line-height carry the same number: a mail client that drops
    /// one of them still draws the control at the height of the site's.
    /// </remarks>
    public static string ButtonStyles => Style(new Dictionary<string, string>
    {
        ["display"] = "block",
        ["height"] = ControlHeight + "px",
        ["line-height"] = ControlHeight + "px",
        ["margin"] = "24px 0 16px",
        ["padding"] = "0 16px",
        ["border-radius"] = BorderRadius + "px",
        ["background-color"] = AccentColor,
        ["color"] = PageColor,
        ["font-size"] = ButtonFontSize + "px",
        ["font-weight"] = "700",
        ["text-decoration"] = "none",
        ["text-align"] = "center",
    });

    /// <summary>A link in the text, the way the guest card draws one</summary>
    /// <remarks>
    /// Breaks anywhere: the one link a letter spells out is the address with
    /// its token, longer than the card.
    /// </remarks>
    public static string LinkStyles => Style(new Dictionary<string, string>
    {
        ["color"] = AccentColor,
        ["font-weight"] = "700",
        ["text-decoration"] = "none",
        ["word-break"] = "break-all",
    });

    /// <summary>
    /// The line under the button: the same step through another door of the site
    /// </summary>
    /// <remarks>
    /// Quiet on purpose. It is read by one reader in a hundred, the one whose
    /// usual address stopped answering, and for everybody else it is a line that
    /// must not compete with the button standing over it. The negative top
    /// margin eats the button's own bottom one, so the pair reads as one block.
    /// </remarks>
    public static string AlternateLinkStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "-8px 0 16px",
        ["font-size"] = TertiaryFontSize + "px",
        ["text-align"] = "center",
    });

    /// <summary>The name of that door, carrying the address in the link</summary>
    /// <remarks>
    /// Muted rather than accented, and without the weight of a link in the text:
    /// the accent of the letter belongs to the button. The host is not printed
    /// here either, the way it is not printed in the line under the card.
    /// </remarks>
    public static string AlternateLinkAnchorStyles => Style(new Dictionary<string, string>
    {
        ["color"] = MutedColor,
        ["text-decoration"] = "underline",
    });

    /// <summary>Separator above the footnote: the top of .auth-footnote</summary>
    public static string RuleStyles => Style(new Dictionary<string, string>
    {
        ["border"] = "none",
        ["border-top"] = $"1px solid {RuleColor}",
        ["margin"] = "24px 0 16px",
    });

    /// <summary>Footnote: .auth-footnote</summary>
    public static string FooterStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "0",
        ["color"] = MutedColor,
        ["font-size"] = SecondaryFontSize + "px",
        ["text-align"] = "center",
    });

    /// <summary>
    /// The box: .auth-warnbox, the short frame the guest card puts facts in
    /// </summary>
    /// <remarks>
    /// A letter without a button needs something besides paragraphs to break it
    /// up, and the site already has that shape. The facts of a security letter
    /// and a moderator word live in it.
    /// </remarks>
    public static string BoxStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "0 0 16px",
        ["padding"] = "13px 16px",
        ["font-size"] = SecondaryFontSize + "px",
        ["line-height"] = "1.5",
        ["border"] = $"1px solid {RuleColor}",
        ["border-radius"] = BorderRadius + "px",
        ["text-align"] = "center",
    });

    /// <summary>The fact table of a security letter, centred inside its box</summary>
    public static string FactTableStyles => Style(new Dictionary<string, string>
    {
        ["margin"] = "0 auto",
    });

    /// <summary>Label cell of the fact table</summary>
    public static string FactLabelStyles => Style(new Dictionary<string, string>
    {
        ["padding"] = "4px 12px 4px 0",
        ["color"] = MutedColor,
        ["vertical-align"] = "top",
        ["text-align"] = "right",
    });

    /// <summary>Value cell of the same table</summary>
    public static string FactValueStyles => Style(new Dictionary<string, string>
    {
        ["padding"] = "4px 0",
        ["text-align"] = "left",
    });

    private static string Style(IDictionary<string, string> properties) =>
        string.Join("; ", properties.Select(kvp => $"{kvp.Key}: {kvp.Value}"));
}
