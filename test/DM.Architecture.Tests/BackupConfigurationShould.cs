using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AwesomeAssertions;
using DM.Testing;
using Xunit;
using static DM.Architecture.Tests.DeploymentFiles;

namespace DM.Architecture.Tests;

// Cron runs these with almost nothing and nobody watches them fail.
public class BackupConfigurationShould
{
    // A verifier that skips one backup says "All backups OK" out loud.
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
    /// Cron runs every backup through bash and from the scripts _paths.sh names.
    /// </summary>
    /// <remarks>
    /// A line that runs the script itself depends on its executable bit, which a checkout on
    /// Windows or an archive can drop, and cron reports that failure to nobody. The path comes
    /// from _paths.sh so that on a server it goes through rel/current and follows each release.
    /// </remarks>
    [Fact]
    public void RunEveryScheduledBackupThroughBashFromThePathsHelper()
    {
        var cron = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "install-cron.sh"));

        cron.Should().Contain(". \"$(dirname \"${BASH_SOURCE[0]}\")/_paths.sh\"",
            "the scripts cron runs are where _paths.sh says, a release's through rel/current");
        cron.Should().Contain("SCRIPT_DIR=\"$DM_CRON_SCRIPTS_DIR\"",
            "and the schedule takes that directory rather than one of its own");

        var scheduled = Regex.Matches(cron, @"(?m)^[0-9*][0-9* ]* (\S.*)$")
            .Select(match => match.Groups[1].Value)
            .ToList();
        scheduled.Should().HaveCount(3, "the parser must find the three scheduled lines");
        scheduled.Should().OnlyContain(command => command.StartsWith("bash $SCRIPT_DIR/", StringComparison.Ordinal),
            "a scheduled line that relies on the executable bit fails silently when the bit is lost");
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
    /// The nightly dump leaves out the rows of the outbox table, where a waiting letter keeps a
    /// live confirmation link in plaintext.
    /// </summary>
    /// <remarks>
    /// pg_dump folds an unquoted pattern to lower case, so OutboxEvents without the double quotes
    /// excludes nothing and the dump still succeeds. Whether pg_dump honours the pattern is
    /// proven on a real dump by check-dump-excludes-outbox.sh in deployment-smoke.
    /// </remarks>
    [Fact]
    public void ExcludeTheOutboxRowsFromTheDumpByTheirQuotedTableName()
    {
        var backup = File.ReadAllText(Path.Combine(DockerDirectory, "scripts", "backup-postgres.sh"));

        backup.Should().Contain("--exclude-table-data='\"OutboxEvents\"'",
            "a stolen dump or its offsite copy must hold no working confirmation link");
        File.ReadAllText(Path.Combine(RepositoryLayout.Root, ".github", "workflows", "dotnet.yml"))
            .Should().Contain("bash docker/scripts/check-dump-excludes-outbox.sh",
                "only a dump read back shows that pg_dump matched the pattern");
    }

    // Named volumes are what "down -v" removes, and the developer reset runs it.
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
