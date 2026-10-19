using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

/// <summary>
/// The backups the installer schedules are verified, run with the environment
/// they need, and kept out of anything that goes with the stack.
/// </summary>
/// <remarks>Cron runs these with almost nothing and nobody watches them fail. DEPLOYMENT.md names the scripts.</remarks>
public class BackupConfigurationShould
{
    /// <summary>
    /// Whatever the installer schedules, the verifier looks at.
    /// </summary>
    /// <remarks>A verifier that skips one backup says "All backups OK" out loud.</remarks>
    [Fact]
    public void VerifyEveryBackupTheInstallerSchedules()
    {
        var scriptsDirectory = Path.Combine(DockerDirectory, "scripts");
        var cron = File.ReadAllText(Path.Combine(scriptsDirectory, "install-cron.sh"));
        var verify = File.ReadAllText(Path.Combine(scriptsDirectory, "verify-backup.sh"));

        var scheduled = new[] { "postgres", "storage" }
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
    /// The alert about a backup sends the reader where the backup writes.
    /// </summary>
    /// <remarks>promtool does not read the sentence; the installer, which creates the log, is the source.</remarks>
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
    /// Every backup script loads the environment file: cron hands a job no credentials, and the
    /// offsite replication switches itself off without them.
    /// </summary>
    [Theory]
    [InlineData("backup-postgres.sh")]
    [InlineData("backup-storage.sh")]
    [InlineData("verify-backup.sh")]
    public void LoadTheEnvironmentInEveryScheduledScript(string script)
    {
        var path = Path.Combine(DockerDirectory, "scripts", script);

        File.Exists(path).Should().BeTrue($"{script} is scheduled by install-cron.sh");
        File.ReadAllText(path).Should().Contain("_env.sh",
            "cron gives the job no environment, and these scripts need credentials");
    }

    /// <summary>
    /// The backups live on the host, out of reach of "docker compose down -v".
    /// </summary>
    /// <remarks>Named volumes are what "down -v" removes, and the developer reset runs it.</remarks>
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
}
