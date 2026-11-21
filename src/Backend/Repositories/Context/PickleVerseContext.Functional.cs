using Entities.DTOs;
using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context;

public partial class PickleVerseContext
{
    public DbSet<ContentCatalogRecord> ContentCatalogs => Set<ContentCatalogRecord>();
    public DbSet<ProgressRewardGrant> ProgressRewardGrants => Set<ProgressRewardGrant>();
    public DbSet<DedicatedOperationRecord> DedicatedOperations => Set<DedicatedOperationRecord>();
    public DbSet<AdminAuditRecord> AdminAudits => Set<AdminAuditRecord>();
    public DbSet<TechniqueReferenceRecord> TechniqueReferences => Set<TechniqueReferenceRecord>();
    public DbSet<ExpertReviewRecord> ExpertReviews => Set<ExpertReviewRecord>();
    public DbSet<CoachingDemoSessionRecord> CoachingDemoSessions => Set<CoachingDemoSessionRecord>();

    static void ConfigureFunctional(ModelBuilder model)
    {
        void Columns<T>(string table, params string[] key) where T : class
        {
            var entity = model.Entity<T>();
            entity.ToTable(table); entity.HasKey(key);
            foreach (var property in entity.Metadata.GetProperties()) property.SetColumnName(property.Name.ToLowerInvariant());
        }
        Columns<ContentCatalogRecord>("contentcatalogs", nameof(ContentCatalogRecord.Version));
        model.Entity<ContentCatalogRecord>().Property(x => x.Data).HasColumnType("jsonb");
        Columns<ProgressRewardGrant>("progressrewardgrants", nameof(ProgressRewardGrant.PlayerId), nameof(ProgressRewardGrant.RewardKey));
        model.Entity<ProgressRewardGrant>().Property(x => x.Details).HasColumnType("jsonb");
        model.Entity<ProgressRewardGrant>().HasOne<Playerprofile>().WithMany().HasForeignKey(x => x.PlayerId);
        Columns<DedicatedOperationRecord>("dedicatedoperations", nameof(DedicatedOperationRecord.OperationId));
        model.Entity<DedicatedOperationRecord>().HasIndex(x => x.MatchId).IsUnique();
        model.Entity<DedicatedOperationRecord>().HasOne<Gamematch>().WithMany().HasForeignKey(x => x.MatchId);
        model.Entity<Lesson>().Property(x => x.PublishStatus).HasColumnName("publishstatus")
            .HasConversion(v => StatusWire.Format(v), v => StatusWire.Parse<ContentPublishStatus>(v));
        model.Entity<Lesson>().Property(x => x.DraftConfig).HasColumnName("draftconfig").HasColumnType("jsonb");
        model.Entity<Lesson>().Property(x => x.ContentVersion).HasColumnName("contentversion");
        model.Entity<Playerprofile>().Property(x => x.LearningEpoch).HasColumnName("learningepoch");
        model.Entity<Playerprofile>().Property(x => x.LearningResetAt).HasColumnName("learningresetat");
        model.Entity<Playerprofile>().Property(x => x.MissionEpoch).HasColumnName("missionepoch");
        model.Entity<Playerprofile>().Property(x => x.EconomyEpoch).HasColumnName("economyepoch");
        model.Entity<Playerprofile>().Property(x => x.TutorialCheckpoint).HasColumnName("tutorialcheckpoint");
        model.Entity<Playerprofile>().Property(x => x.ExperienceChoice).HasColumnName("experiencechoice");
        Columns<AdminAuditRecord>("adminaudits", nameof(AdminAuditRecord.AuditId));
        model.Entity<AdminAuditRecord>().Property(x => x.Details).HasColumnType("jsonb");
        Columns<TechniqueReferenceRecord>("techniquereferences", nameof(TechniqueReferenceRecord.ReferenceId));
        model.Entity<TechniqueReferenceRecord>().Property(x => x.Data).HasColumnType("jsonb");
        model.Entity<TechniqueReferenceRecord>().HasOne<Technique>().WithMany().HasForeignKey(x => x.TechniqueId);
        model.Entity<TechniqueReferenceRecord>().HasIndex(x => new { x.TechniqueId, x.Version }).IsUnique();
        Columns<ExpertReviewRecord>("expertreviews", nameof(ExpertReviewRecord.ReviewId));
        model.Entity<ExpertReviewRecord>().Property(x => x.Data).HasColumnType("jsonb");
        model.Entity<ExpertReviewRecord>().HasOne<AiTelemetrySession>().WithMany().HasForeignKey(x => x.BatchId);
        model.Entity<ExpertReviewRecord>().HasOne<User>().WithMany().HasForeignKey(x => x.ReviewerId);
        model.Entity<ExpertReviewRecord>().HasIndex(x => new { x.BatchId, x.Version }).IsUnique();
        Columns<CoachingDemoSessionRecord>("coachingdemosessions", nameof(CoachingDemoSessionRecord.SessionId));
        var demo = model.Entity<CoachingDemoSessionRecord>();
        demo.HasIndex(x => x.PlayerCode).IsUnique();
        demo.Property(x => x.Tier).HasConversion(v => StatusWire.Format(v), v => StatusWire.Parse<ExpertTier>(v));
        demo.Property(x => x.Split).HasConversion(v => StatusWire.Format(v), v => StatusWire.Parse<DatasetSplit>(v));
        demo.Property(x => x.Shots).HasColumnType("jsonb");
    }
}
