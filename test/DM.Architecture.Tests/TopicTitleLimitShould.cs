using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using DM.Domain.Core.Configuration;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

// Read out of the sources: a data annotation is a compile-time constant, and the client cannot be
// asked.
public class TopicTitleLimitShould
{
    private static readonly Regex ClientLimit =
        new(@"TOPIC_TITLE_MAX_LENGTH\s*=\s*(\d+)", RegexOptions.Compiled);

    // A maxlength typed as a number: the forms bind the constant instead.
    private static readonly Regex LiteralMaxLength =
        new("maxlength=\"\\d", RegexOptions.Compiled);

    [Fact]
    public void BeSpelledOnceForBothValidators()
    {
        foreach (var validator in new[] { "CreateTopicValidator.cs", "UpdateTopicValidator.cs" })
        {
            var source = RepositoryFiles.Read("src", "DM.Domain.Forum", "Features", "Topics", validator);

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
        var source = RepositoryFiles.Read("src", "DM.Web.API", "Features", "Forum", "Topics", "CreateTopicRequest.cs");

        source.Should().Contain("StringLength(TopicPolicy.TitleMaxLength",
            "the contract is checked before the validator is reached, and a wider one lets through " +
            "a title the save then refuses");
        Regex.IsMatch(source, @"StringLength\(\d").Should().BeFalse(
            "a length spelled out in the contract is a second decision");
        source.Should().Contain("до {1} символов",
            "the message a person reads takes the number the request is measured against from the " +
            "attribute, so the number is not spelled a second time");
    }

    [Fact]
    public void BeTheNumberBothFormsStopTypingAt()
    {
        var declared = ClientLimit.Match(RepositoryFiles.Read(
            "src", "DM.Web.Client", "src", "shared", "lib", "constants", "forum.ts"));

        declared.Success.Should().BeTrue(
            "the client keeps its own copy of the limit and has to declare it as a number");
        int.Parse(declared.Groups[1].Value, CultureInfo.InvariantCulture).Should().Be(
            TopicPolicy.TitleMaxLength,
            "a form that accepts more than the validator does turns a finished title into a refusal");

        var forms = new[]
        {
            Path.Combine("src", "DM.Web.Client", "src", "pages", "forum", "TopicCreateForm.vue"),
            Path.Combine("src", "DM.Web.Client", "src", "features", "topic", "ui", "TopicView.vue")
        };

        foreach (var form in forms)
        {
            var source = RepositoryFiles.Read(form);

            source.Should().Contain(":maxlength=\"TOPIC_TITLE_MAX_LENGTH\"",
                $"{form} is one of the two forms that write a topic title");
            LiteralMaxLength.IsMatch(source).Should().BeFalse(
                $"{form} spelling the number itself is how the create form came to promise two hundred");
        }
    }
}
