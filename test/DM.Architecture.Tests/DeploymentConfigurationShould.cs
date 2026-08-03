using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
    private const string NginxConfiguration = "nginx/nginx.conf";

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
    /// </remarks>
    [Fact]
    public void CreateTheEnvironmentFileBeforeTheDeveloperScriptStartsTheStack()
    {
        var script = File.ReadAllText(Path.Combine(RepositoryRoot, "scripts", "dm.sh"));

        var creation = script.IndexOf("init-env.sh", StringComparison.Ordinal);
        var start = script.IndexOf("docker compose up", StringComparison.Ordinal);

        creation.Should().BeGreaterThan(-1,
            "the generator is the one place that creates docker/.env, and a second copy of that " +
            "step is how the two platforms drifted apart in the first place");
        start.Should().BeGreaterThan(-1, "the script is still the thing that starts the stack");
        creation.Should().BeLessThan(start,
            "compose stops on interpolation before it starts anything");
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
