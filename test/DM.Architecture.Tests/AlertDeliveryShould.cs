using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <summary>
/// Every alert rule reaches a receiver that exists, and the contour says it is
/// alive.
/// </summary>
/// <remarks>The chain is in MONITORING.md; every link fails silently, so it is asserted link by link.</remarks>
public class AlertDeliveryShould
{
    private const string ScrapeConfiguration = "prometheus.yml";
    private const string AlertDelivery = "prometheus/alertmanager.yml";
    private const string AlertRules = "prometheus/alerts.yml";
    private const string AlertDeliveryEntrypoint = "alertmanager-init.sh";

    /// <summary>
    /// The document points an external watcher at readiness, not at liveness.
    /// </summary>
    /// <remarks>Liveness is the process being up; readiness asks the stores.</remarks>
    [Fact]
    public void PointTheExternalWatcherAtReadiness()
    {
        var guide = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, "docs", "guides", "MONITORING.md"));

        var outside = Between(guide, "### Наблюдение снаружи машины", "---");

        outside.Should().NotBeNullOrWhiteSpace(
            "the guide has a section about watching from outside the machine, and a walk that " +
            "cannot find it checks nothing");
        outside.Should().Contain("/_ready",
            "readiness is what asks the stores, and it is the probe an external service is " +
            "told to poll");
    }

    /// <summary>
    /// The gate of the monitoring answers about the images the stack runs.
    /// </summary>
    [Fact]
    public void CheckTheImagesTheComposeFileNames()
    {
        var script = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, "scripts", "deployment", "check-monitoring.sh"));

        Regex.IsMatch(script, @"prom/(prometheus|alertmanager):v\d").Should().BeFalse(
            "the gate answers about the images docker-compose.yml names, not about a release " +
            "written into the script");
    }

    /// <summary>
    /// Every alert rule reaches something that can deliver it.
    /// </summary>
    /// <remarks>
    /// Each link fails silently: a rule file with no alerting section, a list with no target, a
    /// target no service answers to, a receiver declared as a bare name.
    /// </remarks>
    [Fact]
    public void DeliverEveryAlertRuleToAReceiverThatExists()
    {
        var scrapes = Read(ScrapeConfiguration);

        scrapes.Should().Contain("rule_files:",
            "this assertion belongs to a Prometheus that evaluates rules of its own");

        var targets = Regex
            .Matches(TopLevelSection(scrapes, "alerting:"), @"targets:\s*\[([^\]]*)\]")
            .SelectMany(match => match.Groups[1].Value.Split(','))
            .Select(target => target.Trim().Trim('\'', '"'))
            .Where(target => target.Length > 0)
            .ToList();

        targets.Should().NotBeEmpty(
            "rules evaluated with nowhere to post them are a state on a page nobody opens: " +
            "the alerting section, its alertmanagers list and a target in it are one link");

        var compose = Read(BaseCompose);
        foreach (var target in targets)
        {
            var host = target.Split(':');
            host.Should().HaveCount(2, $"{target} has to name a host and a port");

            var receiver = ServiceWithContainerName(compose, host[0]);
            receiver.Should().Contain(host[1],
                $"nothing in the stack answers on {target}, so every notification is a " +
                "connection refused written to the log of a container nobody reads");
            receiver.Should().Contain(AlertDelivery,
                "the routing and the receiver come from that file, and alertmanager without " +
                "one starts with a configuration that notifies nobody");
        }

        var delivery = Read(AlertDelivery);
        var routed = FindValue(delivery, "receiver:");

        routed.Should().NotBeNullOrEmpty("the route has to name where an alert goes");

        var declared = Regex
            .Matches(delivery, @"^\s*- name:\s*'?([\w-]+)'?", RegexOptions.Multiline)
            .Select(match => match.Groups[1].Value)
            .ToList();

        declared.Should().Contain(routed!,
            "a route naming a receiver that is not declared is refused at start-up, and a " +
            "container that will not start is a receiver that is not there");

        ReceiverBlock(delivery, routed!).Should().Contain("_configs:",
            $"the receiver {routed} is a name with nothing under it: alertmanager accepts " +
            "that and silently drops everything routed to it, which is the outcome having " +
            "no alertmanager already had");

        // The receiver cannot report its own absence, so a rule fires on its target too.
        foreach (var target in targets)
        {
            var job = Regex.Match(scrapes,
                @"job_name:\s*'([\w-]+)'\s*\n\s*static_configs:\s*\n\s*- targets:\s*\['"
                + Regex.Escape(target) + @"'\]");

            job.Success.Should().BeTrue(
                $"nothing scrapes {target}, so the one component every other rule depends on " +
                "is the one component no rule is about");

            Read(AlertRules).Should().MatchRegex(@"up\{job=""" + Regex.Escape(job.Groups[1].Value) + @"""\}",
                $"a target nobody alerts on is a page nobody opens");
        }
    }

    /// <summary>
    /// The contour says it is alive, and says so through the same channel it would
    /// carry an incident by.
    /// </summary>
    /// <remarks>
    /// Silence reads like health, so the contour speaks through the ordinary receiver; watching
    /// for its absence lives outside the machine.
    /// </remarks>
    [Fact]
    public void SendAHeartbeatTheOwnerCanMissTheAbsenceOf()
    {
        var rules = Read(AlertRules);
        var delivery = Read(AlertDelivery);

        // Whole name, not a prefix: renaming the rule to WatchdogRetired leaves the
        // substring in place and the check green over a contour with no heartbeat.
        Regex.IsMatch(rules, @"^\s*- alert: Watchdog\s*$", RegexOptions.Multiline)
            .Should().BeTrue(
                "a contour with no always-firing rule cannot tell silence from health");
        rules.Should().Contain("expr: vector(1)",
            "the heartbeat fires unconditionally: anything evaluated against the site " +
            "is silent precisely when the site is unreachable");

        delivery.Should().Contain("severity = \"watchdog\"",
            "the heartbeat is routed by its own matcher: grouped with incidents it " +
            "would be buried by group_wait and repeat_interval meant for them");

        var routedTo = Regex.Match(
            delivery, @"severity = ""watchdog""[\s\S]*?receiver:\s*'?([\w-]+)'?");
        routedTo.Success.Should().BeTrue("the heartbeat route has to name a receiver");
        ReceiverBlock(delivery, routedTo.Groups[1].Value).Should().Contain("_configs:",
            "a receiver that is a name with nothing under it drops what it is given, " +
            "which is the failure this rule exists to make visible");
    }

    /// <summary>
    /// What differs between a stand and a server reaches the receiver from the
    /// environment, and nothing else does.
    /// </summary>
    /// <remarks>
    /// Alertmanager expands no variable in its file, so the entrypoint renders them; the two
    /// files and compose agree on the set.
    /// </remarks>
    [Fact]
    public void ConfigureTheAlertReceiverFromTheEnvironmentRatherThanFromTheRepository()
    {
        var delivery = Read(AlertDelivery);
        var script = Read(AlertDeliveryEntrypoint);
        var service = ServiceWithContainerName(Read(BaseCompose), "dm-alertmanager");

        // Both CI steps set their own entrypoint, so only this notices compose losing it.
        Regex.Match(service, @"entrypoint:.*").Value.Should().Contain(AlertDeliveryEntrypoint,
            "the image's own entrypoint reads the configuration file as it is written, and " +
            "as it is written it is a template");
        service.Should().Contain($"./{AlertDeliveryEntrypoint}:",
            "an entrypoint that is not mounted into the container is a file the container " +
            "does not have");

        var placeholders = Regex.Matches(delivery, @"\$\{(ALERT_\w+)\}")
            .Select(match => match.Groups[1].Value)
            .Distinct()
            .ToList();

        placeholders.Should().NotBeEmpty(
            "a receiver configuration with no placeholder at all is one that names a " +
            "smarthost and a mailbox in the repository");

        foreach (var placeholder in placeholders)
        {
            script.Should().Contain(placeholder,
                $"nothing expands {placeholder} on the way in, so a placeholder the " +
                "entrypoint does not render arrives as its own text");
            service.Should().Contain(placeholder + ":",
                $"{placeholder} is what a deployment sets, and a variable compose never " +
                "passes leaves the default as the only value there is");
        }

        var password = Regex.Match(delivery, @"smtp_auth_password:\s*'?([^'\n]*)'?");

        password.Success.Should().BeTrue("the relay credential is still configured here");
        password.Groups[1].Value.Should().Contain("${",
            "a password written into this file is a password in the repository, and the " +
            "file is mounted read-only into a container the operator cannot edit either");
    }

    /// <summary>The block a top-level key opens, up to the next key at column zero.</summary>
    private static string TopLevelSection(string configuration, string key)
    {
        var lines = configuration.Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith(key, StringComparison.Ordinal));

        return start < 0
            ? string.Empty
            : string.Join('\n', lines.Skip(start + 1).TakeWhile(line => !Regex.IsMatch(line, @"^\S")));
    }

    /// <summary>The entry of one receiver, up to the next one.</summary>
    private static string ReceiverBlock(string delivery, string name)
    {
        var lines = delivery.Split('\n');
        var start = Array.FindIndex(lines, line =>
            Regex.IsMatch(line, @"^\s*- name:\s*'?" + Regex.Escape(name) + @"'?\s*$"));

        start.Should().BeGreaterThan(-1, $"the receiver {name} must be declared");
        return string.Join('\n', lines
            .Skip(start + 1)
            .TakeWhile(line => !Regex.IsMatch(line, @"^\s*- name:")));
    }
}
