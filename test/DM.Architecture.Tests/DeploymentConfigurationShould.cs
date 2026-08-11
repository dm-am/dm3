using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DM.Domain.Core.Uploads;
using FluentAssertions;
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
    private const string MirrorCompose = "docker-compose.mirror.yml";
    private const string NginxConfiguration = "nginx/nginx.conf";
    private const string ScrapeConfiguration = "prometheus.yml";
    private const string AlertDelivery = "prometheus/alertmanager.yml";
    private const string AlertDeliveryEntrypoint = "alertmanager-init.sh";
    private const string EnvironmentTemplate = ".env.example";

    /// <summary>
    /// Walks up from the test binary to the repository root. The compose files are
    /// not copied to the output directory, and copying them would let this assert
    /// against a stale snapshot.
    /// </summary>
    private static string DockerDirectory
    {
        get
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "docker")))
            {
                directory = directory.Parent;
            }

            directory.Should().NotBeNull("the repository root must be above the test binary");
            return Path.Combine(directory!.FullName, "docker");
        }
    }

    /// <summary>The checkout the docker directory belongs to.</summary>
    private static string RepositoryRoot => Directory.GetParent(DockerDirectory)!.FullName;

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

        var running = ActiveDirectives(nginx);
        running.Should().Contain(location,
            $"the API hands out {endpoint}/... and nginx routes by prefix");
        running.Should().Contain(upstream,
            "the container publishes on loopback, so the application network is the " +
            "only way in, and the trailing slash strips the prefix the signature " +
            "does not cover");

        var template = CommentedDirectives(nginx);
        template.Should().Contain(location,
            "the commented server is what gets switched on the day certificates " +
            "arrive, and a route missing from it comes back as a blank avatar");
        template.Should().Contain(upstream,
            "the same route to the same upstream");
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

        var scheduled = new[] { "postgres", "mongodb", "minio" }
            .Where(store => cron.Contains($"backup-{store}.sh", StringComparison.Ordinal))
            .ToList();

        scheduled.Should().HaveCount(3, "the parser must find the scheduled backups");
        foreach (var store in scheduled)
        {
            verify.Should().Contain($"/var/backups/{store}",
                $"{store} is backed up nightly, so the verifier has to look at it");
        }
    }

    /// <summary>
    /// Every backup script loads the environment file. Cron hands a job almost
    /// nothing, and these scripts need credentials — the MinIO one exits 1 without
    /// its password, and the offsite replication in all three switches itself off
    /// silently when the AWS variables are absent.
    /// </summary>
    [Theory]
    [InlineData("backup-postgres.sh")]
    [InlineData("backup-mongodb.sh")]
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
    /// Object storage is the one store whose contents nothing can rebuild:
    /// Postgres and Mongo hold references to uploaded files, the files are the
    /// data. So the account a workload holds decides what a leaked configuration
    /// costs.
    /// </summary>
    /// <remarks>
    /// Mongo already draws this line — mongo-init.js creates an application user
    /// with readWrite on one database and refuses to start without it — while the
    /// application, every worker and imgproxy were handed the MinIO root account,
    /// which can drop the bucket, rewrite its policy and mint further accounts.
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
    /// The scoped accounts have to exist before the first upload, and only root
    /// can create them — which is the whole of what root is still for.
    /// </summary>
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
    /// was the single service with state and no named volume — pg, mongo, loki and
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
    /// key, both Mongo passwords and both MinIO accounts through ${...:?}, which
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
    /// into the Mongo and MinIO users at first boot, so rotating it on a live
    /// stand locks the API out of its own stores — the script refuses instead.
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
        environmentBlock[0].Should().NotEndWith("EXISTING\" = 0 ]; then\n",
            "setting it only on a freshly created file is what left a hand-copied " +
            "server .env running in Development");

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

        var edge = Regex.Match(
            ActiveDirectives(Read(NginxConfiguration)), @"client_max_body_size (\d+)m;");
        edge.Success.Should().BeTrue(
            "without the directive the edge cuts every body at its own default of 1 MB");

        edge.Groups[1].Value.Should().Be(endpoint.Groups[1].Value,
            "a lower limit at the edge refuses what the endpoint accepts, and a higher one " +
            "carries the whole body across the network to be refused at the end of it");
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

        foreach (var server in new[] { ActiveDirectives(nginx), CommentedDirectives(nginx) })
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
    /// results, and two empty results compare equal: a switch to --file, to
    /// COMPOSE_FILE, or to a path outside the character class it matches would
    /// have left the gate green on nothing at all. What it exists to catch is a
    /// unit that brings up the base topology without nginx and the SPA, so a
    /// reboot replaces the site with a bare API.
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
    /// documented command brought up seventeen services: a local Postgres, Mongo
    /// and MinIO, a migration container that would have run Migrate() against the
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
        var overlay = Read(MirrorCompose);
        overlay.Should().MatchRegex(@"depends_on: !(reset|override)",
            "compose starts the dependencies of a named service, and the edge declared one on the API");
        overlay.Should().Contain("pop.conf.template",
            "the door has its own edge configuration: its upstream is across the network, " +
            "and the shared one points at a container that does not run here");

        var documents = new[] { "MIRRORING.md", "DEPLOYMENT.md" }
            .Select(name => File.ReadAllText(
                Path.Combine(RepositoryRoot, "docs", "guides", name)));

        var commands = documents
            .SelectMany(document => document.Split('\n'))
            .Where(line => line.Contains("--env-file .env.mirror", StringComparison.Ordinal))
            .ToList();

        commands.Should().NotBeEmpty("the guides still document how the door is started");
        foreach (var command in commands)
        {
            command.Should().Contain(MirrorCompose,
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

            foreach (var elsewhere in new[] { "dmapi", "postgres", "mongo", "minio", "migration" })
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

        Read(MirrorCompose).Should().Contain("NGINX_ENVSUBST_FILTER",
            "without a filter the substitution eats $host and $scheme, which belong to nginx");
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
    /// minio/mc is the documented exception. It speaks to the server over the
    /// admin API and the two are released together, so pinning the client apart
    /// from the server is the failure mode rather than the fix.
    ///
    /// "Names a version" used to be checked as "the last path segment contains a
    /// colon", which <c>containrrr/watchtower:latest</c> satisfies — the single
    /// most dangerous reference here could go back to a floating tag with this
    /// test green. The tag is now read out and refused by name, and the images
    /// this repository publishes itself are checked separately: their tag comes
    /// from IMAGE_TAG, chosen per branch by the deployment, so what is required
    /// of them is that the choice stays a variable and is never written in.
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

        var references = composeImages
            .Concat(dockerfileImages)
            .Where(reference => reference != "minio/mc")
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
        start.Should().BeGreaterOrEqualTo(0, $"{Path.GetFileName(workflow)} declares jobs");

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
    /// Two connection strings put a password between the colon and the at sign:
    /// the Mongo one the API reads, and the one the Postgres exporter scrapes
    /// with. The template shipped TestP@ss123! for every account, so the driver
    /// cut the string at that at sign, called it invalid, and the API aborted on
    /// start with no request ever made. Nothing caught it because init-env.sh
    /// replaces the passwords only in server mode: the local stand and all three
    /// CI jobs that copy the template ran on the broken value.
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
        // part may itself be a substitution carrying a default, ${MONGO_USER:-dm},
        // so it is matched as either a whole ${...} or a plain character - a
        // class that merely excluded the colon skipped the Mongo line, which is
        // the one line this check exists for.
        var embedded = Regex.Matches(compose, @"://(?:\$\{[^}]*\}|[^\s:@/])*:\$\{(?<name>\w+)[^}]*\}@")
            .Select(match => match.Groups["name"].Value)
            .Distinct()
            .ToList();

        embedded.Should().Contain("MONGO_PASSWORD",
            "the connection string the API reads is the one that put a password between a " +
            "colon and an at sign, and an extraction that misses it passes on anything");

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

    /// <summary>Directives of the server that runs, without the commented template.</summary>
    private static string ActiveDirectives(string configuration) =>
        Directives(configuration, commented: false);

    /// <summary>The commented template, with the comment markers taken off.</summary>
    private static string CommentedDirectives(string configuration) =>
        Directives(configuration, commented: true);

    private static string Directives(string configuration, bool commented) => string.Join('\n', configuration
        .Split('\n')
        .Select(line => line.Trim())
        .Where(line => line.StartsWith('#') == commented)
        .Select(line => line.TrimStart('#').Trim()));
}
