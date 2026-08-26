using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DM.Domain.Core.Uploads;
using AwesomeAssertions;
using Xunit;

namespace DM.Architecture.Tests;

/// <summary>
/// The deployment has to supply what the code refuses to assume.
/// </summary>
/// <remarks>
/// Some settings are written to fail safe rather than fail loud, and a safe
/// failure is silent by construction. Reverse-proxy support is the example this
/// class exists for: with no trusted network configured the API processes no
/// forwarded header at all, which is the right default for a host with no proxy
/// and the wrong one for the only stack that gets deployed. Nothing warned,
/// nothing failed a test, and every request looked like it came from nginx — one
/// address for the whole site, shared by the rate limiter and the login journal.
///
/// The code side is covered by ReverseProxySupportShould. What is asserted here is
/// the half no unit test can see: that the compose files agree with each other.
/// </remarks>
public class DeploymentConfigurationShould
{
    private const string BaseCompose = "docker-compose.yml";
    private const string PreviewCompose = "docker-compose.preview.yml";
    private const string PopCompose = "docker-compose.pop.yml";
    private const string NginxConfiguration = "nginx/nginx.conf";
    private const string EdgeLocations = "nginx/edge-locations.conf";

    /// <summary>
    /// The edge as nginx assembles it: the file with its include resolved.
    /// </summary>
    /// <remarks>
    /// Both servers of the edge include the same locations, which is the point of
    /// the file - the TLS server used to be a commented-out copy and had already
    /// drifted. A rule that read only nginx.conf would now be asserting about a
    /// server with no routes in it and passing for the wrong reason.
    /// </remarks>
    private static string EdgeAsAssembled() => Read(NginxConfiguration)
        .Replace("include /etc/nginx/edge-locations.conf;", Read(EdgeLocations), StringComparison.Ordinal);
    private const string ScrapeConfiguration = "prometheus.yml";
    private const string AlertDelivery = "prometheus/alertmanager.yml";
    private const string AlertRules = "prometheus/alerts.yml";
    private const string AlertDeliveryEntrypoint = "alertmanager-init.sh";
    private const string EnvironmentTemplate = ".env.example";

    private static string DockerDirectory => Path.Combine(DM.Testing.RepositoryLayout.Root, "docker");

    private static string RepositoryRoot => DM.Testing.RepositoryLayout.Root;

    private static string Read(string fileName)
    {
        var path = Path.Combine(DockerDirectory, fileName);
        File.Exists(path).Should().BeTrue($"{fileName} must exist at {path}");
        return File.ReadAllText(path);
    }

    /// <summary>
    /// The management script drains a redirected pipe while the process it started
    /// is still running.
    /// </summary>
    /// <remarks>
    /// A redirected pipe holds tens of kilobytes and the writer blocks once it is
    /// full, so a wait loop that reads nothing until the process exits hangs on any
    /// command that prints more than that — which is every "docker compose --build".
    /// Both animated wrappers were written that way, and the symptom was the worst
    /// kind: no output, no error, no exit, on the two commands a developer reaches
    /// for first.
    ///
    /// Asserted on the text because the deadlock is in the order of two statements
    /// and nothing executes this file under test.
    /// </remarks>
    [Fact]
    public void DrainTheOutputOfEveryProcessTheManagementScriptStarts()
    {
        var root = RepositoryRoot;
        var script = File.ReadAllText(Path.Combine(root, "scripts", "dm.ps1"));

        script.Should().NotContain(".ReadToEnd()",
            "a synchronous read that runs after the wait loop deadlocks the moment the " +
            "child fills the pipe; ReadToEndAsync started before the loop keeps it drained");

        var waits = script.Split("while (-not $process.HasExited)").Length - 1;
        var drains = script.Split("ReadToEndAsync()").Length - 1;
        drains.Should().Be(waits * 2,
            "each wait loop covers one process with two redirected pipes, and a pipe " +
            "nobody reads is the one that blocks");
    }

    /// <summary>
    /// The subnet is pinned rather than handed out by Docker, because it is the
    /// value the API trusts. A generated one cannot be named in configuration.
    /// </summary>
    [Fact]
    public void PinTheApplicationNetworkSubnet()
    {
        var subnet = FindSubnet(Read(BaseCompose));

        subnet.Should().NotBeNull("the dm-full-app network must declare an explicit subnet");
    }

    [Fact]
    public void TrustTheApplicationNetworkWhereNginxFrontsTheApi()
    {
        var subnet = FindSubnet(Read(BaseCompose));
        var preview = Read(PreviewCompose);

        preview.Should().Contain("nginx",
            "this assertion belongs to the overlay that puts a proxy in front of the API");

        preview.Should().Contain("DM_ReverseProxyConfiguration__TrustedNetworks__0",
            "without a trusted network the API processes no forwarded header, so the " +
            "rate limiter and the login journal see the proxy's address for everyone");
        preview.Should().Contain("DM_ReverseProxyConfiguration__ProxyCount",
            "the number of proxies decides how many entries are popped off the right " +
            "of X-Forwarded-For");

        FindTrustedNetwork(preview).Should().Be(subnet,
            "trusting a network other than the one the proxy runs on trusts nobody, " +
            "and trusting a wider one trusts more than our own containers");
    }

    /// <summary>
    /// Every image URL the API hands out has to be reachable from the browser it
    /// is handed to.
    /// </summary>
    /// <remarks>
    /// Thumbnail URLs are built by the API and loaded by the visitor, and the base
    /// default named localhost:8080 — in that browser, the visitor's own machine.
    /// Naming the stand's own domain instead would not have helped: the container
    /// publishes on loopback and the proxy had no location for it, so nothing
    /// outside the host could reach the transform layer at all, and avatars were
    /// blank on every page. Nothing failed, because every gate serves the SPA from
    /// a preview server and the overlay that puts nginx in front is started
    /// nowhere.
    ///
    /// So the endpoint is asserted to be same-origin and the route asserted to
    /// exist — in the server that runs and in the template that replaces it.
    /// </remarks>
    [Fact]
    public void RouteThumbnailUrlsThroughTheProxyThatFrontsTheStand()
    {
        var preview = Read(PreviewCompose);
        var nginx = Read(NginxConfiguration);

        var endpoint = FindValue(preview, "DM_ImageProxyConfiguration__Endpoint:");

        endpoint.Should().NotBeNullOrEmpty(
            "the overlay is the only topology where a proxy fronts the API, and the " +
            "base default points every visitor's browser at its own machine");
        endpoint.Should().StartWith("/",
            "a same-origin path is reachable at whatever host the stand answers on, " +
            "while an absolute endpoint has to name that host and nothing keeps the " +
            "two in step");

        var location = $"location {endpoint!.TrimEnd('/')}/";
        const string upstream = "proxy_pass http://imgproxy:8080/";

        var running = ActiveDirectives(EdgeAsAssembled());
        running.Should().Contain(location,
            $"the API hands out {endpoint}/... and nginx routes by prefix");
        running.Should().Contain(upstream,
            "the container publishes on loopback, so the application network is the " +
            "only way in, and the trailing slash strips the prefix the signature " +
            "does not cover");

    }

    /// <summary>
    /// A published port that names no address listens on every interface, and a
    /// port docker publishes reaches the container through the nat and DOCKER
    /// chains — past the INPUT rules the install script writes. So the firewall
    /// does not cover it and the perimeter does not either.
    /// </summary>
    /// <remarks>
    /// Seventeen of the eighteen publications in the base file bound to loopback.
    /// The eighteenth was the API, which put the whole of /v1, /metrics and
    /// /_health/detail on the public interface of the preview stand: nginx and its
    /// basic auth sit on port 80, so they covered the SPA and nothing behind it.
    ///
    /// The overlay is where a port is meant to face the world, and it publishes
    /// exactly one.
    /// </remarks>
    [Fact]
    public void PublishNoPortToTheWorldFromTheBaseStack()
    {
        var published = PublishedPorts(Read(BaseCompose));

        published.Should().NotBeEmpty("the parser must find the port lines");
        published.Should().OnlyContain(p => p.StartsWith("127.0.0.1:", StringComparison.Ordinal),
            "a publication with no address is reachable from outside and skips the firewall");
    }

    /// <summary>
    /// The vulnerability gate has to be able to run. It reads the assets file, so
    /// it needs a restore; the workflow checks out clean and had none, so the job
    /// failed every run — and publish declares needs: dependency-scan, which is why
    /// no image was ever published. It passed by hand only because a developer
    /// machine has obj/ left over from an ordinary build.
    /// </summary>
    [Fact]
    public void RestoreBeforeListingVulnerablePackages()
    {
        // Commands only. The header comment names both of them, so a search over
        // the whole text finds the prose rather than the script.
        var commands = File.ReadAllLines(
                Path.Combine(DockerDirectory, "..", "scripts", "check-vulnerable-packages.sh"))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith('#'))
            .ToList();

        var restore = commands.FindIndex(l => l.Contains("dotnet restore", StringComparison.Ordinal));
        var list = commands.FindIndex(l => l.Contains("dotnet list", StringComparison.Ordinal));

        restore.Should().BeGreaterThan(-1, "dotnet list package reads the assets file");
        list.Should().BeGreaterThan(-1, "the script must still be the one running the check");
        restore.Should().BeLessThan(list, "restoring after the check helps nobody");
    }

    /// <summary>
    /// Whatever the installer schedules, the verifier looks at.
    /// </summary>
    /// <remarks>
    /// Three backups ran nightly and one watchman checked two of them. The MinIO
    /// backup could not have produced anything anyway — cron gives a job no
    /// environment, so the script exited on its missing password every night — and
    /// the watchman, blind to that directory, kept printing "All backups OK". A
    /// backup nobody verifies is not a backup; a verifier that skips one is worse,
    /// because it says so out loud.
    /// </remarks>
    [Fact]
    public void VerifyEveryBackupTheInstallerSchedules()
    {
        var scriptsDirectory = Path.Combine(DockerDirectory, "scripts");
        var cron = File.ReadAllText(Path.Combine(scriptsDirectory, "install-cron.sh"));
        var verify = File.ReadAllText(Path.Combine(scriptsDirectory, "verify-backup.sh"));

        var scheduled = new[] { "postgres", "minio" }
            .Where(store => cron.Contains($"backup-{store}.sh", StringComparison.Ordinal))
            .ToList();

        scheduled.Should().HaveCount(2, "the parser must find the scheduled backups");
        foreach (var store in scheduled)
        {
            verify.Should().Contain($"/var/backups/{store}",
                $"{store} is backed up nightly, so the verifier has to look at it");
        }
    }

    /// <summary>
    /// The suite is served from an origin the API under test answers to.
    /// </summary>
    /// <remarks>
    /// The API validates the origin of a request twice — the CORS policy and the
    /// check that stands in for a CSRF token read one derived list — and that list
    /// is its public address plus the hosts of the site. The two development ports
    /// are added only when the environment is Development, and the end-to-end job
    /// runs Production on purpose, because a tier that exercises Development
    /// proves nothing about the build that ships.
    ///
    /// So the port the suite serves the built bundle from has to be handed to the
    /// API by the overlay that job writes. It was not, for a while, and nothing
    /// said so: the origins used to be a declared array with this port in it, the
    /// array became a derived list, and the two comments that asserted the port was
    /// allowed went on asserting it. What that costs is a whole job's worth of
    /// specs timing out on locators that never fill, with the cause three files
    /// away from the failure.
    /// </remarks>
    [Fact]
    public void ServeTheEndToEndSuiteFromAnOriginTheApiAnswersTo()
    {
        var fixtures = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "DM.Web.Client", "e2e", "fixtures", "auth.ts"));
        var workflow = File.ReadAllText(
            Path.Combine(RepositoryRoot, ".github", "workflows", "dotnet.yml"));

        var declared = Regex.Match(fixtures, @"PREVIEW_PORT\s*=\s*(\d+)");
        declared.Success.Should().BeTrue(
            "the suite declares the port it serves the bundle from, and a walk that cannot " +
            "find it checks nothing");

        var port = declared.Groups[1].Value;

        // From past the heredoc opener to the line that closes it: the marker of
        // the opener is the word itself, so a search that starts at the whole
        // command finds the end of the range inside its own beginning.
        var overlay = Between(workflow,
            "cat > docker/docker-compose.e2e.yml <<'YAML'", "\n        YAML");

        overlay.Should().Contain($"AdditionalOrigins__0: \"http://localhost:{port}\"",
            $"the bundle is served from port {port} and the API adds the development ports " +
            "only under Development, which this job is not - so an origin it does not answer " +
            "to means CORS drops every response and the origin check refuses every write");
    }

    /// <summary>
    /// What the edge has to answer is written once and read twice.
    /// </summary>
    /// <remarks>
    /// The pipeline checks the pair it builds before it publishes an image, and an
    /// operator checks the public address after watchtower has replaced the
    /// containers. Written out in both places, the two lists drift: the pipeline
    /// goes on passing while the thing the operator runs stops covering what it
    /// used to, and nobody is looking at the pair side by side.
    ///
    /// The readiness probe is asserted by name because it is the one an external
    /// watcher is told to poll. Liveness answers green from a process that reaches
    /// neither database, so a stand whose Postgres is gone would pass a heartbeat
    /// built on it — and a heartbeat that cannot go red is a heartbeat nobody
    /// needs.
    /// </remarks>
    [Fact]
    public void CheckTheEdgeThroughOneListOfAssertions()
    {
        var script = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "smoke-edge.sh"));
        var workflow = File.ReadAllText(
            Path.Combine(RepositoryRoot, ".github", "workflows", "dotnet.yml"));

        var smoke = Between(workflow,
            "- name: The edge answers where the deployment needs it to", "- name: Logs on failure");

        smoke.Should().Contain("smoke-edge.sh",
            "the pipeline and the operator have to be checking the same things, and the only " +
            "way to keep two lists equal is to have one");
        smoke.Should().NotContain("curl",
            "a second copy of the assertions beside the call to the script is the drift this " +
            "exists to close");

        script.Should().Contain("/_ready",
            "this is the probe an external watcher polls: liveness answers green from a " +
            "process that reaches neither database");
        script.Should().Contain("401",
            "the stand is closed at the edge, and a smoke test that never sees a refusal " +
            "would pass just as well against an open one");
    }

    /// <summary>
    /// The document points an external watcher at readiness, not at liveness.
    /// </summary>
    /// <remarks>
    /// The difference is the whole value of the watcher. Liveness is answered by
    /// the process being up; readiness asks the stores. Pointed at the first, an
    /// external heartbeat stays green through exactly the outage it was bought to
    /// find.
    /// </remarks>
    [Fact]
    public void PointTheExternalWatcherAtReadiness()
    {
        var guide = File.ReadAllText(
            Path.Combine(RepositoryRoot, "docs", "guides", "MONITORING.md"));

        var outside = Between(guide, "### Наблюдение снаружи машины", "---");

        outside.Should().NotBeNullOrWhiteSpace(
            "the guide has a section about watching from outside the machine, and a walk that " +
            "cannot find it checks nothing");
        outside.Should().Contain("/_ready",
            "readiness is what asks the stores, and it is the probe an external service is " +
            "told to poll");
    }

    /// <summary>
    /// The alert about a backup sends the reader where the backup writes.
    /// </summary>
    /// <remarks>
    /// The rule named /var/log/dm3-backups.log and cron writes dm3-backup.log.
    /// One letter, and it only ever matters at the one moment the rule exists
    /// for: somebody woken by a failed backup, on a machine they are not fluent
    /// in, typing a path that answers "no such file" — and reading that as
    /// "logging is broken too" rather than as a typo in the alert. Nothing else
    /// checks it: promtool validates the syntax of a description and has no
    /// opinion about the sentence inside it.
    ///
    /// The installer is the source of truth, being the thing that creates the
    /// file and rotates it.
    /// </remarks>
    [Fact]
    public void SendTheReaderOfABackupAlertWhereTheBackupWrites()
    {
        var cron = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "install-cron.sh"));
        var rules = File.ReadAllText(Path.Combine(DockerDirectory, "prometheus", "alerts.yml"));

        var logFile = Regex.Match(cron, @"LOG_FILE=""([^""]+)""");
        logFile.Success.Should().BeTrue("install-cron.sh declares where the backup log goes");

        rules.Should().Contain(logFile.Groups[1].Value,
            "the backup alert points the reader at a log, and a path that answers \"no such " +
            "file\" at three in the morning reads as a second failure rather than as a typo");
    }

    /// <summary>
    /// Every backup script loads the environment file. Cron hands a job almost
    /// nothing, and these scripts need credentials — the MinIO one exits 1 without
    /// its password, and the offsite replication in all three switches itself off
    /// silently when the AWS variables are absent.
    /// </summary>
    [Theory]
    [InlineData("backup-postgres.sh")]
    [InlineData("backup-minio.sh")]
    [InlineData("verify-backup.sh")]
    public void LoadTheEnvironmentInEveryScheduledScript(string script)
    {
        var path = Path.Combine(DockerDirectory, "scripts", script);

        File.Exists(path).Should().BeTrue($"{script} is scheduled by install-cron.sh");
        File.ReadAllText(path).Should().Contain("_env.sh",
            "cron gives the job no environment, and these scripts need credentials");
    }

    /// <summary>
    /// The backup watchman takes the path a find listing gives it whole, and asks
    /// for a file size the way the only host it runs on can answer.
    /// </summary>
    /// <remarks>
    /// The two halves of the script disagreed. The directory check split the
    /// listing with -f2- and the file check with -f2, so a backup directory whose
    /// name holds a space became a path that does not exist, and everything
    /// downstream - age, size, gzip integrity, the pg_dump completion marker -
    /// ran against it. The size line also carried a BSD fallback no host could
    /// reach, because the find above it is GNU-only and leaves the function on
    /// its "no backups found" branch long before that line.
    /// </remarks>
    [Fact]
    public void ReadTheWholeBackupPathAndAskTheHostForItsSize()
    {
        var verify = File.ReadAllText(
            Path.Combine(DockerDirectory, "scripts", "verify-backup.sh"));

        verify.Should().NotContain("cut -d' ' -f2)",
            "the field is a path, and cutting it at the first space renames the file " +
            "every check below then reads");
        verify.Should().NotContain("stat -f%z",
            "the BSD form is unreachable: without GNU find and its -printf the listing " +
            "is empty and the function returns long before this line");
    }

    /// <summary>
    /// Object storage is the one store whose contents nothing can rebuild:
    /// Postgres holds references to uploaded files, the files are the data. So
    /// the account a workload holds decides what a leaked configuration costs.
    /// </summary>
    /// <remarks>
    /// The application, every worker and imgproxy were handed the MinIO root
    /// account, which can drop the bucket, rewrite its policy and mint further
    /// accounts.
    /// </remarks>
    [Fact]
    public void GiveNoWorkloadTheObjectStoreRootAccount()
    {
        var compose = Read(BaseCompose);

        FindValue(compose, "DM_CdnConfiguration__AccessKey:").Should().NotBe("minio",
            "the application gets a service account scoped to its bucket, not the root user");
        FindValue(compose, "DM_CdnConfiguration__SecretKey:").Should().NotContain("MINIO_ROOT_PASSWORD",
            "the root password must not be readable from the application at all");
        FindValue(compose, "AWS_ACCESS_KEY_ID:").Should().NotBe("minio",
            "imgproxy only ever reads objects, so it gets the read-only account");
        FindValue(compose, "AWS_SECRET_ACCESS_KEY:").Should().NotContain("MINIO_ROOT_PASSWORD",
            "nor from the container that parses untrusted image data");
    }

    /// <summary>
    /// Anonymous reads reach the prefixes the product declared public, and no
    /// others.
    /// </summary>
    /// <remarks>
    /// One invariant, one owner. The prefixes are a product decision and live in
    /// UploadFolder; the policy is an administrative call and is applied by the
    /// bootstrap container, because the account the application runs as is
    /// deliberately not allowed to make one. Nothing held the two together, and
    /// they disagreed: the script granted mc's readonly policy on the whole
    /// bucket — GetObject on every key and anonymous ListBucket with it — while
    /// the application asserted a per-prefix policy it had no right to set, and
    /// swallowed the refusal. The declaration everybody read was the one that
    /// never took effect.
    ///
    /// Compared as sets of prefixes rather than by matching the text: the script
    /// spells them as ARNs and the domain as folder names, and the point is that
    /// they name the same things.
    /// </remarks>
    [Fact]
    public void GrantAnonymousReadsOnlyToThePrefixesTheProductDeclaredPublic()
    {
        var script = File.ReadAllText(Path.Combine(DockerDirectory, "minio-init.sh"));

        var declared = UploadFolder.AnonymouslyReadable
            .Select(UploadFolder.For)
            .OrderBy(folder => folder, StringComparer.Ordinal)
            .ToArray();
        declared.Should().NotBeEmpty("the rule below is written in terms of those prefixes");

        var anonymousBlock = Between(script, "dm-anonymous-policy.json <<EOF", "EOF");
        anonymousBlock.Should().NotBeNullOrWhiteSpace(
            "the anonymous policy is written as a document, not as `mc anonymous set`: " +
            "a prefixed set adds a statement without removing the one already in place");

        var granted = Regex.Matches(anonymousBlock!, @"arn:aws:s3:::\$BUCKET/([^""*]+)/\*")
            .Select(match => match.Groups[1].Value)
            .OrderBy(folder => folder, StringComparer.Ordinal)
            .ToArray();

        granted.Should().Equal(declared,
            "UploadFolder.AnonymouslyReadable is where the decision is made, and a type " +
            "added to it without the script following is a prefix nobody can read");
        anonymousBlock.Should().NotContain("arn:aws:s3:::$BUCKET/\"",
            "a bucket-wide grant makes every later type public by default");
        anonymousBlock.Should().NotContain("s3:ListBucket",
            "listing turns the random suffix in an object key into a lookup");
    }

    /// <summary>The text between two markers, or null when the opening one is absent.</summary>
    private static string? Between(string text, string opening, string closing)
    {
        var start = text.IndexOf(opening, StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        start += opening.Length;
        var end = text.IndexOf(closing, start, StringComparison.Ordinal);
        return end < 0 ? null : text[start..end];
    }

    /// <summary>
    /// The point of presence gets its own environment template, and it holds no
    /// real credential.
    /// </summary>
    /// <remarks>
    /// Both guides told the operator to copy .env.example, which carries the
    /// repository's Postgres, MinIO, RabbitMQ and Grafana passwords — onto
    /// a machine in another jurisdiction that runs no application code and
    /// connects to no store. The same command then stopped on interpolation
    /// anyway, demanding the session encryption key, which is the one thing the
    /// whole scheme exists to keep off that machine.
    ///
    /// The placeholders the template does carry are required by the base compose
    /// file, whose environment anchor is resolved when the file is read rather
    /// than when a service starts. Nothing the point of presence brings up reads
    /// them, and they are spelled so that a real value could never be mistaken
    /// for one of them. The imgproxy pair joined them the day the template
    /// stopped shipping a working signature: it is a secret of the main server,
    /// and compose now refuses to interpolate an empty one.
    /// </remarks>
    [Fact]
    public void GiveThePointOfPresenceATemplateWithNoRealSecretInIt()
    {
        var template = Path.Combine(DockerDirectory, ".env.pop.example");
        File.Exists(template).Should().BeTrue(
            "the guides copy this file, and without it they name the one that holds every password");

        var content = File.ReadAllText(template);
        var example = File.ReadAllText(Path.Combine(DockerDirectory, ".env.example"));

        foreach (var secret in new[]
                 {
                     "POSTGRES_PASSWORD", "RABBITMQ_DEFAULT_PASS", "MINIO_ROOT_PASSWORD",
                     "GF_SECURITY_ADMIN_PASSWORD",
                 })
        {
            content.Should().NotContain($"{secret}=",
                $"{secret} belongs to the main server and has no use on a point of presence");
        }

        foreach (var required in new[]
                 {
                     "DM_CryptoConfiguration__KeyBase64",
                     "MINIO_APP_PASSWORD", "MINIO_IMGPROXY_PASSWORD", "IMGPROXY_KEY", "IMGPROXY_SALT",
                 })
        {
            var line = content.Split('\n').FirstOrDefault(l => l.StartsWith($"{required}=", StringComparison.Ordinal));
            line.Should().NotBeNull(
                $"{required} is declared through ${{...:?}} in the base file, so compose stops without it");
            line.Should().Contain("not-used-on-a-point-of-presence",
                "a placeholder has to be unmistakable: a real-looking value here would be " +
                "carried onto that machine and read as working");

            var real = example.Split('\n').FirstOrDefault(l => l.StartsWith($"{required}=", StringComparison.Ordinal));
            line.Should().NotBe(real, $"{required} must not be the value the repository publishes");
        }

        foreach (var guide in new[] { "POINT_OF_PRESENCE.md", "DEPLOYMENT.md" })
        {
            var text = File.ReadAllText(Path.Combine(RepositoryRoot, "docs", "guides", guide));
            text.Should().NotContain("cp .env.example .env.pop",
                $"{guide} would put every password of the installation on the point of presence");
        }
    }

    /// <summary>
    /// The signature guarding the transform layer is not published in this
    /// repository, and no stack comes up without one.
    /// </summary>
    /// <remarks>
    /// The template shipped a working 64-hex pair and the deployment guide's own
    /// manual path is to copy that file, so every reader of the tree held the key
    /// deciding which transforms imgproxy performs - which UPLOADS.md names as
    /// the only thing standing between it and arbitrary ones. The other half was
    /// that an empty pair is a valid configuration: the builder signs with the
    /// literal "insecure" and nothing refuses to start.
    ///
    /// Both halves are held here because either alone is worthless. An empty
    /// template with no ${...:?} behind it is a stand running unsigned, and a
    /// ${...:?} over a published value is a stand running on everybody's key.
    /// </remarks>
    [Fact]
    public void KeepTheImageSignatureOutOfTheRepositoryAndDemandItAtStart()
    {
        var template = Read(EnvironmentTemplate);
        var compose = Read(BaseCompose);
        var generator = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "init-env.sh"));

        foreach (var name in new[] { "IMGPROXY_KEY", "IMGPROXY_SALT" })
        {
            var line = template.Split('\n')
                .FirstOrDefault(l => l.StartsWith($"{name}=", StringComparison.Ordinal));

            line.Should().NotBeNull($"{name} is still a variable of the deployment");
            line!.Trim().Should().Be($"{name}=",
                $"a working {name} in the template is a signing key every reader of this " +
                "repository holds, and the manual path in the deployment guide is to copy " +
                "this very file onto a server");

            compose.Should().Contain($"${{{name}:?",
                $"{name} decides which transforms imgproxy performs, so an empty one has to " +
                "stop interpolation rather than sign every URL with the word insecure");
            generator.Should().Contain($"{name} \"$(openssl rand -hex 32)\"",
                $"the one place that creates docker/.env is the one that has to produce {name}, " +
                "or the demand above turns into a stack nobody can start");
        }
    }

    /// <summary>
    /// The scoped accounts have to exist before the first upload, and only root
    /// can create them — which is the whole of what root is still for.
    /// </summary>
    [Fact]
    public void CreateTheScopedObjectStoreAccountsFromABootstrapContainer()
    {
        var compose = Read(BaseCompose);
        var scriptPath = Path.Combine(DockerDirectory, "minio-init.sh");

        compose.Should().Contain("minio-init:",
            "the accounts the workloads are configured with have to be created by something");
        File.Exists(scriptPath).Should().BeTrue("the bootstrap container mounts it");

        var script = File.ReadAllText(scriptPath);
        script.Should().Contain("arn:aws:s3:::",
            "a policy that names no resource grants the account the whole store");
        script.Should().NotContain("consoleAdmin",
            "attaching a built-in administrative policy puts the root privileges back");
    }

    /// <summary>
    /// The broker holds work nothing else records, so its state needs a volume like
    /// every other store.
    /// </summary>
    /// <remarks>
    /// A letter sitting in dm.mail.sending is the only record that a registration
    /// confirmation is owed, and the dead-letter queue is the only artefact left of
    /// one that could not be sent. Both queues are declared durable, and the broker
    /// was the single service with state and no named volume — pg, loki and
    /// minio all had one — so every recreation of the container dropped them.
    ///
    /// The node name is the other half. Mnesia keeps its data under a directory
    /// named after the node, so a container with a generated hostname mounts the
    /// volume and finds an empty directory beside the one holding the data.
    /// </remarks>
    [Fact]
    public void KeepTheBrokerStateInANamedVolume()
    {
        var compose = Read(BaseCompose);

        compose.Should().Contain("rmqdata:/var/lib/rabbitmq",
            "a durable queue whose directory lives in the container layer is durable " +
            "only until the container is recreated");
        compose.Should().Contain("hostname: 'dm-rmq'",
            "mnesia stores its data under a directory named after the node, so a " +
            "generated hostname makes the volume unreadable to the next container");
    }

    /// <summary>
    /// Nothing the ignore list names may also be tracked.
    /// </summary>
    /// <remarks>
    /// .gitignore has no effect on a path git already holds in the index, so an
    /// entry added after the first commit reads as protection and is none. The
    /// basic-auth hash of the preview stand sat in the repository, and in the
    /// history of every clone, while .gitignore listed it — and the next password
    /// change would have been committed just as quietly.
    ///
    /// Asserted over the whole ignore list rather than that one path: the mistake
    /// belongs to the mechanism, not to the file it happened to catch.
    /// </remarks>
    [Fact]
    public void TrackNoFileTheIgnoreListClaimsToIgnore()
    {
        var tracked = Git("ls-files --cached --ignored --exclude-standard");

        tracked.Should().BeEmpty(
            "a tracked file that .gitignore names is in every clone and stays in the " +
            "history, while the entry tells the next author that it is not");
    }

    /// <summary>
    /// The preview credentials are generated on the host, by one script, with a
    /// hash that survives being leaked.
    /// </summary>
    /// <remarks>
    /// The command lived in four places at once: the installer, the overlay
    /// header, the deployment guide and the closing hint of the installer. All
    /// four wrote apr1 — a thousand rounds of md5, minutes of offline guessing
    /// once the file is out — and the file they produced was committed.
    /// </remarks>
    [Fact]
    public void GenerateThePreviewCredentialsFromOnePlace()
    {
        var generator = Path.Combine(DockerDirectory, "scripts", "init-htpasswd.sh");
        File.Exists(generator).Should().BeTrue(
            "the installer and the operator changing the password call the same script");

        File.ReadAllText(generator).Should().Contain("htpasswd -niB",
            "bcrypt rather than the apr1 default, and the password over stdin rather " +
            "than in an argument every process listing shows");

        File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"))
            .Should().Contain("init-htpasswd.sh",
                "a clone carries no credentials, so the installer has to create them");

        File.ReadAllText(Path.Combine(RepositoryRoot, ".gitignore"))
            .Should().Contain("docker/nginx/.htpasswd",
                "the generated file lands inside the tree compose mounts it from");

        var generatorPath = Path.GetFullPath(generator);
        var duplicates = Directory
            .EnumerateFiles(DockerDirectory, "*", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(
                Path.Combine(RepositoryRoot, "docs"), "*.md", SearchOption.AllDirectories))
            .Where(path => !string.Equals(
                Path.GetFullPath(path), generatorPath, StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("htpasswd -", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(RepositoryRoot, path))
            .ToList();

        duplicates.Should().BeEmpty(
            "a second copy of the command is how the installer and the guide both kept " +
            "generating apr1 long after the choice had been made once");
    }

    /// <summary>Runs git at the repository root and returns its trimmed output.</summary>
    private static string Git(string arguments)
    {
        var start = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(start);
        process.Should().NotBeNull("the assertion can only be made inside a checkout");

        // Drained before the wait for the same reason the management script has to:
        // a full pipe blocks the child that nobody is reading.
        var output = process!.StandardOutput.ReadToEndAsync();
        var error = process.StandardError.ReadToEndAsync();
        process.WaitForExit();

        process.ExitCode.Should().Be(0, $"git {arguments} failed: {error.Result}");
        return output.Result.Trim();
    }

    /// <summary>
    /// The installer has to produce docker/.env before it starts anything.
    /// </summary>
    /// <remarks>
    /// The file is not in the repository, and compose declares the encryption
    /// key and both MinIO accounts through ${...:?}, which
    /// rejects an empty value as hard as a missing one. So the last line of the
    /// installer stopped on interpolation, after it had already enabled the
    /// systemd unit and four nightly backup jobs, and what the operator saw was a
    /// message about a variable rather than about a step nobody wrote.
    /// </remarks>
    [Fact]
    public void CreateTheEnvironmentFileBeforeTheInstallerStartsTheStack()
    {
        var installer = File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"));
        var generator = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "init-env.sh"));

        var creation = installer.IndexOf("init-env.sh", StringComparison.Ordinal);
        var start = installer.IndexOf("docker compose", StringComparison.Ordinal);

        creation.Should().BeGreaterThan(-1, "nothing else creates docker/.env on a clean server");
        start.Should().BeGreaterThan(-1, "the installer is still the thing that starts the stack");
        creation.Should().BeLessThan(start, "compose stops on interpolation before it starts anything");

        generator.Should().Contain("DM_CryptoConfiguration__KeyBase64",
            "the one value the template leaves empty is the one compose refuses to start without");
        generator.Should().Contain("openssl rand",
            "a key inherited from the repository is not a secret");
    }

    /// <summary>
    /// The developer script prepares the environment before it starts anything.
    /// </summary>
    /// <remarks>
    /// The same hole as on the server, on the other platform. dm.sh copied
    /// .env.example and went straight to "docker compose up", and the template
    /// ships the encryption key empty, so the documented Linux and macOS first
    /// run stopped on interpolation with a message about a variable rather than
    /// about a step nobody wrote. The PowerShell path generated the key itself,
    /// which is exactly why nobody saw it: the platform the owner develops on
    /// worked.
    ///
    /// Asked of both scripts, because for a while it was asked of one. dm.sh was
    /// moved onto the generator and dm.ps1 kept copying the template and drawing
    /// its own key, so "the generator is the one place that creates docker/.env"
    /// was the motivation of a test that read a single file and could not have
    /// seen the second implementation it was written against.
    /// </remarks>
    [Theory]
    [InlineData("dm.sh", "docker compose up")]
    [InlineData("dm.ps1", "compose up")]
    public void CreateTheEnvironmentFileBeforeTheDeveloperScriptStartsTheStack(
        string name, string startsWith)
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot, "scripts", name));

        var creation = script.IndexOf("init-env.sh", StringComparison.Ordinal);
        var start = script.IndexOf(startsWith, StringComparison.Ordinal);

        creation.Should().BeGreaterThan(-1,
            $"{name} has to reach the generator, which is the one place that creates " +
            "docker/.env; a second copy of that step is how the two platforms drifted " +
            "apart in the first place");
        start.Should().BeGreaterThan(-1, $"{name} is still the thing that starts the stack");
        creation.Should().BeLessThan(start,
            "compose stops on interpolation before it starts anything");

        script.Should().NotContainAny(
            [".env.example\" \"$ENV", "Copy-Item", "cp \"$DOCKER_DIR/.env.example"],
            $"{name} copying the template itself is the second implementation, and it is " +
            "the half that never learns what the generator learns next");
    }

    /// <summary>
    /// The generator completes a file it did not create.
    /// </summary>
    /// <remarks>
    /// Returning success on sight of an existing docker/.env left the original
    /// failure reachable by the documented route: two guides told the reader to
    /// copy .env.example by hand, and after that the generator had nothing to
    /// say — the copy carries the encryption key empty, compose declares it
    /// through ${...:?}, and the first run died on interpolation on a file the
    /// tooling had just approved of.
    /// </remarks>
    [Fact]
    public void CompleteAnEnvironmentFileSomebodyElseCreated()
    {
        var generator = File.ReadAllText(
            Path.Combine(DockerDirectory, "scripts", "init-env.sh"));

        generator.Should().NotContain("already exists at",
            "an existing file is topped up, not accepted as it is: the key is what is " +
            "missing from a hand-made copy and the only thing compose refuses to start " +
            "without");
        generator.Should().Contain("set_if_empty",
            "and topping up fills only what is empty, so rerunning it never invalidates " +
            "a key a running stand already encrypts with");
    }

    /// <summary>
    /// A server is not installed without an address to deliver mail to.
    /// </summary>
    /// <remarks>
    /// Everything the site says out loud goes through one relay: activation, the
    /// password reset, the warning sent to the old address when the new one is
    /// changed, and every rule of alerts.yml by way of alertmanager. The compose
    /// default is Mailpit, which listens on loopback of the stand and is declared
    /// restart: "no", so a server installed by the documented command delivered
    /// all of it into a dead end — and the alerting contour looked complete while
    /// reaching nobody. There is no Watchdog rule either, so its silence reads
    /// exactly like health.
    ///
    /// Refused rather than defaulted: a contour that delivers nowhere is worse
    /// than no contour at all, because it buys confidence.
    /// </remarks>
    [Fact]
    public void RefuseToInstallAServerWithNowhereToSendMail()
    {
        var generator = File.ReadAllText(
            Path.Combine(DockerDirectory, "scripts", "init-env.sh"));

        var check = generator.IndexOf("is_empty MAIL_HOST", StringComparison.Ordinal);
        check.Should().BeGreaterThan(-1,
            "the relay address is the one answer a server cannot be given by a default");

        var tail = generator[check..];
        tail.Should().Contain("exit 1",
            "a warning on stderr followed by exit 0 is a clean run to the installer " +
            "calling this, which is how the environment stayed Development once already");
    }

    /// <summary>
    /// A server never comes up in Development, and never on the passwords printed
    /// in this repository.
    /// </summary>
    /// <remarks>
    /// Topping up an existing file used to leave both. The environment stayed as
    /// the template had it — Development, which mounts Swagger, relaxes the CSP
    /// to script-src 'self' 'unsafe-inline' and drops Strict-Transport-Security —
    /// and the credentials stayed as the template had them, which is to say
    /// published. The script noted both on stderr and exited 0, and to the
    /// installer calling it that is a clean run.
    ///
    /// The two are fixed differently on purpose. The environment is read at
    /// startup and bound to nothing, so it is simply set. A password is baked
    /// into the MinIO users at first boot, so rotating it on a live stand locks
    /// the API out of its own store — the script refuses instead.
    /// </remarks>
    [Fact]
    public void RefuseToApproveAServerFileStillHoldingTheTemplatesSecrets()
    {
        var generator = File.ReadAllText(
            Path.Combine(DockerDirectory, "scripts", "init-env.sh"));

        var environmentBlock = generator
            .Split("set_value ASPNETCORE_ENVIRONMENT Production", StringSplitOptions.None);
        environmentBlock.Should().HaveCount(2,
            "the environment is set in exactly one place");

        // The condition guarding it, whatever it is. Asserted as "does not test
        // EXISTING" rather than as "does not end with a particular string": the
        // first draft of this compared against a line the file cannot contain, so
        // it was true no matter what the script said.
        var guard = environmentBlock[0][(environmentBlock[0].LastIndexOf("if [", StringComparison.Ordinal))..];
        guard.Should().NotContain("EXISTING",
            "setting the environment only on a file this run created is what left a " +
            "hand-copied server .env running in Development");
        guard.Should().Contain("\"$MODE\" = \"server\"",
            "and it is a server the rule is about");

        generator.Should().Contain("still holds the example values for",
            "an existing server file carrying the repository's passwords has to be " +
            "refused, not noted");
        generator.Should().MatchRegex(@"still holds the example values for[\s\S]{0,400}exit 1",
            "and refused with a non-zero code: a note on stderr beside exit 0 reads " +
            "as success to whatever called this");
    }

    /// <summary>
    /// The server runs what CI published, it does not build.
    /// </summary>
    /// <remarks>
    /// A --build on the installer line compiles the whole solution on a
    /// preview-class VPS and, worse, tags the result with the name the registry
    /// publishes: every later "up" finds that image locally and never pulls
    /// again, so the images CI pushes are consumed by nobody and there is no
    /// version to roll back to. Compose still builds when the registry cannot be
    /// reached, and that fallback is the whole of the build story on a server.
    /// </remarks>
    [Fact]
    public void PullThePublishedImagesInsteadOfBuildingOnTheServer()
    {
        var installer = File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"));
        var compose = Read(BaseCompose) + Read(PreviewCompose);

        var start = installer.Split('\n')
            .First(line => line.Contains("docker compose", StringComparison.Ordinal)
                           && line.Contains("up -d", StringComparison.Ordinal));

        start.Should().NotContain("--build",
            "the deployment pulls the published image, and building it here replaces it " +
            "with a local one under the same tag");

        var localTags = compose.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("image:", StringComparison.Ordinal)
                           && line.Contains(":local", StringComparison.Ordinal))
            .ToList();

        foreach (var tag in localTags)
        {
            tag.Should().Contain("dm-seeder", StringComparison.Ordinal switch
            {
                _ => "a local tag cannot be pulled, so every service holding one is built on " +
                     "the server; the seeder is the exception, the tools profile never starts " +
                     "it there"
            });
        }
    }

    /// <summary>
    /// Everything the delivery workflow publishes has to be something the
    /// deployment pulls.
    /// </summary>
    /// <remarks>
    /// Four images are built and pushed on every merge. Two of them — both
    /// consumers — were named in no compose file at all, which the server made up
    /// for by building them from source. An image nobody pulls is not delivery.
    /// </remarks>
    [Fact]
    public void PullEveryImageTheDeliveryWorkflowPublishes()
    {
        var workflow = File.ReadAllText(
            Path.Combine(DockerDirectory, "..", ".github", "workflows", "dotnet.yml"));
        var compose = Read(BaseCompose) + Read(PreviewCompose);

        var published = Regex.Matches(workflow, @"image_suffix:\s*(\S+)")
            .Select(match => match.Groups[1].Value)
            .Concat(Regex.Matches(workflow, @"IMAGE_PREFIX\s*\}\}-([a-z-]+)")
                .Select(match => match.Groups[1].Value))
            .Distinct()
            .ToList();

        published.Should().HaveCountGreaterThan(1, "the parser must find the published images");
        foreach (var image in published)
        {
            compose.Should().Contain($"/dm-{image}:",
                $"dm-{image} is pushed on every merge, so a deployment has to name it");
        }
    }


    /// <summary>
    /// Nothing but the initialisation connects to Postgres as the superuser.
    /// </summary>
    /// <remarks>
    /// Five workloads and the exporter all connected as postgres. What a leaked
    /// password buys there is not the data of this site: it is the server - every
    /// database on it, the roles, and COPY FROM PROGRAM, which runs commands as
    /// the account the server runs as. The same leak against a role that owns one
    /// database buys that database.
    ///
    /// Asserted on the connection strings and on the script together, because
    /// either half alone passes while the stand does not come up: a role renamed
    /// in the compose file and not in the script leaves every workload unable to
    /// log in, and a script creating a role nobody uses is a role nobody uses.
    ///
    /// The database is created by the script rather than by the migration, and
    /// owned by the application role: since Postgres 15 the right to create in the
    /// public schema belongs to the owner of the database alone.
    /// </remarks>
    [Fact]
    public void ReachPostgresAsARoleSomebodyCreatedForIt()
    {
        var compose = Read(BaseCompose);

        var workload = Regex.Match(compose, @"DM_ConnectionStrings__Rdb:\s*(.+)");
        workload.Success.Should().BeTrue("the workloads share one connection string");
        workload.Value.Should().NotContain("User ID=postgres",
            "a workload holding the superuser holds the server rather than this database");

        var exporter = Regex.Match(compose, @"DATA_SOURCE_NAME:\s*(.+)");
        exporter.Success.Should().BeTrue("the exporter has one of its own");
        exporter.Value.Should().NotContain("postgresql://postgres",
            "reading the statistics views needs pg_monitor and nothing else");

        // Without the comments: this file explains itself at length, and a rule
        // that reads the prose is satisfied by a statement somebody commented out.
        var script = string.Join('\n', File
            .ReadAllLines(Path.Combine(DockerDirectory, "postgres-init.sh"))
            .Select(line => line.Trim())
            .Where(line => !line.StartsWith("--", StringComparison.Ordinal))
            .Where(line => !line.StartsWith('#')));

        foreach (var role in new[] { "dm_app", "dm_exporter" })
        {
            script.Should().Contain($"CREATE ROLE {role}",
                $"{role} is used by the deployment and created by nobody");
        }

        script.Should().Contain("OWNER dm_app",
            "a database owned by the superuser leaves the application unable to create its " +
            "own tables in the public schema");
        script.Should().Contain("GRANT pg_monitor TO dm_exporter",
            "the exporter reads the statistics views, and that is a predefined role");

        ServiceBlock(compose, "postgres").Should().Contain("postgres-init.sh:/docker-entrypoint-initdb.d/",
            "a script the server never runs creates nothing at all");
    }

    /// <summary>
    /// A certificate can be issued, and the server that serves it is not a copy.
    /// </summary>
    /// <remarks>
    /// The TLS server used to be a commented-out copy of the running one, sitting
    /// there since the first day, and it had already drifted: no health probe, and a
    /// listen directive in a form nginx has warned about since 1.25. The thing meant
    /// to be switched on in the hour it was needed would not have started.
    ///
    /// Getting a certificate at all needs the challenge path answered over plain
    /// http, in front of the basic-auth door, by both edges — the authority fetches
    /// it anonymously from the public internet. The documented command was
    /// `certbot --nginx`, which cannot work here: the edge runs in a container whose
    /// configuration is mounted read-only, so the plugin has nothing on the host to
    /// edit. None of that fails visibly until the day somebody needs https.
    /// </remarks>
    [Fact]
    public void LeaveARoadToACertificateAtBothEdges()
    {
        var edge = Read(NginxConfiguration);
        var pointOfPresence = Read("nginx/pop.conf.template");

        foreach (var (name, configuration) in new[]
                 {
                     (NginxConfiguration, edge),
                     ("pop.conf.template", pointOfPresence),
                 })
        {
            ActiveDirectives(configuration).Should().Contain("location ^~ /.well-known/acme-challenge/",
                $"{name} has to answer the challenge over plain http, and ^~ so it wins over " +
                "the prefix that proxies the SPA");
        }

        ActiveDirectives(edge).Should().Contain("auth_basic off",
            "the authority fetches the challenge anonymously, so the door cannot stand in front of it");

        // The copy is gone, and so is the shape it was written in.
        edge.Should().NotContain("listen 443 ssl http2",
            "nginx has warned about that form since 1.25 and the image is newer than that");
        edge.Should().Contain("include /etc/nginx/edge-locations.conf;",
            "one set of locations for both servers, because the copy is what drifted");
        pointOfPresence.Should().NotContain("listen 443 ssl http2",
            "the same form, and the point of presence is the edge that already serves TLS");

        var script = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "init-ssl.sh"));
        script.Should().Contain("certonly --webroot",
            "--nginx has nothing to edit: the configuration is mounted read-only into a container");
        script.Should().Contain("--deploy-hook",
            "nginx reads the certificate at start-up, so a renewal nobody reloads for is a " +
            "certificate that expires while a fresh one sits on disk");

        File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh")).Should().Contain("certbot",
            "the tool the documented procedure runs has to be installed by the installer");
    }

    /// <summary>
    /// The updater the guides call the update mechanism has to be started by the
    /// commands that deploy.
    /// </summary>
    /// <remarks>
    /// watchtower sits behind a profile, and neither the installer nor the unit
    /// enabled one, so the container never started: "updates arrive
    /// automatically" described a service that was declared and never run, and
    /// the only way to move a stand forward was to build on it by hand.
    /// </remarks>
    [Fact]
    public void StartTheUpdaterTheDeploymentUpdatesItselfWith()
    {
        var compose = Read(BaseCompose);
        var installer = File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"));
        var unit = File.ReadAllText(Path.Combine(DockerDirectory, "dm3.service"));

        ServiceBlock(compose, "watchtower").Should().Contain("production",
            "the profile the deployment enables is the one the service has to declare");
        installer.Should().Contain("--profile production",
            "a service behind a profile nobody enables never starts");
        unit.Should().Contain("--profile production",
            "a reboot must bring up the set the installer deployed");

        foreach (var service in new[] { "dmapi", "dm-mail-worker", "dm-notification-worker" })
        {
            ServiceBlock(compose, service).Should().Contain("watchtower.enable=true",
                $"{service} runs a published image, and only labelled containers are updated");
        }

        ServiceBlock(Read(PreviewCompose), "dmfront").Should().Contain("watchtower.enable=true",
            "the SPA image is published by the same run and has to move with it");
    }

    /// <summary>
    /// The edge is recreated whenever a container it resolves by name is replaced.
    /// </summary>
    /// <remarks>
    /// nginx resolves every upstream name once, while parsing its configuration, and
    /// holds that address for the life of the process. There is no resolver directive
    /// and there is not going to be one: a variable in proxy_pass re-resolves but
    /// drops the URI part of the target, which two of the routes here depend on. So a
    /// replaced container leaves the edge proxying to an address that is gone — a 502
    /// on every request through it, for as long as nobody looks.
    ///
    /// Nothing in the stack notices. Compose outside Swarm never restarts a container
    /// for being unhealthy: restart: unless-stopped reacts to the process exiting and
    /// nothing else, and there is no autoheal here. The health check of the edge asks
    /// the edge about itself and gets a cheerful answer from a container that cannot
    /// reach a thing behind it.
    ///
    /// Which leaves the updater's own ordering as the mechanism, and labels as the
    /// only place it is written down. Both directions are asserted: a container the
    /// updater replaces has to be named by the edge, and a name in that list has to
    /// belong to a container the updater actually watches — a dependency it does not
    /// see triggers no restart, ever, while looking exactly like protection.
    /// </remarks>
    [Fact]
    public void RecreateTheEdgeWhenAContainerItResolvesIsReplaced()
    {
        var stack = Read(BaseCompose) + "\n" + Read(PreviewCompose);
        var edge = ServiceBlock(Read(PreviewCompose), "nginx");

        edge.Should().Contain("watchtower.enable=true",
            "with WATCHTOWER_LABEL_ENABLE a container without the label does not exist as " +
            "far as the updater is concerned, so no linked restart can apply to it");

        var declared = EdgeDependencies(edge);
        declared.Should().NotBeEmpty(
            "the edge holds the addresses of the containers behind it, so something has to " +
            "put it back on its feet after one of them is replaced");

        // Forwards: everything the edge resolves by name and the updater replaces.
        foreach (var host in ResolvedHosts(File.ReadAllText(
            Path.Combine(DockerDirectory, "nginx", "nginx.conf"))))
        {
            if (!Regex.IsMatch(stack, $@"^  {Regex.Escape(host)}:", RegexOptions.Multiline)) continue;
            if (!ServiceBlock(stack, host).Contains("watchtower.enable=true", StringComparison.Ordinal)) continue;

            var containerName = Regex.Match(ServiceBlock(stack, host), @"container_name:\s*'([\w-]+)'");
            containerName.Success.Should().BeTrue($"{host} is a service of the stack and has a container name");
            declared.Should().Contain(containerName.Groups[1].Value,
                $"the updater replaces {host} on its own schedule, and the edge resolved it once");
        }

        // Backwards: a name in the list that the updater never touches.
        foreach (var dependency in declared)
        {
            ServiceWithContainerName(stack, dependency).Should().Contain("watchtower.enable=true",
                $"{dependency} is named as a reason to recreate the edge, and the updater " +
                "does not watch it, so it never is one");
        }

        // The point of presence carries no application, and a list inherited from the
        // main stand would name a container that does not exist there.
        var pointOfPresence = ServiceBlock(Read(PopCompose), "nginx");
        var api = Regex.Match(Read(BaseCompose), @"^  dmapi:\n(?:.*\n)*?\s*container_name:\s*'([\w-]+)'",
            RegexOptions.Multiline);

        api.Success.Should().BeTrue("the base stack names the API container");

        // Its own list rather than the inherited one, and stated as a replacement:
        // how compose merges label lists across three files is an implementation
        // detail, and the list it would inherit names the API.
        pointOfPresence.Should().Contain("labels: !override",
            "silence here is not an empty list, it is the list of the main stand");
        EdgeDependencies(pointOfPresence).Should().NotBeEmpty(
            "the edge of the point of presence holds addresses the same way the main one does")
            .And.NotContain(api.Groups[1].Value,
            "the API does not run at the point of presence, and a dependency on a container " +
            "that is never there is a line that reads like ordering and is not");
    }

    /// <summary>Container names the edge is told to wait for, as the label lists them.</summary>
    private static string[] EdgeDependencies(string serviceBlock) => Regex
        .Match(serviceBlock, @"watchtower\.depends-on=([^""\s]+)")
        .Groups[1].Value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>
    /// Hosts an nginx configuration resolves by name and then holds. Names carrying a
    /// variable are excluded: those are re-resolved on every request and are exactly
    /// what this rule exists in place of.
    /// </summary>
    private static IEnumerable<string> ResolvedHosts(string configuration) => configuration
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => !line.StartsWith('#'))
        .SelectMany(line => new[]
        {
            Regex.Match(line, @"^server\s+([a-z0-9][a-z0-9._-]*):\d+;"),
            Regex.Match(line, @"^proxy_pass\s+https?://([a-z0-9][a-z0-9._-]*)"),
        })
        .Where(match => match.Success)
        .Select(match => match.Groups[1].Value)
        .Where(host => !host.Contains('$') && !host.Contains('{'))
        .Distinct();

    /// <summary>
    /// The log of every container on the server has a ceiling.
    /// </summary>
    /// <remarks>
    /// The default json-file driver keeps every line forever, and Serilog writes
    /// to the console beside Loki, so each line is stored twice: once under
    /// Loki's retention and once in a file that grows until the disk is full. On
    /// a preview-class VPS the first service to die on a full disk is Postgres,
    /// and the alert that would have said so is not wired to a receiver.
    ///
    /// Given to the daemon rather than to each compose service, because the
    /// daemon also covers the one-off containers the backup script and the
    /// credential generator start. The installer already owns this layer: it
    /// writes the firewall rules, the unit, the crontab and a logrotate policy
    /// for the backup log.
    /// </remarks>
    [Fact]
    public void CapTheLogOfEveryContainerOnTheServer()
    {
        var installer = File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"));

        var policy = installer.IndexOf("/etc/docker/daemon.json", StringComparison.Ordinal);
        var start = installer.IndexOf("docker compose", StringComparison.Ordinal);

        policy.Should().BeGreaterThan(-1,
            "the default driver keeps every line of every container until the disk is full");
        installer.Should().Contain("max-size", "a driver with no size limit never rotates");
        installer.Should().Contain("max-file", "one rotated file is one file that keeps growing");

        start.Should().BeGreaterThan(-1, "the installer is still the thing that starts the stack");
        policy.Should().BeLessThan(start,
            "log options apply to containers created after they are set");
    }

    /// <summary>
    /// The edge accepts a body as large as the endpoint behind it does.
    /// </summary>
    /// <remarks>
    /// nginx defaults to one megabyte and the upload endpoint accepts ten, and
    /// the client-side compressor returns a file untouched when neither side is
    /// over 1024 pixels — so a four-megabyte PNG travelled as it was. What the
    /// visitor got was an HTML 413 from the edge: not the API error shape, and
    /// with no CORS header on it, so the form could not even show the refusal at
    /// the field. The symptom is "upload does not work", with no explanation.
    /// </remarks>
    [Fact]
    public void AcceptAtTheEdgeEverythingTheUploadEndpointAccepts()
    {
        var controller = File.ReadAllText(Path.Combine(RepositoryRoot,
            "src", "DM.Web.API", "Features", "General", "Upload", "UploadController.cs"));

        var endpoint = Regex.Match(controller, @"RequestSizeLimit\((\d+) \* 1024 \* 1024\)");
        endpoint.Success.Should().BeTrue("the upload endpoint declares its own ceiling");

        // Both public edges. The point of presence proxies the upload path upstream,
        // so a lower limit of its own refuses bodies the main edge and the endpoint
        // both accept - for readers who came in by one address and not the other,
        // which is the hardest shape of this to reproduce and the easiest to miss.
        foreach (var name in new[] { NginxConfiguration, "nginx/pop.conf.template" })
        {
            var edge = Regex.Match(
                ActiveDirectives(Read(name)), @"client_max_body_size (\d+)m;");
            edge.Success.Should().BeTrue(
                $"without the directive {name} cuts every body at its own default of 1 MB");

            edge.Groups[1].Value.Should().Be(endpoint.Groups[1].Value,
                $"a lower limit in {name} refuses what the endpoint accepts, and a higher one " +
                "carries the whole body across the network to be refused at the end of it");
        }
    }

    /// <summary>
    /// An external heartbeat reaches the API, and nothing else about it does.
    /// </summary>
    /// <remarks>
    /// The monitoring guide tells the operator to watch /_health from outside
    /// the host. With no location of its own that path fell into "/", which
    /// proxies the SPA container, and the SPA fallback answers index.html with
    /// 200 — so the monitor reported a healthy site for exactly as long as nginx
    /// and a static container were alive, which is when the answer is worthless.
    ///
    /// Exact matches rather than a prefix: /_health/detail names every
    /// dependency and its state, and /metrics is the whole instrumentation of
    /// the process. Both stay on the inside.
    /// </remarks>
    [Fact]
    public void AnswerTheExternalHeartbeatAtTheEdge()
    {
        var nginx = Read(NginxConfiguration);

        // One server, and the TLS one includes the same file rather than copying it:
        // the copy that used to sit here commented out had already lost this very
        // location by the time anybody would have switched it on.
        // Both public edges: the point of presence proxies these paths upstream, and
        // without locations of their own they fall into the prefix that serves the
        // SPA - which answers index.html with 200 to anything, so a heartbeat aimed
        // there reports a healthy site for as long as the edge itself is alive.
        foreach (var server in new[]
                 {
                     ActiveDirectives(EdgeAsAssembled()),
                     ActiveDirectives(Read("nginx/pop.conf.template")),
                 })
        {
            server.Should().Contain("location = /_health {",
                "the heartbeat the guide recommends has to reach the API, not the SPA fallback");
            server.Should().Contain("location = /_ready {",
                "readiness answers for the stores, and a stand that cannot reach Postgres is down");
            server.Should().NotContain("location /_health",
                "a prefix location publishes /_health/detail, which names every dependency");
            server.Should().NotContain("location /metrics",
                "the instrumentation of the process stays on loopback");
        }
    }

    /// <summary>
    /// The unit and the installer deploy the same set.
    /// </summary>
    /// <remarks>
    /// The shell copy of this comparison in CI greps both files and compares the
    /// results. That step now rejects an empty extraction and matches the overlay
    /// and the profile by name, so a switch to --file, to COMPOSE_FILE, or to a
    /// path outside the character class it matches fails there instead of passing
    /// on nothing. This test carries the same invariant off the runner. What both
    /// exist to catch is a unit that brings up the base topology without nginx
    /// and the SPA, so a reboot replaces the site with a bare API.
    /// </remarks>
    [Fact]
    public void DeployTheSameFilesAndProfilesFromTheUnitAndTheInstaller()
    {
        var installer = ComposeSelectors(
            File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh")));
        var unit = ComposeSelectors(
            File.ReadAllText(Path.Combine(DockerDirectory, "dm3.service")));

        installer.Should().Contain(BaseCompose).And.Contain(PreviewCompose,
            "the site lives in the overlay, and a command naming the base file alone starts " +
            "the API without it");
        installer.Should().Contain("production",
            "watchtower sits behind that profile, and a deployment without it stops updating");
        unit.Should().BeEquivalentTo(installer,
            "a reboot has to bring up the set the installer deployed");
    }

    /// <summary>
    /// The second door runs an edge and a frontend, and no application at all.
    /// </summary>
    /// <remarks>
    /// A profile widens the default set instead of narrowing it, so the
    /// documented command brought up seventeen services: a local Postgres and
    /// MinIO, a migration container that would have run Migrate() against the
    /// main database, and no nginx at all — the edge lives only in the overlay,
    /// so the topology the guide drew was produced by no command.
    ///
    /// Two halves keep that shut, and both are asserted here because either one
    /// alone is enough to bring the whole default profile back. The command names
    /// its services, and the overlay clears the dependencies of the services it
    /// names: compose starts whatever a named service depends on, and nginx and
    /// the frontend both declared a dependency on the API.
    ///
    /// The API is the point. A door that runs one is a second copy of the
    /// application, and a second copy needs the password of the production
    /// database and the key the session is signed with, on a machine chosen for
    /// being reachable rather than for being trusted. The whole reason this
    /// deployment is an edge and a static frontend is that neither holds a
    /// secret.
    /// </remarks>
    [Fact]
    public void RunNoApplicationOnTheSecondDoor()
    {
        var overlay = Read(PopCompose);
        overlay.Should().MatchRegex(@"depends_on: !(reset|override)",
            "compose starts the dependencies of a named service, and the edge declared one on the API");
        overlay.Should().Contain("pop.conf.template",
            "the door has its own edge configuration: its upstream is across the network, " +
            "and the shared one points at a container that does not run here");

        var documents = new[] { "POINT_OF_PRESENCE.md", "DEPLOYMENT.md" }
            .Select(name => File.ReadAllText(
                Path.Combine(RepositoryRoot, "docs", "guides", name)));

        var commands = documents
            .SelectMany(document => document.Split('\n'))
            .Where(line => line.Contains("--env-file .env.pop", StringComparison.Ordinal))
            .ToList();

        commands.Should().NotBeEmpty("the guides still document how the door is started");
        foreach (var command in commands)
        {
            command.Should().Contain(PopCompose,
                "the base file alone starts every store the door has no business running");
            command.Should().Contain(PreviewCompose,
                "the edge and the frontend live in the overlay");

            var arguments = command[(command.IndexOf("up -d", StringComparison.Ordinal) + 5)..]
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(argument => argument.Trim('`'))
                .ToList();

            arguments.Should().Contain("nginx",
                "the door serves from its own edge, and nothing else in the command starts one");
            arguments.Should().Contain("dmfront",
                "serving the frontend from the door is the reason it exists: markup, scripts and " +
                "styles stop crossing the network on every page");

            foreach (var elsewhere in new[] { "dmapi", "postgres", "minio", "migration" })
            {
                arguments.Should().NotContain(elsewhere,
                    $"{elsewhere} belongs to the main server, and a door that runs its own holds " +
                    "the secrets this deployment exists to keep away from it");
            }
        }
    }

    /// <summary>
    /// The door's edge talks to the main server, not to a container beside it.
    /// </summary>
    /// <remarks>
    /// The shared edge proxies to service names on the compose network. On the
    /// door those names resolve to nothing, and nginx refuses to start rather
    /// than serving a broken site — which is the good outcome and still an
    /// outage. The door's own configuration names an upstream that arrives as
    /// configuration, and every deployment fills it in.
    /// </remarks>
    [Fact]
    public void SendTheDoorsApiTrafficAcrossTheNetwork()
    {
        var door = File.ReadAllText(Path.Combine(RepositoryRoot, "docker", "nginx", "pop.conf.template"));

        door.Should().Contain("${POP_UPSTREAM}",
            "the address of the main server is deployment configuration");
        door.Should().Contain("proxy_cache",
            "caching media on the door is why it is a door and not a redirect");

        Regex.Matches(door, @"proxy_pass\s+http://dmapi")
            .Should().BeEmpty("the API does not run here");

        Read(PopCompose).Should().Contain("NGINX_ENVSUBST_FILTER",
            "without a filter the substitution eats $host and $scheme, which belong to nginx");
    }

    /// <summary>
    /// Wherever an edge sets a security header, the copy the upstream sent is
    /// dropped, so the response leaves with one.
    /// </summary>
    /// <remarks>
    /// add_header appends rather than replaces what an upstream sent, and the API
    /// sets the same five on every response of its own, so /v1/ and /hubs/ left
    /// with two copies of each. Two identical copies are not an outage: every one
    /// of these five parses to the same decision while the values agree, which is
    /// why nothing on the stand was ever seen to break. What the rule forbids is
    /// the state and not a symptom - the day the value is edited in one of the two
    /// layers, which copy wins is decided by the parsing rules of that particular
    /// header rather than by whoever made the edit.
    ///
    /// Read per block, because both directives inherit the same way: a level keeps
    /// what the level above it declared only while it declares none of its own. A
    /// file that pairs an add_header in one location with a proxy_hide_header in
    /// another satisfies a text search and still sends two copies, and the point
    /// of presence already has a location that repeats the four headers of its
    /// server.
    ///
    /// Content-Type is the same mistake with the opposite symptom: nginx already
    /// sets one from default_type on a return with a body, so a health path
    /// answered with two of them - and, being an add_header at location level, it
    /// cancelled the inheritance of the policy headers above, which left the one
    /// path of the stand carrying none at all.
    ///
    /// Asked of the server that runs, of the commented template certificates
    /// switch on, and of the point of presence: the mistake belongs to the
    /// mechanism rather than to one file, and the template is the copy nobody
    /// re-reads until the day it becomes the site.
    /// </remarks>
    [Fact]
    public void SendOneCopyOfEverySecurityHeaderTheEdgeSets()
    {
        var edge = Read(NginxConfiguration);
        var configurations = new Dictionary<string, string>
        {
            ["nginx.conf and the locations both of its servers include"] =
                ActiveDirectives(EdgeAsAssembled()),
            ["pop.conf.template"] = ActiveDirectives(File.ReadAllText(
                Path.Combine(DockerDirectory, "nginx", "pop.conf.template"))),
        };

        // Only the headers more than one layer knows how to set. X-Cache-Status is
        // an add_header too and belongs to one layer alone, so the rule is stated
        // over the set that can collide rather than over every directive these
        // files happen to carry.
        var policyHeaders = new[]
        {
            "X-Frame-Options", "X-Content-Type-Options",
            "Referrer-Policy", "Permissions-Policy",
        };

        foreach (var (name, configuration) in configurations)
        {
            var blocks = HeaderBlocks(configuration);

            foreach (var block in blocks)
            {
                block.Added.Contains("Content-Type").Should().BeFalse(
                    $"{name} sets Content-Type with add_header in \"{block.Name}\": nginx emits " +
                    "one from default_type on a return with a body and the directive appends a " +
                    "second, and at location level it cancels the inheritance of every policy " +
                    "header above it as well");

                foreach (var hidden in block.Hidden)
                {
                    block.Emitted.Contains(hidden).Should().BeTrue(
                        $"{name} drops the incoming {hidden} in \"{block.Name}\" and sets none of " +
                        "its own, which leaves the response with no such policy at all");
                }
            }

            var emitting = blocks
                .SelectMany(block => block.Emitted
                    .Where(header => policyHeaders.Contains(header))
                    .Select(header => (Block: block, Header: header)))
                .ToList();

            emitting.Should().NotBeEmpty(
                $"{name} sets the policy headers of the origin it fronts");

            foreach (var (block, header) in emitting)
            {
                block.Hides(header).Should().BeTrue(
                    $"{name} emits {header} in \"{block.Name}\" and proxies to a layer that sets " +
                    "it too, so with no proxy_hide_header in scope the response leaves with two " +
                    "copies of one policy");
            }
        }
    }

    /// <summary>
    /// The backups live on the host, out of reach of "docker compose down -v".
    /// </summary>
    /// <remarks>
    /// A named volume mounted at /var/backups looked like backup storage and was
    /// none: all three scripts write to host directories through docker exec.
    /// Named volumes are what "down -v" removes, and two routine commands run it
    /// — the developer reset and the cleanup step of the security workflow — so
    /// the first backup ever written inside the container would have vanished at
    /// the next reset, with the restore procedure still naming the path.
    /// </remarks>
    [Fact]
    public void KeepTheBackupsOutOfAVolumeThatGoesWithTheStack()
    {
        var compose = Read(BaseCompose) + "\n" + Read(PreviewCompose);

        compose.Should().NotContain("/var/backups",
            "the backup scripts write on the host, and a volume mounted there is removed by " +
            "docker compose down -v");
        compose.Should().NotContain("backups:",
            "a volume nothing ever writes to is a promise the restore procedure cannot keep");
    }

    /// <summary>
    /// Every image the deployment runs names a version.
    /// </summary>
    /// <remarks>
    /// A reference with no tag resolves to whatever the registry holds that day,
    /// so the same commit gives a different runtime a month later. watchtower is
    /// the one that decides it: it mounts /var/run/docker.sock, which is full
    /// control of the host.
    ///
    /// There is no exception. minio/mc used to be one, on the argument that a
    /// client pinned apart from its server is the failure mode - which is true and
    /// is an argument for pinning them together, not for leaving one of them
    /// floating: an unpinned client walks away from the pinned server on its own
    /// schedule, and the first sign is a command the server does not know.
    ///
    /// "Names a version" used to be checked as "the last path segment contains a
    /// colon", which <c>containrrr/watchtower:latest</c> satisfies — the single
    /// most dangerous reference here could go back to a floating tag with this
    /// test green. The tag is now read out and refused by name, and the images
    /// this repository publishes itself are checked separately: their tag comes
    /// from IMAGE_TAG, chosen per branch by the deployment, so what is required
    /// of them is that the choice stays a variable and is never written in. A
    /// digest passes the same rule and is stricter than a tag: what follows the
    /// colon is a hash rather than the word this refuses.
    ///
    /// The shell scripts are read for the same reason the compose files are. This
    /// summary said "every image the deployment runs" while the parser looked at
    /// compose files and Dockerfiles only, so the one line that mints the
    /// credentials of the stand ran an untagged httpd with this test green - and
    /// nothing else was watching it either, because dependabot parses compose
    /// files and Dockerfiles and never a script.
    /// </remarks>
    [Fact]
    public void PinEveryImageTheDeploymentRuns()
    {
        var composeImages = (Read(BaseCompose) + "\n" + Read(PreviewCompose))
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("image:", StringComparison.Ordinal))
            .Select(line => line[6..].Trim().Trim('\'', '"'));

        var dockerfileImages = new[]
            {
                Path.Combine(DockerDirectory, "app.Dockerfile"),
                Path.Combine(RepositoryRoot, "src", "DM.Web.Client", "Dockerfile"),
            }
            .SelectMany(path => File.ReadAllLines(path))
            .Where(line => line.StartsWith("FROM ", StringComparison.Ordinal))
            .Select(line => line[5..].Split(' ')[0]);

        var shellImages = ImagesRunByShellScripts();

        shellImages.Should().HaveCountGreaterThan(1,
            "the parser must find the images the scripts run, in both spellings: the bare " +
            "command and the installer's array that carries sudo");

        var references = composeImages
            .Concat(dockerfileImages)
            .Concat(shellImages)
            .ToList();

        references.Should().HaveCountGreaterThan(10, "the parser must find the image references");

        var published = references.Where(reference => reference.Contains("${", StringComparison.Ordinal)).ToList();
        var external = references.Except(published).ToList();

        published.Should().NotBeEmpty("the stack runs the images this repository builds");
        external.Should().HaveCountGreaterThan(10, "the parser must find the third-party references");

        foreach (var reference in published)
        {
            reference.Should().Contain("${IMAGE_TAG",
                $"{reference} is built here, and which build a server runs is the deployment's " +
                "choice through IMAGE_TAG; a tag written into the file takes that choice away");
        }

        foreach (var reference in external)
        {
            var name = reference.Split('/')[^1];
            var separator = name.LastIndexOf(':');

            separator.Should().BeGreaterThan(0,
                $"{reference} resolves to whatever the registry holds on the day it is pulled");
            name[(separator + 1)..].Should().NotBeEquivalentTo("latest",
                $"{reference} follows the registry rather than this repository, so the same " +
                "commit gives a different runtime a month later - and watchtower, the one that " +
                "would decide it, mounts /var/run/docker.sock");
        }
    }

    /// <summary>docker run options whose value is the argument after them.</summary>
    private static readonly HashSet<string> OptionsTakingAValue = new(StringComparer.Ordinal)
    {
        "--add-host", "--entrypoint", "--env", "--env-file", "--label", "--name",
        "--network", "--platform", "--publish", "--pull", "--user", "--volume",
        "--workdir", "-e", "-l", "-p", "-u", "-v", "-w",
    };

    /// <summary>
    /// The images the deployment scripts start with <c>docker run</c>.
    /// </summary>
    /// <remarks>
    /// Shell is the third place an image reference lives, next to the compose
    /// files and the Dockerfiles, and it was the one nothing read at all.
    /// </remarks>
    private static List<string> ImagesRunByShellScripts()
    {
        var found = new List<string>();

        foreach (var script in Directory
            .EnumerateFiles(DockerDirectory, "*.sh", SearchOption.AllDirectories)
            .OrderBy(path => path, StringComparer.Ordinal))
        {
            // An invocation is wrapped over several lines with backslashes and the
            // image sits on the last of them, so the continuations are folded away
            // before anything is read out. The second alternative of the pattern is
            // the installer's "${DOCKER[@]}" array, which holds sudo for as long as
            // the docker group membership of the session has not taken effect.
            var folded = Regex.Replace(File.ReadAllText(script), @"\\\r?\n\s*", " ");

            foreach (Match invocation in Regex.Matches(
                folded, @"(?:docker|DOCKER\[@\]\}""?)\s+run\s+(?<arguments>[^\r\n]*)"))
            {
                var image = ImageOperandOf(invocation.Groups["arguments"].Value);
                if (image != null)
                {
                    found.Add(image);
                }
            }
        }

        return found;
    }

    /// <summary>
    /// The image of a <c>docker run</c>: the first argument that is neither an
    /// option nor the value of one.
    /// </summary>
    private static string? ImageOperandOf(string arguments)
    {
        var tokens = arguments
            .Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(token => token.Trim('\'', '"'))
            .Where(token => token.Length > 0)
            .ToList();

        for (var index = 0; index < tokens.Count; index++)
        {
            if (tokens[index][0] != '-')
            {
                return tokens[index];
            }

            // Without the skip an "--entrypoint sh" hands back sh as the image. The
            // --option=value spelling is one token and needs none.
            if (OptionsTakingAValue.Contains(tokens[index]))
            {
                index++;
            }
        }

        return null;
    }

    /// <summary>
    /// The installer prints no credential.
    /// </summary>
    /// <remarks>
    /// The operator chose the password a minute earlier, so printing it teaches
    /// nobody anything and leaves it in the terminal, in the history of the
    /// session and in the install log whenever the run goes through tee. Under
    /// set -u it was worse than untidy: an operator who typed the password at the
    /// generator's prompt instead of exporting it lost the installer on its very
    /// last line.
    /// </remarks>
    [Fact]
    public void PrintNoCredentialFromTheInstaller()
    {
        var lines = File.ReadAllLines(Path.Combine(DockerDirectory, "setup-server.sh"))
            .Where(line => line.TrimStart().StartsWith("echo", StringComparison.Ordinal))
            .ToList();

        lines.Should().NotBeEmpty("the installer still reports what it did");
        lines.Should().NotContain(
            line => line.Contains("DM_PREVIEW_PASSWORD", StringComparison.Ordinal),
            "the terminal, the shell history and the install log keep whatever is printed here");
    }

    /// <summary>
    /// The branch the documented command downloads is the branch the installer
    /// then clones.
    /// </summary>
    /// <remarks>
    /// The install command is a raw URL with the branch in the path, and the
    /// script it fetches clones a branch of its own from DM_BRANCH. The two are
    /// written in three places — the guide, the usage comment, the default — and
    /// nothing tied them together, so moving the stand to another branch takes
    /// three edits and misses one. The failure is quiet in the worst way: the
    /// operator runs the URL they were given, and the server ends up on the other
    /// branch, with the image tag of the other branch, while the guide keeps
    /// describing the one they asked for.
    ///
    /// This asserts coherence, not the choice. Which branch a stand tracks is an
    /// operational decision — this repository develops on dev, publishes latest
    /// from main and has no release tag flow, so the answer is not derivable from
    /// the tree.
    /// </remarks>
    [Fact]
    public void InstallFromTheRefTheDocumentedCommandDownloads()
    {
        var installer = File.ReadAllText(Path.Combine(DockerDirectory, "setup-server.sh"));

        Regex.IsMatch(installer, @"DM_REF=""\$\{DM_REF:-").Should().BeTrue(
            "the installer picks what it clones, and it has to name a default for a " +
            "command that passes nothing");

        Regex.IsMatch(installer, @"git clone --branch ""\$DM_REF""").Should().BeTrue(
            "the clone takes the ref the caller asked for, not a second name for it");

        // README.md is in the walk because it is the first page a reader meets and
        // the earlier version of this test scanned docs/ alone: the one file most
        // likely to carry the install command was the one file exempt from it.
        var sources = Directory
            .GetFiles(Path.Combine(RepositoryRoot, "docs"), "*.md", SearchOption.AllDirectories)
            .Append(Path.Combine(RepositoryRoot, "README.md"))
            .Append(Path.Combine(DockerDirectory, "setup-server.sh"));

        var refs = sources
            .SelectMany(path => Regex.Matches(
                File.ReadAllText(path),
                @"raw\.githubusercontent\.com/[\w.\-]+/[\w.\-]+/([^/]+)/docker/setup-server\.sh")
                .Select(match => (File: Path.GetFileName(path), Ref: match.Groups[1].Value)))
            .ToList();

        refs.Should().NotBeEmpty(
            "the automatic installation is documented as a URL with the ref in its path");

        // The ref is written once and read twice. A literal in the path hands out a
        // script from one tree that clones another the moment the default moves, and
        // that is exactly how a stand ended up on dev's images under main's compose.
        foreach (var (file, reference) in refs)
        {
            reference.Should().Be("$DM_REF",
                $"{file} spells the ref out instead of passing the one the operator chose, " +
                "so the script downloaded and the tree cloned drift apart");
        }
    }

    /// <summary>
    /// The gates exercise the environment that ships.
    /// </summary>
    /// <remarks>
    /// Both workflows create docker/.env from the example, and the example ends
    /// with ASPNETCORE_ENVIRONMENT=Development for local work — so the end-to-end
    /// tier and the security scan ran against a build that maps Swagger and
    /// relaxes the script-src of its own content policy for it, while the
    /// configuration that reaches a server was exercised by nothing.
    ///
    /// Asked of every job that starts the stack, not of the file. Searching the
    /// whole text for the line once made two jobs share one answer: the .NET
    /// workflow starts the stack twice, in the end-to-end tier and in the
    /// deployment smoke, and either of them could go back to Development while
    /// the other kept the string and the test green. The named subject of the
    /// finding, 235 end-to-end tests, was on the side that could slip.
    /// </remarks>
    [Theory]
    [InlineData("dotnet.yml")]
    [InlineData("security.yml")]
    public void RunTheGatesAgainstTheEnvironmentThatShips(string workflow)
    {
        var jobs = Jobs(Path.Combine(RepositoryRoot, ".github", "workflows", workflow));

        jobs.Should().NotBeEmpty($"{workflow} declares jobs");

        var starters = jobs
            .Where(job => job.Body.Contains("docker compose up", StringComparison.Ordinal))
            .ToList();

        starters.Should().NotBeEmpty(
            $"{workflow} is one of the workflows that start the stack");
        foreach (var (name, body) in starters)
        {
            body.Should().Contain("ASPNETCORE_ENVIRONMENT: Production",
                $"job {name} of {workflow} exercises Development, which proves nothing " +
                "about what gets deployed");
        }
    }

    /// <summary>
    /// The jobs of a workflow, each with the text that belongs to it.
    /// </summary>
    /// <remarks>
    /// A job is a two-space key under "jobs:", and everything up to the next one
    /// is its body. Textual because that is the granularity the assertions need
    /// and a YAML reader would have to be taught the same nesting anyway.
    /// </remarks>
    private static List<(string Name, string Body)> Jobs(string workflow)
    {
        var lines = File.ReadAllLines(workflow);
        var header = new Regex(@"^  ([\w.\-]+):\s*$");
        var jobs = new List<(string, string)>();

        var start = Array.FindIndex(lines, line => line.StartsWith("jobs:", StringComparison.Ordinal));
        start.Should().BeGreaterThanOrEqualTo(0, $"{Path.GetFileName(workflow)} declares jobs");

        var current = string.Empty;
        var body = new StringBuilder();
        for (var index = start + 1; index < lines.Length; index++)
        {
            var match = header.Match(lines[index]);
            if (match.Success)
            {
                if (current.Length > 0)
                {
                    jobs.Add((current, body.ToString()));
                }

                current = match.Groups[1].Value;
                body.Clear();
                continue;
            }

            body.AppendLine(lines[index]);
        }

        if (current.Length > 0)
        {
            jobs.Add((current, body.ToString()));
        }

        return jobs;
    }

    /// <summary>
    /// The coverage the build collects is also read.
    /// </summary>
    /// <remarks>
    /// The workflow collected cobertura and uploaded it with if-no-files-found:
    /// error — a guarantee that the file exists, with no threshold anywhere, so
    /// deleting tests or adding a module without any passed every gate green.
    /// The frontend has had a ratchet since it had tests at all.
    /// </remarks>
    [Fact]
    public void CheckTheCoverageTheBuildCollects()
    {
        var workflow = File.ReadAllText(
            Path.Combine(RepositoryRoot, ".github", "workflows", "dotnet.yml"));

        File.Exists(Path.Combine(RepositoryRoot, "scripts", "check-coverage.sh"))
            .Should().BeTrue("the threshold lives beside the reason for it, in the script");

        var collection = workflow.IndexOf("--collect:", StringComparison.Ordinal);
        var check = workflow.IndexOf("check-coverage.sh", StringComparison.Ordinal);

        check.Should().BeGreaterThan(-1, "a number nobody reads is a report, not a gate");
        collection.Should().BeGreaterThan(-1, "the run still collects the coverage");
        collection.Should().BeLessThan(check, "the check reads what the run collected");
    }

    /// <summary>
    /// Every alert rule reaches something that can deliver it.
    /// </summary>
    /// <remarks>
    /// Prometheus evaluated fifteen rules and posted them nowhere: the file
    /// declared rule_files and no alerting section, and no alertmanager existed to
    /// declare. ApiDown, PostgresDown and DiskSpaceLow all reached a firing state
    /// and stayed there, so the only way to learn of an outage was to open /alerts
    /// through a tunnel — which takes already suspecting there is one.
    ///
    /// The whole chain is asserted because every link of it fails silently and
    /// nothing else executes any of it: a rule file with no alerting section, an
    /// alertmanagers list with no target, a target naming a host no service
    /// answers to, and a receiver declared as a bare name — which alertmanager
    /// accepts and which drops everything routed to it — all leave a stack that
    /// looks configured and delivers nothing.
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

        // The receiver is watched like anything else it delivers alerts about. It
        // cannot report its own absence - the report would go through it - but a rule
        // firing on the page, and a letter the moment it is back saying how long the
        // contour had no mouth, is the difference between finding out and not.
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
    /// Every other rule speaks when the site is unwell, and none of them speaks
    /// when the contour itself breaks: a relay refusing mail, an alertmanager that
    /// stopped, a rule file that failed to load. Silence then reads exactly like
    /// health, which is why a whole set of alerts spent its life undelivered
    /// without anybody noticing.
    ///
    /// Delivered by the ordinary receiver rather than by one of its own: a channel
    /// that only the heartbeat uses proves the health of a channel nothing else
    /// uses. Watching for its absence is outside the machine by construction — a
    /// watchman living on the observed host cannot report its own death — and that
    /// part is written in the deployment guide, not here.
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
    /// Alertmanager expands no environment variable in its configuration file and
    /// has no include mechanism, so the smarthost and the credentials of a relay
    /// are either rendered in at start-up or committed. The entrypoint renders
    /// them; what this holds is that the two files and compose agree on the set,
    /// because each way of disagreeing is quiet. A placeholder the script does not
    /// know reaches alertmanager as the literal text "${ALERT_...}" — a smarthost
    /// nobody owns, with the configuration valid and the container healthy — and a
    /// value compose does not pass is a value that cannot be changed without
    /// editing the repository.
    /// </remarks>
    [Fact]
    public void ConfigureTheAlertReceiverFromTheEnvironmentRatherThanFromTheRepository()
    {
        var delivery = Read(AlertDelivery);
        var script = Read(AlertDeliveryEntrypoint);
        var service = ServiceWithContainerName(Read(BaseCompose), "dm-alertmanager");

        // The two links that make the rendering happen at all, and the two the
        // rest of this check silently assumed. Both CI steps mount the template
        // and the script by explicit path and set an entrypoint of their own, so
        // neither notices compose losing either line — and compose without them
        // is a container that reads the template as it stands, fails on
        // "${ALERT_SMTP_SMARTHOST}: missing port in address", and never starts.
        // Which is the state the whole row began in: fifteen rules, no receiver.
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

    /// <summary>
    /// A value that lands inside a URI stays a value, not a delimiter.
    /// </summary>
    /// <remarks>
    /// A connection string puts a password between the colon and the at sign:
    /// the one the Postgres exporter scrapes with. The template shipped
    /// TestP@ss123! for every account, so the driver cut the string at that at
    /// sign, called it invalid, and the consumer aborted on start with no
    /// request ever made. Nothing caught it because init-env.sh replaces the
    /// passwords only in server mode: the local stand and all three CI jobs
    /// that copy the template ran on the broken value.
    ///
    /// The check is on the template because the template is what those jobs and
    /// the documented first run copy. A password chosen by an operator is their
    /// own business, and the file says which characters are safe.
    /// </remarks>
    [Fact]
    public void GiveTheTemplateAPasswordThatSurvivesBeingPutInAUri()
    {
        var compose = Read(BaseCompose);
        var template = Read(EnvironmentTemplate);

        // The password position of a URI: scheme://user:${VAR}@host. The user
        // part may itself be a substitution carrying a default, so it is
        // matched as either a whole ${...} or a plain character - a class that
        // merely excluded the colon used to skip exactly the line this check
        // exists for.
        var embedded = Regex.Matches(compose, @"://(?:\$\{[^}]*\}|[^\s:@/])*:\$\{(?<name>\w+)[^}]*\}@")
            .Select(match => match.Groups["name"].Value)
            .Distinct()
            .ToList();

        embedded.Should().Contain("DM_EXPORTER_PASSWORD",
            "the exporter's connection string puts a password between a colon and an at " +
            "sign, and an extraction that misses it passes on anything");

        foreach (var name in embedded)
        {
            var value = Regex.Match(template, $@"^{name}=(?<value>.*)$", RegexOptions.Multiline);

            value.Success.Should().BeTrue(
                $"{name} reaches a URI and docker/.env.example is what the CI jobs and the " +
                "documented first run copy");

            // Everything a URI accepts between the colon and the at sign, from
            // RFC 3986: unreserved and sub-delims. Anything else has to be
            // percent-encoded, and nothing on the way in encodes it.
            value.Groups["value"].Value.Should().MatchRegex(@"^[A-Za-z0-9\-._~!$&'()*+,;=]+$",
                $"{name} is substituted between the colon and the at sign of a connection " +
                "string, so a character that a URI reads as a delimiter ends the string early " +
                "and the client rejects it before it connects");
        }
    }

    /// <summary>Compose files and profiles named by the docker compose lines of a file.</summary>
    private static List<string> ComposeSelectors(string text)
    {
        var selectors = text.Split('\n')
            .Where(line => line.Contains("docker compose", StringComparison.Ordinal))
            .SelectMany(line => Regex.Matches(line, @"(?:-f|--file)\s+(\S+\.yml)|--profile\s+(\S+)"))
            .Select(match => match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value)
            .Select(Path.GetFileName)
            .Distinct()
            .ToList();

        selectors.Should().NotBeEmpty(
            "an extraction that finds nothing compares equal to another that finds nothing");
        return selectors!;
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

    /// <summary>The body of the service a container name belongs to.</summary>
    /// <remarks>
    /// By container name rather than by service name: a target is written as the
    /// hostname another container resolves, and that is the container name.
    /// </remarks>
    private static string ServiceWithContainerName(string compose, string containerName)
    {
        var lines = compose.Split('\n');
        var marker = Array.FindIndex(lines, line =>
            line.TrimStart().StartsWith("container_name:", StringComparison.Ordinal) &&
            line.Contains(containerName, StringComparison.Ordinal));

        marker.Should().BeGreaterThan(-1, $"no service of the stack answers to {containerName}");

        var start = marker;
        while (start > 0 && !Regex.IsMatch(lines[start], @"^  \S"))
        {
            start--;
        }

        return string.Join('\n', lines.Skip(start + 1).TakeWhile(line => !Regex.IsMatch(line, @"^  \S")));
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

    /// <summary>The body of one service, up to the next key at the same indent.</summary>
    private static string ServiceBlock(string compose, string name)
    {
        var lines = compose.Split('\n');
        var start = Array.FindIndex(lines, line => line.StartsWith($"  {name}:", StringComparison.Ordinal));

        start.Should().BeGreaterThan(-1, $"the compose file must declare {name}");
        return string.Join('\n', lines.Skip(start + 1).TakeWhile(line => !Regex.IsMatch(line, @"^  \S")));
    }

    /// <summary>Published ports of every service, as written.</summary>
    private static string[] PublishedPorts(string compose)
    {
        var ports = new List<string>();
        var inPorts = false;
        foreach (var raw in compose.Split('\n'))
        {
            var line = raw.Trim();
            if (line.StartsWith('#')) continue;
            if (line == "ports:") { inPorts = true; continue; }
            if (inPorts && line.StartsWith("- ", StringComparison.Ordinal))
            {
                ports.Add(line[2..].Trim().Trim('\'', '"'));
                continue;
            }

            if (line.Length > 0) inPorts = false;
        }

        return ports.ToArray();
    }

    /// <summary>The first <c>subnet:</c> value, quoted or bare.</summary>
    private static string? FindSubnet(string compose) =>
        FindValue(compose, "subnet:");

    private static string? FindTrustedNetwork(string compose) =>
        FindValue(compose, "DM_ReverseProxyConfiguration__TrustedNetworks__0:");

    private static string? FindValue(string compose, string key) => compose
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => !line.StartsWith('#'))
        .Select(line =>
        {
            var marker = line.IndexOf(key, StringComparison.Ordinal);
            return marker < 0 ? null : line[(marker + key.Length)..].Trim().Trim('\'', '"', '-', ' ');
        })
        .FirstOrDefault(value => !string.IsNullOrEmpty(value));

    /// <summary>Directives that are not commented out.</summary>
    private static string ActiveDirectives(string configuration) => string.Join('\n', configuration
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => !line.StartsWith('#'))
        .Select(line => line.TrimStart('#').Trim()));

    /// <summary>
    /// One block of an nginx configuration and the header directives declared
    /// directly in it.
    /// </summary>
    /// <remarks>
    /// add_header and proxy_hide_header inherit the same way: a level keeps what
    /// the level above it declared only while it declares none of its own. So a
    /// location repeating one add_header keeps none of the server's, and a
    /// location naming one proxy_hide_header stops hiding everything else the
    /// server hid. Both questions are therefore asked of a block and its
    /// ancestors, never of the file as a whole.
    /// </remarks>
    private sealed class NginxBlock
    {
        /// <summary>The block this one is nested in, or null at the top level.</summary>
        public NginxBlock? Parent { get; init; }

        /// <summary>The line that opened the block, without its brace.</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>Headers this block sets with an add_header of its own.</summary>
        public List<string> Added { get; } = new();

        /// <summary>Headers this block drops with a proxy_hide_header of its own.</summary>
        public List<string> Hidden { get; } = new();

        /// <summary>What a response leaving this block carries: its own headers, or the inherited set.</summary>
        public IReadOnlyList<string> Emitted =>
            Added.Count > 0 ? Added : Parent?.Emitted ?? Array.Empty<string>();

        /// <summary>Whether the copy an upstream sent is dropped before the response leaves this block.</summary>
        public bool Hides(string header) => Hidden.Count > 0
            ? Hidden.Contains(header)
            : Parent?.Hides(header) ?? false;
    }

    /// <summary>
    /// Splits a configuration into blocks by braces, keeping the two header
    /// directives of each. Lines that are still comments are skipped: the
    /// commented template arrives with one marker already taken off, and what is
    /// commented inside it is commented out on purpose.
    /// </summary>
    private static IReadOnlyList<NginxBlock> HeaderBlocks(string configuration)
    {
        var blocks = new List<NginxBlock>();
        var open = new Stack<NginxBlock>();

        foreach (var raw in configuration.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var current = open.Count > 0 ? open.Peek() : null;

            var added = Regex.Match(line, @"^add_header\s+([^\s;]+)");
            if (added.Success)
            {
                current?.Added.Add(added.Groups[1].Value);
            }

            var hidden = Regex.Match(line, @"^proxy_hide_header\s+([^\s;]+)");
            if (hidden.Success)
            {
                current?.Hidden.Add(hidden.Groups[1].Value);
            }

            if (line.EndsWith('{'))
            {
                var block = new NginxBlock { Parent = current, Name = line[..^1].Trim() };
                blocks.Add(block);
                open.Push(block);
            }
            else if (line == "}" && open.Count > 0)
            {
                open.Pop();
            }
        }

        return blocks;
    }
}
