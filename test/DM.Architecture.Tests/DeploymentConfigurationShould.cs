using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        var root = Directory.GetParent(DockerDirectory)!.FullName;
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
}
