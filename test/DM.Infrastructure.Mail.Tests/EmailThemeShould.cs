using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Infrastructure.Mail.Rendering;
using Xunit;

namespace DM.Infrastructure.Mail.Tests;

/// <summary>
/// A letter is the guest card of the site read in a mailbox, and it cannot load
/// the site's stylesheet: the theme reaches it as values copied by hand into
/// EmailConstants. This is the check that the copy still equals its source.
/// </summary>
/// <remarks>
/// The light theme, because a mail client renders a letter on its own ground and
/// the letter paints its page white. A token renamed or recoloured on the site
/// fails here, on the letter side, which is the side nobody opens to look.
/// </remarks>
public class EmailThemeShould
{
    private static readonly string Styles = Path.Combine(
        DM.Testing.RepositoryLayout.Root, "src", "DM.Web.Client", "src", "assets", "styles");

    /// <summary>Custom properties of the first :root block: the light theme.</summary>
    private static IReadOnlyDictionary<string, string> LightTheme()
    {
        var css = File.ReadAllText(Path.Combine(Styles, "ThemeVariables.css"));
        // The block is addressed by a selector list (:root and the light class), so
        // the match runs from :root to the first brace, whatever stands between.
        var root = Regex.Match(css, @":root[^{]*\{(?<body>[^}]*)\}", RegexOptions.Singleline);
        root.Success.Should().BeTrue("the light theme is the first :root block of ThemeVariables.css");
        var tokens = new Dictionary<string, string>();
        foreach (Match m in Regex.Matches(root.Groups["body"].Value, @"--(?<name>[\w-]+)\s*:\s*(?<value>[^;]+);"))
        {
            tokens[m.Groups["name"].Value] = m.Groups["value"].Value.Trim();
        }

        return tokens;
    }

    /// <summary>A pixel variable of a Sass file, as a number.</summary>
    private static int Pixels(string file, string variable)
    {
        var sass = File.ReadAllText(Path.Combine(Styles, file));
        var m = Regex.Match(sass, @"^\$" + Regex.Escape(variable) + @":\s*(?<px>\d+)px\s*$", RegexOptions.Multiline);
        m.Success.Should().BeTrue($"{file} declares ${variable} in pixels");
        return int.Parse(m.Groups["px"].Value);
    }

    [Theory]
    [InlineData("text", EmailConstants.TextColor)]
    [InlineData("text-muted", EmailConstants.MutedColor)]
    [InlineData("heading", EmailConstants.HeadingColor)]
    [InlineData("link", EmailConstants.AccentColor)]
    [InlineData("border", EmailConstants.RuleColor)]
    [InlineData("bg-page", EmailConstants.PageColor)]
    public void PaintWithTheColourOfTheSite(string token, string colour)
    {
        LightTheme().Should().ContainKey(token, "the letter mirrors a token that exists");
        colour.Should().Be(LightTheme()[token], $"--{token} is what the letter paints with");
    }

    [Fact]
    public void KeepTheScaleOfTheSite()
    {
        EmailConstants.FontSize.Should().Be(Pixels("AuthSurface.sass", "auth-font-size"));
        EmailConstants.ButtonFontSize.Should().Be(Pixels("Variables.sass", "font-size"));
        EmailConstants.TitleFontSize.Should().Be(Pixels("Variables.sass", "title-font-size"));
        EmailConstants.CardWidth.Should().Be(Pixels("AuthSurface.sass", "auth-card-width"));
        EmailConstants.SecondaryFontSize.Should().Be(Pixels("Variables.sass", "secondary-font-size"));
        // $border-radius is $small, and $small is two grid steps.
        EmailConstants.BorderRadius.Should().Be(2 * Pixels("Variables.sass", "grid-step"));
        EmailConstants.ControlHeight.Should().Be(Pixels("AuthSurface.sass", "auth-control-height"));
    }

    [Fact]
    public void LeadWithTheFaceOfTheSite()
    {
        var reset = File.ReadAllText(Path.Combine(Styles, "Reset.sass"));
        var face = Regex.Match(reset, @"font-family:\s*""(?<face>[^""]+)""");
        face.Success.Should().BeTrue("Reset.sass names the face of the site");
        EmailConstants.FontFamily.Should().StartWith($"\"{face.Groups["face"].Value}\"",
            "a reader who has the face installed reads the letter in it");
    }
}
