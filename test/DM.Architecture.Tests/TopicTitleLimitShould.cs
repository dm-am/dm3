using System;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using DM.Domain.Core.Configuration;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The two validators, the request contract and both forms stop a topic title
/// at the same character.
/// </summary>
/// <remarks>
/// The limit lived in four places and two of them disagreed: the validators
/// refused a title over a hundred and thirty and the edit form stopped the
/// typing there, while the create form and the request contract promised two
/// hundred. A title in between was typed in full, accepted by the form, and
/// then refused by the save with a message that named no number.
///
/// Read out of the sources rather than out of running code: a data annotation
/// is a compile-time constant no runtime check would reach, and the client
/// cannot be asked from here at all. A number is compared rather than a phrase,
/// so a failure says which of the four moved.
/// </remarks>
public class TopicTitleLimitShould
{
    private static readonly Regex ClientLimit =
        new(@"TOPIC_TITLE_MAX_LENGTH\s*=\s*(\d+)", RegexOptions.Compiled);

    /// <summary>A maxlength typed as a number: the forms bind the constant instead.</summary>
    private static readonly Regex LiteralMaxLength =
        new("maxlength=\"\\d", RegexOptions.Compiled);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(RepositoryRoot, Path.Combine(parts)));

    [Fact]
    public void BeSpelledOnceForBothValidators()
    {
        foreach (var validator in new[] { "CreateTopicValidator.cs", "UpdateTopicValidator.cs" })
        {
            var source = Read("src", "DM.Domain.Forum", "Features", "Topics", validator);

            source.Should().Contain("MaximumLength(TopicPolicy.TitleMaxLength)",
                $"{validator} answers the save, and the number it answers with belongs to the policy");
            Regex.IsMatch(source, @"MaximumLength\(\d").Should().BeFalse(
                $"a length spelled out in {validator} is a second decision, and it drifts from the " +
                "contract and from the forms the first time the product one moves");
        }
    }

    [Fact]
    public void BeTheNumberTheRequestContractPromises()
    {
        var source = Read("src", "DM.Web.API", "Features", "Forum", "Topics", "CreateTopicRequest.cs");

        source.Should().Contain("StringLength(TopicPolicy.TitleMaxLength",
            "the contract is checked before the validator is reached, and a wider one lets through " +
            "a title the save then refuses");
        Regex.IsMatch(source, @"StringLength\(\d").Should().BeFalse(
            "a length spelled out in the contract is a second decision");
        source.Should().Contain($"до {TopicPolicy.TitleMaxLength} символов",
            "the message a person reads names the number the request is measured against");
    }

    [Fact]
    public void BeTheNumberBothFormsStopTypingAt()
    {
        var declared = ClientLimit.Match(Read(
            "src", "DM.Web.Client", "src", "shared", "lib", "constants", "forum.ts"));

        declared.Success.Should().BeTrue(
            "the client keeps its own copy of the limit and has to declare it as a number");
        int.Parse(declared.Groups[1].Value, CultureInfo.InvariantCulture).Should().Be(
            TopicPolicy.TitleMaxLength,
            "a form that accepts more than the validator does turns a finished title into a refusal");

        var forms = new[]
        {
            Path.Combine("src", "DM.Web.Client", "src", "pages", "forum", "TopicsList.vue"),
            Path.Combine("src", "DM.Web.Client", "src", "features", "topic", "ui", "TopicView.vue")
        };

        foreach (var form in forms)
        {
            var source = Read(form);

            source.Should().Contain(":maxlength=\"TOPIC_TITLE_MAX_LENGTH\"",
                $"{form} is one of the two forms that write a topic title");
            LiteralMaxLength.IsMatch(source).Should().BeFalse(
                $"{form} spelling the number itself is how the create form came to promise two hundred");
        }
    }
}
