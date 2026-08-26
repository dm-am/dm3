using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// A consumer says what happened to the messages it took, and every rule written
/// about that names a series something actually publishes.
/// </summary>
/// <remarks>
/// The broker knows how much work is waiting; only the consumer knows whether
/// the work it took succeeded. A worker that threw on every message kept its
/// queue short, its subscription alive and its process answering, so every rule
/// about the queue and every panel about the process stayed green, and the one
/// trace of the failures was a warning line nothing watched.
///
/// The second half is the other way the hole reappears: a rule whose expression
/// names a series nobody exports evaluates over nothing and is silent for the
/// same reason it would be if everything were healthy. Names are compared
/// against the instruments declared in the sources, because the exporter derives
/// the exported name from those - dots become underscores, a counter gains
/// _total, and a unit outside the UCUM table is appended verbatim.
/// </remarks>
public class ConsumerMetricsShould
{
    /// <summary>Instrument declaration, as every metrics class spells it.</summary>
    private static readonly Regex Instrument = new(
        @"Create(?:Counter|Histogram|UpDownCounter|ObservableGauge|ObservableCounter|Gauge)<[^>]+>\(\s*""(dm\.[a-z0-9._]+)""",
        RegexOptions.Compiled);

    /// <summary>A series of this project, as an expression spells it.</summary>
    private static readonly Regex Series = new(@"\bdm_[a-z0-9_]+", RegexOptions.Compiled);

    /// <summary>Any series name, whoever exports it.</summary>
    /// <remarks>
    /// Deliberately wider than the one above. Restricting the walk to dm_ left the
    /// rules and the boards free to name anything else — including the very series
    /// the previous wave put in place of two panels that had been drawing on
    /// metrics nobody exported. A rule over rabbitmq_queue_messages_ready is as
    /// silent as a rule over a typo, and the walk that was supposed to catch that
    /// class of defect could not see it.
    /// </remarks>
    private static readonly Regex AnySeries = new(
        @"(?<![\w.""])[a-z][a-z0-9]*_[a-z0-9_]+", RegexOptions.Compiled);

    /// <summary>
    /// The prefix of a series nobody here declares, and the scrape job that brings
    /// it in. Both halves are asserted, so a prefix outliving its job is caught and
    /// so is a job nobody draws on.
    /// </summary>
    private static readonly (string Prefix, string Job)[] Exporters =
    [
        // The .NET hosts: ASP.NET Core and the runtime instrument themselves, and the
        // three jobs below scrape /metrics off the same port the app serves on.
        ("http_server_", "dm-api"),
        ("http_client_", "dm-api"),
        ("process_runtime_", "dm-api"),
        ("rabbitmq_", "rabbitmq"),
        ("pg_", "postgres"),
        ("node_", "node"),
        // The contour instruments itself, and the two halves of it are targets like
        // any other: a rule about the receiver is as silent as any rule over a name
        // nobody scrapes.
        ("alertmanager_", "alertmanager"),
        ("prometheus_", "prometheus"),
    ];

    /// <summary>
    /// Words that look like a series and are not: PromQL functions, label names,
    /// and the plugin name in prose. Listed rather than pattern-matched, because
    /// what separates them from a series is what they mean and not how they read.
    /// </summary>
    private static readonly HashSet<string> NotASeries = new(StringComparer.Ordinal)
    {
        "histogram_quantile", "label_replace", "label_join", "clamp_max", "clamp_min",
        "http_route", "http_response_status_code", "job_name", "scrape_interval",
        "static_configs", "rabbitmq_prometheus", "rule_files", "metrics_path",
    };

    /// <summary>What the Prometheus exporter appends to a name of its own accord.</summary>
    private static readonly string[] Suffixes =
        ["_total", "_bucket", "_sum", "_count", "_seconds", "_bytes", "_ratio"];

    [Fact]
    public void MeasureEveryMessageThatPassesThroughAConsumer()
    {
        var middlewares = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*Middleware.cs", SearchOption.AllDirectories)
            .Where(IsAuthored)
            .Where(path => File.ReadAllText(path).Contains(ConsumerSide, StringComparison.Ordinal))
            .ToArray();

        middlewares.Should().NotBeEmpty(
            "the hosts consume through middlewares of this shape, and a walk that finds none " +
            "of them checks nothing");

        middlewares
            .Where(path => !Measures(File.ReadAllText(path)))
            .Select(Relative)
            .Should().BeEmpty(
                "every message of a consumer passes through its middleware, so this is " +
                "the one place that can count them; without it a consumer failing everything " +
                "is indistinguishable from an idle one");

        var hosts = ConsumingHosts();
        hosts.Should().NotBeEmpty(
            "three hosts take messages off a queue, and a walk that finds none of them to " +
            "read passes whatever they do");

        foreach (var host in hosts)
        {
            var installed = InstalledConsumerMiddlewares(
                File.ReadAllText(Path.Combine(host.FullName, Composition)));

            installed.Should().NotBeEmpty(
                $"{host.Name} takes messages off a queue, and a host whose consumer pipeline " +
                "installs nothing counts nothing of what it took");

            installed
                .Where(name => !middlewares.Any(path => path.EndsWith(
                    $"{Path.DirectorySeparatorChar}{name}.cs", StringComparison.Ordinal)))
                .Should().BeEmpty(
                    $"{host.Name} consumes through a middleware no file of this walk measures, " +
                    "and a queue read through one is a queue whose failures reach no dashboard " +
                    "and no rule");
        }
    }

    /// <summary>The file a host composes itself in.</summary>
    private const string Composition = "Startup.cs";

    /// <summary>The interface a host takes its consumers from.</summary>
    private const string ConsumerFactory = "IDmConsumerBuilder";

    /// <summary>A middleware installed into a pipeline, as the declaration spells it.</summary>
    private static readonly Regex Installation = new(@"AddDmConsumerMiddleware<(\w+)>", RegexOptions.Compiled);

    /// <summary>
    /// The hosts that consume, read off the tree rather than listed here.
    /// </summary>
    /// <remarks>
    /// Counting the middleware files was the whole of this rule while every host
    /// carried one of its own. The workers share one now, so the count says nothing
    /// about how many hosts are covered - three consume through two files, and a
    /// fourth would consume through the same two. A list written out here would be no
    /// better: a consuming host forgotten in it is exactly as invisible as it was to
    /// the count, which is the failure a hand-kept mirror of the notification
    /// generators had already caused once. So a host is a project that composes
    /// itself, and it consumes when something in it asks the client for a consumer.
    /// </remarks>
    private static IReadOnlyCollection<DirectoryInfo> ConsumingHosts() =>
        new DirectoryInfo(Path.Combine(RepositoryRoot, "src"))
            .EnumerateDirectories()
            .Where(project => File.Exists(Path.Combine(project.FullName, Composition)))
            .Where(Consumes)
            .ToArray();

    private static bool Consumes(DirectoryInfo project) => project
        .EnumerateFiles("*.cs", SearchOption.AllDirectories)
        .Where(file => IsAuthored(file.FullName))
        .Any(file => File.ReadAllText(file.FullName).Contains(ConsumerFactory, StringComparison.Ordinal));

    /// <summary>
    /// The consumer middlewares a host installs, out of the declarations in its
    /// composition.
    /// </summary>
    /// <remarks>
    /// The declaring call is generic over the middleware type, so the name read
    /// out of the angle brackets is the type the message scope will resolve -
    /// there is no second pipeline sharing the call the way the producer
    /// defaults of the previous client did.
    /// </remarks>
    private static IReadOnlyCollection<string> InstalledConsumerMiddlewares(string composition) =>
        Installation.Matches(composition).Select(match => match.Groups[1].Value).ToArray();

    /// <summary>The interface a consumer middleware implements, which is what selects one.</summary>
    /// <remarks>
    /// The walk went by a file name ending in RetryMiddleware, which is the name the
    /// two workers happened to give theirs. The third consumer has no retry to
    /// name a file after - the realtime push of the API is a copy of a stored
    /// notification and is dropped rather than redelivered - so the one queue
    /// nobody counted was also the one queue this rule could not see.
    /// </remarks>
    private const string ConsumerSide = "IConsumerMiddleware";

    /// <summary>The helper that holds the policy and the instruments they share.</summary>
    private const string SharedPipeline = "MeasuredConsumerPipeline";

    /// <summary>
    /// A middleware that counts what passes through it: either it writes the
    /// instruments itself, or it hands the pipeline to the helper that does.
    /// </summary>
    /// <remarks>
    /// Reading every middleware for the name of the metrics class was the whole
    /// check while each of them carried its own copy of the counters. They share
    /// one now, and a check that still demanded the name in every file would have
    /// demanded the duplication back with it.
    /// </remarks>
    private static bool Measures(string source) =>
        source.Contains("MessagingMetrics", StringComparison.Ordinal) ||
        source.Contains(SharedPipeline, StringComparison.Ordinal);

    /// <summary>A metrics class declaring the meter its instruments belong to.</summary>
    private static readonly Regex MeterOwner = new(
        @"class\s+(\w+)[\s\S]{0,400}?const\s+string\s+MeterName", RegexOptions.Compiled);

    /// <summary>
    /// Every meter this project declares reaches the exporter.
    /// </summary>
    /// <remarks>
    /// Naming the one meter that existed made this a rule about that meter rather
    /// than about meters: three more were declared after it, each with its own
    /// AddMeter line, and nothing would have said a word had any of those lines
    /// been forgotten. An unregistered meter is the worst shape of this defect,
    /// because the instruments still record - the code runs, the counters rise in
    /// process memory, and the scrape simply has no series - so it reads from the
    /// inside exactly like instrumentation that works.
    /// </remarks>
    [Fact]
    public void RegisterEveryMeterWithTheExporter()
    {
        var registration = SourceText.ReadCode(Path.Combine(
            RepositoryRoot, "src", "DM.Infrastructure.Core", "Logging", "LoggingConfiguration.cs"));

        var owners = MetricsSources()
            .SelectMany(path => MeterOwner.Matches(File.ReadAllText(path)))
            .Select(match => match.Groups[1].Value)
            .ToList();

        owners.Should().NotBeEmpty(
            "the sources declare meters, and a walk that finds none of them passes everything");

        foreach (var owner in owners)
        {
            registration.Should().Contain($"{owner}.MeterName",
                $"{owner} declares a meter the provider is never told about, so its " +
                "instruments record into process memory and are exported nowhere - which " +
                "looks from the inside exactly like instrumentation that works");
        }
    }

    /// <summary>The instrument names of one meter, by the class that owns it.</summary>
    private static IReadOnlyCollection<string> InstrumentsOf(string owner)
    {
        var source = MetricsSources()
            .FirstOrDefault(path => Path.GetFileNameWithoutExtension(path) == owner);

        source.Should().NotBeNull($"{owner} is the class this rule is about");

        return Instrument.Matches(File.ReadAllText(source!))
            .Select(match => match.Groups[1].Value.Replace('.', '_'))
            .ToArray();
    }

    private static IEnumerable<string> MetricsSources() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*Metrics.cs", SearchOption.AllDirectories)
        .Where(IsAuthored);

    /// <summary>
    /// The meters whose measurements nobody is waiting on, and why each is one.
    /// </summary>
    /// <remarks>
    /// Named rather than derived, because what puts a meter here is not visible in
    /// a declaration: it is whether a failure it counts has a caller left to
    /// return an error to. Both entries are asserted to exist, so an entry cannot
    /// outlive the class it excuses.
    /// </remarks>
    private static readonly (string Owner, string Why)[] Unattended =
    [
        // A message is taken off a queue with nobody on the other end of it, and
        // the sender swallows a refusal on purpose so that one dead channel does
        // not cost the recipient duplicates over the channels that are up.
        ("MessagingMetrics", "a delivery that fails answers nobody"),
        // The relational write is already committed when the document write runs,
        // and no transaction spans the two, so the failure has nothing left to
        // unwind and the request that caused it has already answered success.
        ("StorageMetrics", "a dropped write is answered as a success"),
    ];

    /// <summary>
    /// Every failure nobody is waiting on is read by a rule.
    /// </summary>
    /// <remarks>
    /// These counters measure unattended work: there is no caller to return an
    /// error to and no page to turn red, so a counter no rule reads is the same
    /// silence as no counter at all - written, exported, and looked at by nobody
    /// until a reader asks why a notification never came or why a badge has been
    /// wrong for a week.
    ///
    /// Deliberately not every failure counter in the project. An upload that fails
    /// answers its caller in the same request, so a rule about it is a choice; the
    /// failures listed above answer nobody, so a rule about them is the only
    /// answer there is.
    /// </remarks>
    /// <summary>
    /// Failure counters whose alert deliberately reads a different series, and
    /// the series it reads instead. Both halves are asserted, so an entry can
    /// neither excuse a counter that is gone nor point at a rule that is.
    /// </summary>
    /// <remarks>
    /// The outbox relay's publish refusal is a delay, not a loss: the row stays
    /// and the next pass retries it, so the alertable fact is how long the
    /// oldest row has been waiting - the lag gauge. A rule on the refusal count
    /// itself would fire on every deploy's broker restart, which is exactly the
    /// event the outbox exists to make routine.
    /// </remarks>
    private static readonly Dictionary<string, string> AlertedThroughAnotherSeries =
        new(StringComparer.Ordinal)
        {
            ["dm_messaging_outbox_publish_failed"] = "dm_messaging_outbox_lag",
        };

    [Fact]
    public void AlertOnEveryFailureNobodyIsWaitingOn()
    {
        var rules = File.ReadAllText(
            Path.Combine(RepositoryRoot, "docker", "prometheus", "alerts.yml"));

        foreach (var (owner, why) in Unattended)
        {
            var failures = InstrumentsOf(owner)
                .Where(name => name.Contains("_failed", StringComparison.Ordinal)
                    || name.Contains("_lost", StringComparison.Ordinal))
                .ToList();

            failures.Should().NotBeEmpty(
                $"{owner} is listed as measuring work nobody is waiting on, and a meter with " +
                "no failure among its instruments does not belong on that list");

            foreach (var failure in failures)
            {
                var alerted = AlertedThroughAnotherSeries.GetValueOrDefault(failure, failure);
                rules.Should().Contain(alerted,
                    $"{failure} counts work that was lost while {why}, and a count no rule " +
                    "reads is the same silence as no count at all");
            }
        }

        foreach (var (excused, _) in AlertedThroughAnotherSeries)
        {
            Unattended.SelectMany(entry => InstrumentsOf(entry.Owner)).Should().Contain(excused,
                $"the exemption for {excused} names an instrument that is gone");
        }
    }

    /// <summary>
    /// Every sender counts what its channel refused.
    /// </summary>
    /// <remarks>
    /// The pipeline counts a delivery that threw, and none of these throw: each
    /// sender catches its own failures on purpose, so that one dead channel cannot
    /// cost the recipient a second copy over the channels that are up. Which means
    /// the pipeline's counter never sees the ordinary case - a bot answering 403
    /// for every recipient, a relay rejecting every letter - and the only place
    /// that can count it is the sender itself.
    ///
    /// By the file rather than by a list, so that a channel added later is asked
    /// the same question. Nothing about a new sender fails without this: it
    /// compiles, it runs, it logs its warning, and the channel it speaks for is
    /// missing from every rule and every panel for as long as nobody asks.
    /// </remarks>
    [Fact]
    public void CountWhatEverySenderSwallows()
    {
        var senders = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src", "DM.Workers.NotificationDispatcher"),
                "*Sender.cs", SearchOption.AllDirectories)
            .Where(IsAuthored)
            .Where(path => !Path.GetFileName(path).StartsWith("I", StringComparison.Ordinal))
            .ToList();

        senders.Should().NotBeEmpty(
            "the dispatcher delivers through senders, and a walk that finds none of them " +
            "passes everything");

        senders
            .Where(path => !File.ReadAllText(path).Contains(
                "MessagingMetrics.DeliveryFailed", StringComparison.Ordinal))
            .Select(Relative)
            .Should().BeEmpty(
                "a sender swallows what its channel refused so that the other channels are " +
                "not replayed, and a swallowed failure nobody counts is a reader who never " +
                "got the notification and nothing anywhere saying so");
    }

    /// <summary>
    /// Refusals at the front door are read by a rule, under both of the names they
    /// arrive under.
    /// </summary>
    /// <remarks>
    /// Two halves, because a login is refused in two places. Past the limiter the
    /// application decides, and the reason it decided is the whole information -
    /// a run of WrongLogin is somebody walking a list of addresses, a run of
    /// WrongPassword against few addresses is somebody walking a list of
    /// passwords. Before the limiter nothing of the sort runs, so the only trace
    /// of a run being turned away is the status on the request series.
    ///
    /// Neither half fails visibly on its own: the site answers, the dashboards
    /// stay green, and the difference between a busy evening of typos and a
    /// credential-stuffing run is a number nobody computes.
    /// </remarks>
    [Fact]
    public void AlertOnRefusalsAtTheFrontDoor()
    {
        var rules = File.ReadAllText(
            Path.Combine(RepositoryRoot, "docker", "prometheus", "alerts.yml"));

        foreach (var refusal in InstrumentsOf("AuthenticationMetrics"))
        {
            rules.Should().Contain(refusal,
                $"{refusal} carries the reason a login was refused, which is the one thing " +
                "the request series cannot say");
        }

        rules.Should().MatchRegex(@"http_response_status_code=""429""",
            "the limiter answers before any code that could count a reason runs, so a run " +
            "being turned away at the door is visible only as a status");
    }

    [Fact]
    public void AlertAndDrawOnlyOnSeriesSomethingPublishes()
    {
        var published = Published();
        published.Should().NotBeEmpty("the sources declare instruments, and finding none passes everything");

        var referenced = Referenced();
        referenced.Should().NotBeEmpty(
            "the rules and the boards name series of this project, and finding none passes everything");

        referenced
            .Where(reference => !published.Contains(Base(reference.Series)))
            .Select(reference => $"{reference.File}: {reference.Series}")
            .Should().BeEmpty(
                "an expression over a series nobody exports returns nothing, and a rule that " +
                "returns nothing is quiet in exactly the way a healthy system is");
    }

    /// <summary>
    /// A series this project does not export comes from a job it scrapes.
    /// </summary>
    /// <remarks>
    /// The other half of the same defect. Two panels of the consumer board used to
    /// draw on metrics no consumer publishes, and they were replaced by
    /// rabbitmq_queue_* — which is right, and which the check above could not see,
    /// because it only ever looked at names beginning with dm_. Anything from an
    /// exporter was outside the walk entirely: the queue series, the runtime
    /// series, the host series. What can be asserted without a running stack is the
    /// pairing — the name belongs to an exporter, and that exporter is scraped.
    ///
    /// The limit of it, stated so nobody reads more into a green run than is there:
    /// within a prefix that is scraped, a misspelt series still passes. Telling
    /// rabbitmq_queue_consumers from rabbitmq_queue_consumerz needs a /metrics
    /// response, which needs the stack up, which is the CI step this replaces and
    /// does not equal. What it does catch is a whole exporter nobody scrapes and a
    /// name belonging to no exporter at all — the two ways a board or a rule ends up
    /// evaluating over nothing for the life of the file.
    /// </remarks>
    [Fact]
    public void DrawOnlyOnExportersPrometheusScrapes()
    {
        var scrape = File.ReadAllText(
            Path.Combine(RepositoryRoot, "docker", "prometheus.yml"));
        var jobs = Regex.Matches(scrape, @"job_name:\s*'([\w-]+)'")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        jobs.Should().NotBeEmpty("the scrape configuration names the jobs");

        var published = Published();
        var foreign = ReferencedAnywhere()
            .Where(reference => !reference.Series.StartsWith("dm_", StringComparison.Ordinal))
            .ToList();

        foreign.Should().NotBeEmpty(
            "the rules and the boards read the exporters too, and finding none passes everything");

        foreach (var (file, series) in foreign)
        {
            var exporter = Exporters.FirstOrDefault(e =>
                series.StartsWith(e.Prefix, StringComparison.Ordinal));

            exporter.Prefix.Should().NotBeNull(
                $"{file} names {series}, which no exporter in this stack is known to publish; " +
                "an expression over a name nobody exports evaluates over nothing and stays " +
                "silent for the same reason it would if everything were healthy");
            jobs.Should().Contain(exporter.Job,
                $"{file} draws on {series} from the {exporter.Job} exporter, and nothing " +
                "scrapes it");
        }

        // Both directions, so an entry cannot outlive what it excused.
        foreach (var (prefix, job) in Exporters)
        {
            jobs.Should().Contain(job,
                $"the {prefix} prefix is excused by the {job} job, which no longer exists");
        }

        published.Should().NotBeEmpty("this project exports series of its own too");
    }

    /// <summary>An instrument declaration with its unit argument.</summary>
    private static readonly Regex InstrumentWithUnit = new(
        @"Create(?<kind>Counter|Histogram|UpDownCounter|ObservableGauge|ObservableCounter|Gauge)<[^>]+>\(\s*""(?<name>dm\.[a-z0-9._]+)""\s*,\s*(?<unit>null|""[^""]*"")",
        RegexOptions.Compiled);

    /// <summary>
    /// Units the Prometheus exporter knows how to turn into a suffix.
    /// </summary>
    /// <remarks>
    /// The exporter maps the UCUM table and appends anything else verbatim, so a
    /// counter declared in "uploads" was exported as dm_uploads_success_uploads_total.
    /// Two entries because two are what this project measures; a third belongs here
    /// the day something is measured in it, and is a decision rather than a typo.
    /// </remarks>
    private static readonly HashSet<string> Units = new(StringComparer.Ordinal) { "s", "By" };

    /// <summary>
    /// A measurement carries its unit and a count carries none.
    /// </summary>
    /// <remarks>
    /// Nothing fails when this is wrong. The instrument records, the exporter
    /// exports, and the series simply has a name nobody would write down from the
    /// declaration - so the rule about it, or the panel drawing it, matches nothing
    /// and stays as silent as it would be if everything were healthy. Which is the
    /// same defect class the walk above exists for, arriving from the other side.
    /// </remarks>
    [Fact]
    public void DeclareOnlyUnitsTheExporterUnderstands()
    {
        var declarations = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsAuthored)
            .SelectMany(path => InstrumentWithUnit.Matches(File.ReadAllText(path)))
            .Select(match => (
                Name: match.Groups["name"].Value,
                Kind: match.Groups["kind"].Value,
                Unit: match.Groups["unit"].Value.Trim('"')))
            .ToList();

        declarations.Should().NotBeEmpty(
            "the expression has to find the declarations, and one that matches none passes " +
            "whatever they say");

        foreach (var (name, kind, unit) in declarations)
        {
            if (kind.EndsWith("Counter", StringComparison.Ordinal))
            {
                unit.Should().Be("null",
                    $"{name} counts things, and a word the exporter does not know is appended " +
                    "verbatim in front of _total");
                continue;
            }

            if (kind == "Gauge" && unit == "null")
            {
                // A level gauge over a count - rows waiting, connections open -
                // is dimensionless: the UCUM table has nothing to map "rows"
                // to, and anything unmapped is appended to the name verbatim.
                continue;
            }

            unit.Should().NotBe("null", $"{name} measures something, and a measurement has a unit");
            Units.Should().Contain(unit,
                $"{name} is declared in a unit the exporter cannot map, so it exports under a " +
                "name nobody would write a rule from");
        }
    }

    /// <summary>
    /// Every histogram of this project has boundaries somebody chose.
    /// </summary>
    /// <remarks>
    /// The SDK ships one default ladder for the whole process and its top bucket is
    /// ten seconds. A histogram left on it answers questions with an interpolation
    /// between boundaries picked for something else, and it answers them in exactly
    /// the same tone as one whose boundaries were picked for it — which is the
    /// failure: a p95 read off the defaults is a number, and it is wrong.
    ///
    /// Bytes make it plainer. Measured against a ladder meant for seconds, every
    /// upload this site accepts lands in the same bucket and the histogram carries
    /// no information whatsoever.
    /// </remarks>
    [Fact]
    public void ChooseTheBucketsOfEveryHistogram()
    {
        var declared = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsAuthored)
            .SelectMany(path => Regex.Matches(File.ReadAllText(path), @"CreateHistogram<[^>]+>\(\s*""(dm\.[a-z0-9._]+)"""))
            .Select(match => match.Groups[1].Value)
            .ToList();

        declared.Should().NotBeEmpty("the expression has to find the histograms it is about");

        var configured = Regex
            .Matches(
                SourceText.ReadCode(Path.Combine(RepositoryRoot,
                    "src", "DM.Infrastructure.Core", "Logging", "LoggingConfiguration.cs")),
                @"AddView\(\s*""(dm\.[a-z0-9._]+)""")
            .Select(match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        declared.Should().BeSubsetOf(configured,
            "a histogram nobody configured is one measured against the defaults of the SDK");
        configured.Should().BeSubsetOf(declared,
            "a view naming an instrument that no longer exists configures nothing and reads " +
            "like it configures something");
    }

    /// <summary>Instrument names declared in the sources, as the exporter sanitises them.</summary>
    private static HashSet<string> Published()
    {
        var fromCode = Directory
            .EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(IsAuthored)
            .SelectMany(path => Instrument.Matches(File.ReadAllText(path)))
            .Select(match => match.Groups[1].Value.Replace('.', '_'));

        return fromCode.Concat(FromTextfileCollector()).ToHashSet(StringComparer.Ordinal);
    }

    /// <summary>
    /// Series a maintenance script writes for node-exporter to pick up.
    /// </summary>
    /// <remarks>
    /// Not every exporter of this project is a C# instrument. The nightly backup
    /// check runs from cron and leaves its verdict in the textfile collector's
    /// directory, which is how a shell script gets a series at all — and it is a
    /// series an alert has every reason to read.
    ///
    /// Read from the scripts rather than trusted from the rule file, so the rule
    /// still has to point at something somebody actually writes: that is the
    /// whole point of the check above.
    ///
    /// Registered under the same normalisation the references go through. A
    /// script writes the final name, unit suffix and all, while a C# instrument
    /// is declared without one and Prometheus appends it — so the set is keyed on
    /// the stem either way.
    /// </remarks>
    private static IEnumerable<string> FromTextfileCollector() => Directory
        .EnumerateFiles(Path.Combine(RepositoryRoot, "docker", "scripts"), "*.sh")
        .SelectMany(path => TextfileSeries.Matches(File.ReadAllText(path)))
        .Select(match => Base(match.Groups[1].Value));

    /// <summary>A HELP line, which is what names a series in the text exposition format.</summary>
    private static readonly Regex TextfileSeries = new(
        @"#\s*HELP\s+(dm_\w+)", RegexOptions.Compiled);

    /// <summary>Series named by the rule file and the boards.</summary>
    private static IReadOnlyCollection<(string File, string Series)> Referenced() =>
        new[] { Path.Combine("docker", "prometheus", "alerts.yml") }
            .Concat(Directory
                .EnumerateFiles(Path.Combine(RepositoryRoot, "docker", "grafana", "dashboards"), "*.json")
                .Select(Relative))
            .SelectMany(relative => Series
                .Matches(File.ReadAllText(Path.Combine(RepositoryRoot, relative)))
                .Select(match => (File: relative, Series: match.Value)))
            .Distinct()
            .ToArray();

    /// <summary>Every series-shaped name in the rules and the boards.</summary>
    private static IReadOnlyCollection<(string File, string Series)> ReferencedAnywhere() =>
        MonitoringFiles()
            .SelectMany(relative => AnySeries
                .Matches(File.ReadAllText(Path.Combine(RepositoryRoot, relative)))
                .Select(match => (File: relative, Series: match.Value)))
            .Where(reference => !NotASeries.Contains(reference.Series))
            .Distinct()
            .ToArray();

    private static IEnumerable<string> MonitoringFiles() =>
        new[] { Path.Combine("docker", "prometheus", "alerts.yml") }
            .Concat(Directory
                .EnumerateFiles(Path.Combine(RepositoryRoot, "docker", "grafana", "dashboards"), "*.json")
                .Select(Relative));

    /// <summary>Strips whatever the exporter appended, down to the instrument name.</summary>
    private static string Base(string series)
    {
        var trimmed = series;
        bool stripped;
        do
        {
            stripped = false;
            foreach (var suffix in Suffixes)
            {
                if (trimmed.EndsWith(suffix, StringComparison.Ordinal))
                {
                    trimmed = trimmed[..^suffix.Length];
                    stripped = true;
                }
            }
        }
        while (stripped);

        return trimmed;
    }

    private static bool IsAuthored(string path) => !path
        .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
        .Any(segment => segment is "obj" or "bin" or "node_modules");

    private static string Relative(string path) => Path.GetRelativePath(RepositoryRoot, path);

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;
}
