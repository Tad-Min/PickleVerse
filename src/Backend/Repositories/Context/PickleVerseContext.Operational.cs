using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context;

public partial class PickleVerseContext
{
    public DbSet<CosmeticPrice> CosmeticPrices => Set<CosmeticPrice>();
    public DbSet<GameCommandRecord> GameCommandRecords => Set<GameCommandRecord>();
    public DbSet<CurrencyTransaction> CurrencyTransactions => Set<CurrencyTransaction>();
    public DbSet<TrainingSessionRecord> TrainingSessionRecords => Set<TrainingSessionRecord>();
    public DbSet<AiTelemetrySession> AiTelemetrySessions => Set<AiTelemetrySession>();
    public DbSet<AiTelemetryPart> AiTelemetryParts => Set<AiTelemetryPart>();
    public DbSet<AiAnalysisJob> AiAnalysisJobs => Set<AiAnalysisJob>();
    public DbSet<AiCoachingSession> AiCoachingSessions => Set<AiCoachingSession>();
    public DbSet<AiCoachingProfile> AiCoachingProfiles => Set<AiCoachingProfile>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CosmeticPrice>(entity =>
        {
            entity.ToTable("cosmeticprices");
            entity.HasKey(e => new { e.CosmeticId, e.CurrencyId });
            entity.Property(e => e.CosmeticId).HasColumnName("cosmeticid");
            entity.Property(e => e.CurrencyId).HasColumnName("currencyid");
            entity.Property(e => e.Price).HasColumnName("price");
        });
        modelBuilder.Entity<GameCommandRecord>(entity =>
        {
            entity.ToTable("gamecommands");
            entity.HasKey(e => new { e.PlayerId, e.CommandId });
            entity.Property(e => e.PlayerId).HasColumnName("playerid");
            entity.Property(e => e.CommandId).HasColumnName("commandid");
            entity.Property(e => e.Kind).HasColumnName("kind");
            entity.Property(e => e.Fingerprint).HasColumnName("fingerprint");
            entity.Property(e => e.Result).HasColumnName("result").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("createdat").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        modelBuilder.Entity<CurrencyTransaction>(entity =>
        {
            entity.ToTable("currencytransactions");
            entity.HasKey(e => e.TransactionId);
            entity.Property(e => e.TransactionId).HasColumnName("transactionid").ValueGeneratedOnAdd();
            entity.Property(e => e.PlayerId).HasColumnName("playerid");
            entity.Property(e => e.CurrencyId).HasColumnName("currencyid");
            entity.Property(e => e.CommandId).HasColumnName("commandid");
            entity.Property(e => e.Delta).HasColumnName("delta");
            entity.Property(e => e.BalanceAfter).HasColumnName("balanceafter");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.CreatedAt).HasColumnName("createdat").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        modelBuilder.Entity<TrainingSessionRecord>(entity =>
        {
            entity.ToTable("trainingsessions");
            entity.HasKey(e => e.AttemptId);
            entity.Property(e => e.AttemptId).HasColumnName("attemptid");
            entity.Property(e => e.Sequence).HasColumnName("sequence").UseIdentityAlwaysColumn();
            entity.Property(e => e.PlayerId).HasColumnName("playerid");
            entity.Property(e => e.LessonId).HasColumnName("lessonid");
            entity.Property(e => e.DrillId).HasColumnName("drillid");
            entity.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb");
            entity.Property(e => e.Completed).HasColumnName("completed");
            entity.Property(e => e.IsCurrent).HasColumnName("iscurrent").HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnName("createdat").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedat").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        modelBuilder.Entity<AiTelemetrySession>(entity =>
        {
            entity.ToTable("aitelemetrysessions");
            entity.HasKey(e => e.BatchId);
            entity.Property(e => e.BatchId).HasColumnName("batchid");
            entity.Property(e => e.PlayerId).HasColumnName("playerid");
            entity.Property(e => e.SessionId).HasColumnName("sessionid");
            entity.Property(e => e.SessionType).HasColumnName("sessiontype");
            entity.Property(e => e.PartCount).HasColumnName("partcount");
            entity.Property(e => e.Metadata).HasColumnName("metadata").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("createdat").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        modelBuilder.Entity<AiTelemetryPart>(entity =>
        {
            entity.ToTable("aitelemetryparts");
            entity.HasKey(e => new { e.BatchId, e.PartIndex });
            entity.Property(e => e.BatchId).HasColumnName("batchid");
            entity.Property(e => e.PartIndex).HasColumnName("partindex");
            entity.Property(e => e.Fingerprint).HasColumnName("fingerprint");
            entity.Property(e => e.Shots).HasColumnName("shots").HasColumnType("jsonb");
        });
        modelBuilder.Entity<AiAnalysisJob>(entity =>
        {
            entity.ToTable("aianalysisjobs");
            entity.HasKey(e => e.BatchId);
            entity.Property(e => e.BatchId).HasColumnName("batchid");
            entity.Property(e => e.State).HasColumnName("state").HasDefaultValue("pending");
            entity.Property(e => e.Attempts).HasColumnName("attempts");
            entity.Property(e => e.AvailableAt).HasColumnName("availableat").HasDefaultValueSql("CURRENT_TIMESTAMP");
            entity.Property(e => e.LeaseToken).HasColumnName("leasetoken");
            entity.Property(e => e.LeaseUntil).HasColumnName("leaseuntil");
            entity.Property(e => e.Request).HasColumnName("request").HasColumnType("jsonb");
            entity.Property(e => e.RuleVersion).HasColumnName("ruleversion").HasDefaultValue("rules-v1");
            entity.Property(e => e.LastError).HasColumnName("lasterror");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedat").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        modelBuilder.Entity<AiCoachingSession>(entity =>
        {
            entity.ToTable("aicoachingsessions");
            entity.HasKey(e => e.BatchId);
            entity.Property(e => e.BatchId).HasColumnName("batchid");
            entity.Property(e => e.PlayerId).HasColumnName("playerid");
            entity.Property(e => e.SessionId).HasColumnName("sessionid");
            entity.Property(e => e.SessionType).HasColumnName("sessiontype");
            entity.Property(e => e.Result).HasColumnName("result").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("createdat").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        modelBuilder.Entity<AiCoachingProfile>(entity =>
        {
            entity.ToTable("aicoachingprofiles");
            entity.HasKey(e => e.PlayerId);
            entity.Property(e => e.PlayerId).HasColumnName("playerid");
            entity.Property(e => e.BatchId).HasColumnName("batchid");
            entity.Property(e => e.Result).HasColumnName("result").HasColumnType("jsonb");
            entity.Property(e => e.UpdatedAt).HasColumnName("updatedat").HasDefaultValueSql("CURRENT_TIMESTAMP");
        });
        modelBuilder.Entity<Cosmetic>(entity =>
        {
            entity.Property(e => e.Config).HasColumnName("config").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        });
        modelBuilder.Entity<Mission>(entity =>
        {
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.Config).HasColumnName("config").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        });
        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.Property(e => e.Code).HasColumnName("code");
            entity.Property(e => e.SortOrder).HasColumnName("sortorder");
            entity.Property(e => e.Config).HasColumnName("config").HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb");
        });
        modelBuilder.Entity<Drill>(entity =>
        {
            entity.Property(e => e.Code).HasColumnName("code");
        });
        modelBuilder.Entity<Playerlessondrill>(entity =>
        {
            entity.Property(e => e.Attempts).HasColumnName("attempts");
            entity.Property(e => e.Retries).HasColumnName("retries");
            entity.Property(e => e.Completions).HasColumnName("completions");
            entity.Property(e => e.Trials).HasColumnName("trials");
            entity.Property(e => e.Successes).HasColumnName("successes");
            entity.Property(e => e.Seconds).HasColumnName("seconds");
            entity.Property(e => e.BestAccuracy).HasColumnName("bestaccuracy");
            entity.Property(e => e.Errors).HasColumnName("errors").HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb");
        });
    }
}
