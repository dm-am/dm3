using System;
using DM.Domain.Core.Enums;
using DM.Infrastructure.Persistence.Entities.Account;
using DM.Infrastructure.Persistence.Entities.Account.Settings;
using DM.Infrastructure.Persistence.RelationalStorage;
using Microsoft.EntityFrameworkCore;
using SystemUser = DM.Domain.Core.Identity.SystemUser;

namespace DM.Infrastructure.Persistence.ModelConfiguration;

internal static class AccountModel
{
    internal static void Configure(ModelBuilder modelBuilder, bool isPostgres)
    {
        Users(modelBuilder, isPostgres);
        Tokens(modelBuilder);
        Registration(modelBuilder);
        SignIns(modelBuilder);
        Settings(modelBuilder, isPostgres);
        SecondFactor(modelBuilder);
        SecurityAudit(modelBuilder);
    }

    private static void Users(ModelBuilder modelBuilder, bool isPostgres)
    {
        // The system author every automated action is attributed to. Cannot log in:
        // no salt, no hash.
        modelBuilder.Entity<User>().HasData(
            new User
            {
                UserId = SystemUser.Id,
                Username = SystemUser.Username,
                Email = SystemUser.Email,
                CreatedUtc = new DateTimeOffset(2020, 1, 1, 0, 0, 0, TimeSpan.Zero),
                Role = UserRole.System,
                AccessPolicy = AccessPolicy.NotSpecified,
                Salt = "",
                PasswordHash = "",
                PasswordHashVersion = 0,
                RatingDisabled = true,
                QualityRating = 0,
                QuantityRating = 0,
                IsRemoved = false,
                Gender = Gender.Unknown,
                ShowBirthday = false
            });

        // IsNewbie as a stored computed column: the schema's copy of
        // ProbationPolicy.NewbiePostThreshold, which SQL cannot read. The number is
        // repeated here, in the migration and in both snapshots, and an
        // architecture test compares all four with the constant.
        if (isPostgres)
        {
            modelBuilder.Entity<User>()
                .Property(u => u.IsNewbie)
                .HasComputedColumnSql("\"QuantityRating\" < 100", stored: true);
        }

        modelBuilder.Entity<User>()
            .HasOne(u => u.AvatarUpload)
            .WithMany()
            .HasForeignKey(u => u.AvatarUploadId)
            .OnDelete(DeleteBehavior.SetNull);

        // Username and email are looked up by lower(column): EF cannot declare an
        // expression index, so ExpressionIndexInitializer asserts them at startup.

        // OldUsername is globally unique — permanently reserved usernames cannot be reused
        modelBuilder.Entity<UsernameHistory>()
            .HasIndex(h => h.OldUsername).IsUnique();

        // Only one pending request per user
        modelBuilder.Entity<UsernameChangeRequest>()
            .HasIndex(r => r.UserId)
            .HasFilter("\"Status\" = 0")
            .IsUnique();
    }

    private static void Tokens(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Token>()
            .HasOne(t => t.User)
            .WithMany(u => u.Tokens)
            .HasForeignKey(t => t.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<Token>()
            .HasOne(t => t.Creator)
            .WithMany()
            .HasForeignKey(t => t.CreatorId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
        // Every lookup of a mailed confirmation goes by this hash, and two live
        // tokens may not share one. Filtered, because an invitation carries no
        // secret at all: it is redeemed by the addressee while signed in, and its
        // identifier is not a credential.
        modelBuilder.Entity<Token>()
            .HasIndex(t => t.SecretHash)
            .HasDatabaseName("IX_Tokens_SecretHash")
            .IsUnique()
            .HasFilter("\"SecretHash\" IS NOT NULL");
        modelBuilder.Entity<Token>()
            .HasOne(t => t.DeletedBy)
            .WithMany()
            .HasForeignKey(t => t.DeletedByUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Token>()
            .HasIndex(t => new { t.UserId, t.Type });
    }

    private static void Registration(ModelBuilder modelBuilder)
    {
        // PendingRegistration indexes for email-first registration flow
        modelBuilder.Entity<PendingRegistration>(entity =>
        {
            // One pending registration per email
            entity.HasIndex(p => p.Email)
                .HasDatabaseName("IX_PendingRegistrations_Email")
                .IsUnique();

            // Activation is looked up by the hash of the mailed secret, which is
            // also the only form of it the row holds.
            entity.HasIndex(p => p.SecretHash)
                .HasDatabaseName("IX_PendingRegistrations_SecretHash")
                .IsUnique();

            // The sweep of stale pending registrations (AccountRetentionPolicy)
            entity.HasIndex(p => p.CreatedUtc)
                .HasDatabaseName("IX_PendingRegistrations_CreatedUtc");
        });
    }

    private static void SignIns(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserLoginRecord>(entity =>
        {
            // Efficient lookup of a user's login history (sorted by date descending)
            entity.HasIndex(r => new { r.UserId, r.LoginUtc })
                .HasDatabaseName("ix_user_login_records_user_date");

            // Efficient search by IP address (for linked profiles detection)
            entity.HasIndex(r => r.IpAddress)
                .HasDatabaseName("ix_user_login_records_ip");

            entity.HasOne(r => r.User)
                .WithMany(u => u.LoginRecords)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // UserSessions: a row per session, out of the document-per-user with an
        // array. The FK index the convention adds serves the device list and
        // RemoveAllSessions; ExpirationUtc serves the hourly purge.
        modelBuilder.Entity<UserSession>(entity =>
        {
            entity.HasKey(s => s.SessionId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(s => s.ExpirationUtc);
        });

        // LoginAttempts: keyed by the composite string the domain forms
        // (email|address). Email serves ResetAttempts(email), LastAttemptUtc
        // serves the retention sweep.
        modelBuilder.Entity<LoginAttempt>(entity =>
        {
            entity.HasKey(a => a.Key);
            entity.HasIndex(a => a.Email);
            entity.HasIndex(a => a.LastAttemptUtc);
        });
    }

    private static void Settings(ModelBuilder modelBuilder, bool isPostgres)
    {
        // UserSettings: a row per user, keyed by the user. Absence of the row is
        // UserSettings.Default. The paging numbers are NOT NULL columns so a
        // partial row is unrepresentable; the channel preferences are nullable
        // objects with a category set inside — a document shape, hence jsonb.
        modelBuilder.Entity<UserSettings>(entity =>
        {
            entity.HasKey(s => s.UserId);
            entity.HasOne<User>()
                .WithOne()
                .HasForeignKey<UserSettings>(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            // An IANA zone name: the longest one today is under forty characters,
            // and the bound is twice that so the column holds the limit rather
            // than a hope that names stay short.
            entity.Property(s => s.TimeZoneId).HasMaxLength(64);
            entity.Property(s => s.DiscordPreferences).IsJson(isPostgres);
            entity.Property(s => s.TelegramPreferences).IsJson(isPostgres);
            entity.Property(s => s.EmailPreferences).IsJson(isPostgres);
        });
    }

    private static void SecondFactor(ModelBuilder modelBuilder)
    {
        // Keyed by the account: one factor per person as a constraint. RemovalDueUtc
        // is partially indexed for the sweep, its only reader.
        modelBuilder.Entity<UserTwoFactor>(entity =>
        {
            entity.HasKey(f => f.UserId);
            entity.HasOne<User>()
                .WithOne()
                .HasForeignKey<UserTwoFactor>(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(f => f.RemovalDueUtc).HasFilter("\"RemovalDueUtc\" IS NOT NULL");
        });

        // UserTwoFactorRecoveryCodes: ten rows per account, read as a set and
        // written as a set. The FK index the convention adds is what serves both.
        modelBuilder.Entity<UserTwoFactorRecoveryCode>(entity =>
        {
            entity.HasKey(c => c.RecoveryCodeId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TwoFactorChallenges: a stream with a term. CreatedUtc serves the
        // retention sweep; the FK index serves nothing today and exists because
        // the cascade from a deleted account needs it not to scan.
        modelBuilder.Entity<TwoFactorChallenge>(entity =>
        {
            entity.HasKey(c => c.ChallengeId);
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(c => c.CreatedUtc);
        });
    }

    private static void SecurityAudit(ModelBuilder modelBuilder)
    {
        // SecurityAuditEntries: append-only stream, read as "this user's
        // events, newest first" — the compound index serves the equality and
        // hands the sort back ordered.
        modelBuilder.Entity<SecurityAuditEntry>(entity =>
        {
            entity.HasOne<User>()
                .WithMany()
                .HasForeignKey(e => e.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.UserId, e.TimestampUtc })
                .IsDescending(false, true);
        });
    }
}
