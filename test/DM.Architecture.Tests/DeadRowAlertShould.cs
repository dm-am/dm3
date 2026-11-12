using System;
using System.IO;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Infrastructure.Core.Queues;
using DM.Testing;
using Xunit;

namespace DM.Architecture.Tests;

/// <remarks>
/// The operator on call acts on the alert text alone, so the text carries the command the code
/// returns a dead row with, letter for letter. When it fires is the rule tests' (alerts_test.yml).
/// </remarks>
public class DeadRowAlertShould
{
    private const string IdPlaceholder = "<Id>";

    private static readonly string Rule = RuleText();

    /// <remarks>Mutations it must fail on: a rule without the return command, a stale command.</remarks>
    [Fact]
    public void CarryTheCommandThatReturnsADeadRow() =>
        Rule.Should().Contain(
            DeadRows.ReturnCommand(0).Replace("\"Id\" = 0 ", $"\"Id\" = {IdPlaceholder} ", StringComparison.Ordinal),
            "the operator returns a dead row with one database command and no reading of code");

    [Fact]
    public void FireOnDeadRowsOfEitherQueue() =>
        Rule.Should().Contain("expr: max by (queue) (dm_messaging_outbox_dead) > 0",
                "the gauge carries one series per queue, so a dead letter and a dead event each fire, " +
                "and one alert per queue however many API instances are scraped")
            .And.Contain("for: 5m");

    [Fact]
    public void SayThatAnAccountLetterDeadForMoreThan48HoursIsDeletedAndNotReturned() =>
        Rule.Should().Contain("48 часов",
            "the link in an account letter has expired by then, and a returned one sends a dead link");

    private static string RuleText()
    {
        var rules = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "docker", "prometheus", "alerts.yml"));
        var match = Regex.Match(rules, @"- alert: DeadOutboxRows\r?\n(?<body>(?:[ ]{8,}.*\r?\n|\r?\n)+)");
        match.Success.Should().BeTrue("the rule file holds the dead-row alert, and a walk that finds none checks nothing");
        return match.Groups["body"].Value;
    }
}
