using System;
using System.Collections.Generic;
using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context;

public partial class PickleVerseContext : DbContext
{
    public PickleVerseContext()
    {
    }

    public PickleVerseContext(DbContextOptions<PickleVerseContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Achievement> Achievements { get; set; }

    public virtual DbSet<Blockeduser> Blockedusers { get; set; }

    public virtual DbSet<Cosmetic> Cosmetics { get; set; }

    public virtual DbSet<Currency> Currencies { get; set; }

    public virtual DbSet<Drill> Drills { get; set; }

    public virtual DbSet<Friendrequest> Friendrequests { get; set; }

    public virtual DbSet<Friendship> Friendships { get; set; }

    public virtual DbSet<Lesson> Lessons { get; set; }

    public virtual DbSet<Lessondrill> Lessondrills { get; set; }

    public virtual DbSet<Matchhistory> Matchhistories { get; set; }

    public virtual DbSet<Matchhistoryreplay> Matchhistoryreplays { get; set; }

    public virtual DbSet<Mission> Missions { get; set; }

    public virtual DbSet<Playerachievement> Playerachievements { get; set; }

    public virtual DbSet<Playercosmetic> Playercosmetics { get; set; }

    public virtual DbSet<Playercurrency> Playercurrencies { get; set; }

    public virtual DbSet<Playerlessondrill> Playerlessondrills { get; set; }

    public virtual DbSet<Playermatchhistory> Playermatchhistories { get; set; }

    public virtual DbSet<Playermission> Playermissions { get; set; }

    public virtual DbSet<Playerprofile> Playerprofiles { get; set; }

    public virtual DbSet<Playertechniquemastery> Playertechniquemasteries { get; set; }

    public virtual DbSet<Playertechniqueserror> Playertechniqueserrors { get; set; }

    public virtual DbSet<Refreshtoken> Refreshtokens { get; set; }

    public virtual DbSet<Reportplayerinmatch> Reportplayerinmatches { get; set; }

    public virtual DbSet<Role> Roles { get; set; }

    public virtual DbSet<Technique> Techniques { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<Verificationemail> Verificationemails { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=pickleverse;Username=sa;Password=12345;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");

        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.HasKey(e => e.Achievementid).HasName("achievements_pkey");

            entity.ToTable("achievements");

            entity.HasIndex(e => e.Achievementcode, "achievements_achievementcode_key").IsUnique();

            entity.Property(e => e.Achievementid).HasColumnName("achievementid");
            entity.Property(e => e.Achievementcode)
                .HasMaxLength(50)
                .HasColumnName("achievementcode");
            entity.Property(e => e.Achievementname)
                .HasMaxLength(254)
                .HasColumnName("achievementname");
            entity.Property(e => e.Description)
                .HasMaxLength(254)
                .HasColumnName("description");
            entity.Property(e => e.Reward)
                .HasDefaultValueSql("'{\"Exp\": 0, \"Currency\": 0}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("reward");
            entity.Property(e => e.Target).HasColumnName("target");
            entity.Property(e => e.Type)
                .HasMaxLength(25)
                .HasColumnName("type");
        });

        modelBuilder.Entity<Blockeduser>(entity =>
        {
            entity.HasKey(e => new { e.Userid, e.Blockeduserid }).HasName("blockedusers_pkey");

            entity.ToTable("blockedusers");

            entity.Property(e => e.Userid).HasColumnName("userid");
            entity.Property(e => e.Blockeduserid).HasColumnName("blockeduserid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("createdat");

            entity.HasOne(d => d.BlockeduserNavigation).WithMany(p => p.BlockeduserBlockeduserNavigations)
                .HasForeignKey(d => d.Blockeduserid)
                .HasConstraintName("fk_blockedusers_blocked");

            entity.HasOne(d => d.User).WithMany(p => p.BlockeduserUsers)
                .HasForeignKey(d => d.Userid)
                .HasConstraintName("fk_blockedusers_user");
        });

        modelBuilder.Entity<Cosmetic>(entity =>
        {
            entity.HasKey(e => e.Cosmeticid).HasName("cosmetics_pkey");

            entity.ToTable("cosmetics");

            entity.HasIndex(e => e.Itemcode, "cosmetics_itemcode_key").IsUnique();

            entity.Property(e => e.Cosmeticid).HasColumnName("cosmeticid");
            entity.Property(e => e.Cosmeticname)
                .HasMaxLength(254)
                .HasColumnName("cosmeticname");
            entity.Property(e => e.Currencyid).HasColumnName("currencyid");
            entity.Property(e => e.Description)
                .HasMaxLength(254)
                .HasColumnName("description");
            entity.Property(e => e.Itemcode)
                .HasMaxLength(50)
                .HasColumnName("itemcode");
            entity.Property(e => e.Price)
                .HasDefaultValue(99999)
                .HasColumnName("price");
            entity.Property(e => e.Type)
                .HasMaxLength(25)
                .HasColumnName("type");

            entity.HasOne(d => d.Currency).WithMany(p => p.Cosmetics)
                .HasForeignKey(d => d.Currencyid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_cosmetics_currencies");
        });

        modelBuilder.Entity<Currency>(entity =>
        {
            entity.HasKey(e => e.Currencyid).HasName("currencies_pkey");

            entity.ToTable("currencies");

            entity.HasIndex(e => e.Currencyname, "currencies_currencyname_key").IsUnique();

            entity.Property(e => e.Currencyid).HasColumnName("currencyid");
            entity.Property(e => e.Currencyname)
                .HasMaxLength(254)
                .HasColumnName("currencyname");
            entity.Property(e => e.Description)
                .HasMaxLength(254)
                .HasColumnName("description");
        });

        modelBuilder.Entity<Drill>(entity =>
        {
            entity.HasKey(e => e.Drillid).HasName("drills_pkey");

            entity.ToTable("drills");

            entity.Property(e => e.Drillid).HasColumnName("drillid");
            entity.Property(e => e.Description)
                .HasMaxLength(254)
                .HasColumnName("description");
            entity.Property(e => e.Drillname)
                .HasMaxLength(254)
                .HasColumnName("drillname");
            entity.Property(e => e.Techniqueid).HasColumnName("techniqueid");

            entity.HasOne(d => d.Technique).WithMany(p => p.Drills)
                .HasForeignKey(d => d.Techniqueid)
                .HasConstraintName("fk_drills_techniques");
        });

        modelBuilder.Entity<Friendrequest>(entity =>
        {
            entity.HasKey(e => e.Requestid).HasName("friendrequests_pkey");

            entity.ToTable("friendrequests");

            entity.HasIndex(e => new { e.Senderid, e.Receiverid }, "uq_sender_receiver").IsUnique();

            entity.Property(e => e.Requestid).HasColumnName("requestid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("createdat");
            entity.Property(e => e.Receiverid).HasColumnName("receiverid");
            entity.Property(e => e.Respondedat).HasColumnName("respondedat");
            entity.Property(e => e.Senderid).HasColumnName("senderid");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.Receiver).WithMany(p => p.FriendrequestReceivers)
                .HasForeignKey(d => d.Receiverid)
                .HasConstraintName("fk_friendrequests_receiver");

            entity.HasOne(d => d.Sender).WithMany(p => p.FriendrequestSenders)
                .HasForeignKey(d => d.Senderid)
                .HasConstraintName("fk_friendrequests_sender");
        });

        modelBuilder.Entity<Friendship>(entity =>
        {
            entity.HasKey(e => new { e.Userid1, e.Userid2 }).HasName("friendships_pkey");

            entity.ToTable("friendships");

            entity.Property(e => e.Userid1).HasColumnName("userid1");
            entity.Property(e => e.Userid2).HasColumnName("userid2");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("createdat");

            entity.HasOne(d => d.Userid1Navigation).WithMany(p => p.FriendshipUserid1Navigations)
                .HasForeignKey(d => d.Userid1)
                .HasConstraintName("fk_friendships_user1");

            entity.HasOne(d => d.Userid2Navigation).WithMany(p => p.FriendshipUserid2Navigations)
                .HasForeignKey(d => d.Userid2)
                .HasConstraintName("fk_friendships_user2");
        });

        modelBuilder.Entity<Lesson>(entity =>
        {
            entity.HasKey(e => e.Lessonid).HasName("lessons_pkey");

            entity.ToTable("lessons");

            entity.Property(e => e.Lessonid).HasColumnName("lessonid");
            entity.Property(e => e.Description)
                .HasMaxLength(254)
                .HasColumnName("description");
            entity.Property(e => e.Lessonname)
                .HasMaxLength(254)
                .HasColumnName("lessonname");
            entity.Property(e => e.Reward)
                .HasDefaultValueSql("'{\"Exp\": 0, \"Currency\": 0}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("reward");
            entity.Property(e => e.Target).HasColumnName("target");
            entity.Property(e => e.Type)
                .HasMaxLength(25)
                .HasColumnName("type");
        });

        modelBuilder.Entity<Lessondrill>(entity =>
        {
            entity.HasKey(e => new { e.Lessonid, e.Drillid }).HasName("lessondrills_pkey");

            entity.ToTable("lessondrills");

            entity.Property(e => e.Lessonid).HasColumnName("lessonid");
            entity.Property(e => e.Drillid).HasColumnName("drillid");
            entity.Property(e => e.Target).HasColumnName("target");

            entity.HasOne(d => d.Drill).WithMany(p => p.Lessondrills)
                .HasForeignKey(d => d.Drillid)
                .HasConstraintName("fk_lessondrills_drills");

            entity.HasOne(d => d.Lesson).WithMany(p => p.Lessondrills)
                .HasForeignKey(d => d.Lessonid)
                .HasConstraintName("fk_lessondrills_lessons");
        });

        modelBuilder.Entity<Matchhistory>(entity =>
        {
            entity.HasKey(e => e.Matchid).HasName("matchhistory_pkey");

            entity.ToTable("matchhistory");

            entity.Property(e => e.Matchid)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("matchid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("createdat");
            entity.Property(e => e.Matchtype)
                .HasMaxLength(25)
                .HasDefaultValueSql("'1Vs1'::character varying")
                .HasColumnName("matchtype");
            entity.Property(e => e.Teamascore).HasColumnName("teamascore");
            entity.Property(e => e.Teambscore).HasColumnName("teambscore");
        });

        modelBuilder.Entity<Matchhistoryreplay>(entity =>
        {
            entity.HasKey(e => e.Matchid).HasName("matchhistoryreplay_pkey");

            entity.ToTable("matchhistoryreplay");

            entity.Property(e => e.Matchid)
                .ValueGeneratedNever()
                .HasColumnName("matchid");
            entity.Property(e => e.Replayurl)
                .HasMaxLength(1000)
                .HasColumnName("replayurl");

            entity.HasOne(d => d.Match).WithOne(p => p.Matchhistoryreplay)
                .HasForeignKey<Matchhistoryreplay>(d => d.Matchid)
                .HasConstraintName("fk_matchhistoryreplay_matchhistory");
        });

        modelBuilder.Entity<Mission>(entity =>
        {
            entity.HasKey(e => e.Missionid).HasName("missions_pkey");

            entity.ToTable("missions");

            entity.Property(e => e.Missionid).HasColumnName("missionid");
            entity.Property(e => e.Description)
                .HasMaxLength(254)
                .HasColumnName("description");
            entity.Property(e => e.Missionname)
                .HasMaxLength(254)
                .HasColumnName("missionname");
            entity.Property(e => e.Reward)
                .HasDefaultValueSql("'{\"Exp\": 0, \"Currency\": 0}'::jsonb")
                .HasColumnType("jsonb")
                .HasColumnName("reward");
            entity.Property(e => e.Target).HasColumnName("target");
            entity.Property(e => e.Type)
                .HasMaxLength(25)
                .HasColumnName("type");
        });

        modelBuilder.Entity<Playerachievement>(entity =>
        {
            entity.HasKey(e => new { e.Playerid, e.Achievementid }).HasName("playerachievements_pkey");

            entity.ToTable("playerachievements");

            entity.Property(e => e.Playerid).HasColumnName("playerid");
            entity.Property(e => e.Achievementid).HasColumnName("achievementid");
            entity.Property(e => e.Claimedat).HasColumnName("claimedat");
            entity.Property(e => e.Isclaimed).HasColumnName("isclaimed");
            entity.Property(e => e.Progress).HasColumnName("progress");

            entity.HasOne(d => d.Achievement).WithMany(p => p.Playerachievements)
                .HasForeignKey(d => d.Achievementid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_playerachievements_achievements");

            entity.HasOne(d => d.Player).WithMany(p => p.Playerachievements)
                .HasForeignKey(d => d.Playerid)
                .HasConstraintName("fk_playerachievements_playerprofiles");
        });

        modelBuilder.Entity<Playercosmetic>(entity =>
        {
            entity.HasKey(e => new { e.Playerid, e.Cosmeticid }).HasName("playercosmetics_pkey");

            entity.ToTable("playercosmetics");

            entity.Property(e => e.Playerid).HasColumnName("playerid");
            entity.Property(e => e.Cosmeticid).HasColumnName("cosmeticid");
            entity.Property(e => e.Equipped).HasColumnName("equipped");

            entity.HasOne(d => d.Cosmetic).WithMany(p => p.Playercosmetics)
                .HasForeignKey(d => d.Cosmeticid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_playercosmetics_cosmetics");

            entity.HasOne(d => d.Player).WithMany(p => p.Playercosmetics)
                .HasForeignKey(d => d.Playerid)
                .HasConstraintName("fk_playercosmetics_playerprofiles");
        });

        modelBuilder.Entity<Playercurrency>(entity =>
        {
            entity.HasKey(e => new { e.Playerid, e.Currencyid }).HasName("playercurrencies_pkey");

            entity.ToTable("playercurrencies");

            entity.Property(e => e.Playerid).HasColumnName("playerid");
            entity.Property(e => e.Currencyid).HasColumnName("currencyid");
            entity.Property(e => e.Amount).HasColumnName("amount");

            entity.HasOne(d => d.Currency).WithMany(p => p.Playercurrencies)
                .HasForeignKey(d => d.Currencyid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_playercurrencies_currencies");

            entity.HasOne(d => d.Player).WithMany(p => p.Playercurrencies)
                .HasForeignKey(d => d.Playerid)
                .HasConstraintName("fk_playercurrencies_playerprofiles");
        });

        modelBuilder.Entity<Playerlessondrill>(entity =>
        {
            entity.HasKey(e => new { e.Playerid, e.Lessonid, e.Drillid }).HasName("playerlessondrills_pkey");

            entity.ToTable("playerlessondrills");

            entity.Property(e => e.Playerid).HasColumnName("playerid");
            entity.Property(e => e.Lessonid).HasColumnName("lessonid");
            entity.Property(e => e.Drillid).HasColumnName("drillid");
            entity.Property(e => e.Isclaimedreward).HasColumnName("isclaimedreward");
            entity.Property(e => e.Score).HasColumnName("score");

            entity.HasOne(d => d.Drill).WithMany(p => p.Playerlessondrills)
                .HasForeignKey(d => d.Drillid)
                .HasConstraintName("fk_playerlessondrills_drills");

            entity.HasOne(d => d.Lesson).WithMany(p => p.Playerlessondrills)
                .HasForeignKey(d => d.Lessonid)
                .HasConstraintName("fk_playerlessondrills_lessons");

            entity.HasOne(d => d.Player).WithMany(p => p.Playerlessondrills)
                .HasForeignKey(d => d.Playerid)
                .HasConstraintName("fk_playerlessondrills_playerprofiles");
        });

        modelBuilder.Entity<Playermatchhistory>(entity =>
        {
            entity.HasKey(e => new { e.Playerid, e.Matchid }).HasName("playermatchhistory_pkey");

            entity.ToTable("playermatchhistory");

            entity.Property(e => e.Playerid).HasColumnName("playerid");
            entity.Property(e => e.Matchid).HasColumnName("matchid");
            entity.Property(e => e.Isteama)
                .HasDefaultValue(true)
                .HasColumnName("isteama");
            entity.Property(e => e.Receivedelo).HasColumnName("receivedelo");
            entity.Property(e => e.Status)
                .HasMaxLength(25)
                .HasDefaultValueSql("'WIN'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.Match).WithMany(p => p.Playermatchhistories)
                .HasForeignKey(d => d.Matchid)
                .HasConstraintName("fk_playermatchhistory_matchhistory");

            entity.HasOne(d => d.Player).WithMany(p => p.Playermatchhistories)
                .HasForeignKey(d => d.Playerid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_playermatchhistory_playerprofiles");
        });

        modelBuilder.Entity<Playermission>(entity =>
        {
            entity.HasKey(e => new { e.Playerid, e.Missionid }).HasName("playermissions_pkey");

            entity.ToTable("playermissions");

            entity.Property(e => e.Playerid).HasColumnName("playerid");
            entity.Property(e => e.Missionid).HasColumnName("missionid");
            entity.Property(e => e.Claimedat).HasColumnName("claimedat");
            entity.Property(e => e.Isclaimed).HasColumnName("isclaimed");
            entity.Property(e => e.Progress).HasColumnName("progress");

            entity.HasOne(d => d.Mission).WithMany(p => p.Playermissions)
                .HasForeignKey(d => d.Missionid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_playermissions_missions");

            entity.HasOne(d => d.Player).WithMany(p => p.Playermissions)
                .HasForeignKey(d => d.Playerid)
                .HasConstraintName("fk_playermissions_playerprofiles");
        });

        modelBuilder.Entity<Playerprofile>(entity =>
        {
            entity.HasKey(e => e.Userid).HasName("playerprofiles_pkey");

            entity.ToTable("playerprofiles");

            entity.HasIndex(e => e.Playername, "playerprofiles_playername_key").IsUnique();

            entity.Property(e => e.Userid)
                .ValueGeneratedNever()
                .HasColumnName("userid");
            entity.Property(e => e.Avatar)
                .HasMaxLength(80)
                .HasDefaultValueSql("'initial'::character varying")
                .HasColumnName("avatar");
            entity.Property(e => e.Daystreak).HasColumnName("daystreak");
            entity.Property(e => e.Elo)
                .HasDefaultValue(100)
                .HasColumnName("elo");
            entity.Property(e => e.Exp).HasColumnName("exp");
            entity.Property(e => e.Ismale)
                .HasDefaultValue(true)
                .HasColumnName("ismale");
            entity.Property(e => e.Level)
                .HasDefaultValue(1)
                .HasColumnName("level");
            entity.Property(e => e.Losecount).HasColumnName("losecount");
            entity.Property(e => e.Playername)
                .HasMaxLength(18)
                .HasColumnName("playername");
            entity.Property(e => e.Timeplayedseconds).HasColumnName("timeplayedseconds");
            entity.Property(e => e.Wincount).HasColumnName("wincount");
            entity.Property(e => e.Winstreak).HasColumnName("winstreak");

            entity.HasOne(d => d.User).WithOne(p => p.Playerprofile)
                .HasForeignKey<Playerprofile>(d => d.Userid)
                .HasConstraintName("fk_playerprofiles_users");
        });

        modelBuilder.Entity<Playertechniquemastery>(entity =>
        {
            entity.HasKey(e => new { e.Playerid, e.Techniqueid }).HasName("playertechniquemastery_pkey");

            entity.ToTable("playertechniquemastery");

            entity.Property(e => e.Playerid).HasColumnName("playerid");
            entity.Property(e => e.Techniqueid).HasColumnName("techniqueid");
            entity.Property(e => e.Lastupdatedat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("lastupdatedat");
            entity.Property(e => e.Masteryscore)
                .HasComputedColumnSql("\nCASE\n    WHEN (totalattempts = 0) THEN 0\n    ELSE (((((totalattempts - totalerrors) * 100) / totalattempts))::smallint)::integer\nEND", true)
                .HasColumnName("masteryscore");
            entity.Property(e => e.Totalattempts).HasColumnName("totalattempts");
            entity.Property(e => e.Totalerrors).HasColumnName("totalerrors");

            entity.HasOne(d => d.Player).WithMany(p => p.Playertechniquemasteries)
                .HasForeignKey(d => d.Playerid)
                .HasConstraintName("fk_playertechniquemastery_playerprofiles");

            entity.HasOne(d => d.Technique).WithMany(p => p.Playertechniquemasteries)
                .HasForeignKey(d => d.Techniqueid)
                .HasConstraintName("fk_playertechniquemastery_techniques");
        });

        modelBuilder.Entity<Playertechniqueserror>(entity =>
        {
            entity.HasKey(e => e.Errorlogid).HasName("playertechniqueserrors_pkey");

            entity.ToTable("playertechniqueserrors");

            entity.Property(e => e.Errorlogid).HasColumnName("errorlogid");
            entity.Property(e => e.Coachingtip).HasColumnName("coachingtip");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("createdat");
            entity.Property(e => e.Errortype)
                .HasMaxLength(50)
                .HasColumnName("errortype");
            entity.Property(e => e.Lessonid).HasColumnName("lessonid");
            entity.Property(e => e.Matchid).HasColumnName("matchid");
            entity.Property(e => e.Playerid).HasColumnName("playerid");
            entity.Property(e => e.Techniqueid).HasColumnName("techniqueid");

            entity.HasOne(d => d.Lesson).WithMany(p => p.Playertechniqueserrors)
                .HasForeignKey(d => d.Lessonid)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_playertechniqueserrors_lessons");

            entity.HasOne(d => d.Match).WithMany(p => p.Playertechniqueserrors)
                .HasForeignKey(d => d.Matchid)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_playertechniqueserrors_matchhistory");

            entity.HasOne(d => d.Player).WithMany(p => p.Playertechniqueserrors)
                .HasForeignKey(d => d.Playerid)
                .HasConstraintName("fk_playertechniqueserrors_playerprofiles");

            entity.HasOne(d => d.Technique).WithMany(p => p.Playertechniqueserrors)
                .HasForeignKey(d => d.Techniqueid)
                .HasConstraintName("fk_playertechniqueserrors_techniques");
        });

        modelBuilder.Entity<Refreshtoken>(entity =>
        {
            entity.HasKey(e => e.Tokenid).HasName("refreshtokens_pkey");

            entity.ToTable("refreshtokens");

            entity.HasIndex(e => e.Tokenhash, "refreshtokens_tokenhash_key").IsUnique();

            entity.Property(e => e.Tokenid).HasColumnName("tokenid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("createdat");
            entity.Property(e => e.Expiresat).HasColumnName("expiresat");
            entity.Property(e => e.Isrevoked).HasColumnName("isrevoked");
            entity.Property(e => e.Tokenhash)
                .HasMaxLength(254)
                .HasColumnName("tokenhash");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne(d => d.User).WithMany(p => p.Refreshtokens)
                .HasForeignKey(d => d.Userid)
                .HasConstraintName("fk_refreshtokens_users");
        });

        modelBuilder.Entity<Reportplayerinmatch>(entity =>
        {
            entity.HasKey(e => new { e.Reporterid, e.Reportedplayerid, e.Matchid }).HasName("reportplayerinmatch_pkey");

            entity.ToTable("reportplayerinmatch");

            entity.Property(e => e.Reporterid).HasColumnName("reporterid");
            entity.Property(e => e.Reportedplayerid).HasColumnName("reportedplayerid");
            entity.Property(e => e.Matchid).HasColumnName("matchid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("createdat");
            entity.Property(e => e.Reason)
                .HasMaxLength(254)
                .HasColumnName("reason");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'PENDING'::character varying")
                .HasColumnName("status");
            entity.Property(e => e.Updatedat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("updatedat");

            entity.HasOne(d => d.Match).WithMany(p => p.Reportplayerinmatches)
                .HasForeignKey(d => d.Matchid)
                .HasConstraintName("fk_report_match");

            entity.HasOne(d => d.Reportedplayer).WithMany(p => p.ReportplayerinmatchReportedplayers)
                .HasForeignKey(d => d.Reportedplayerid)
                .HasConstraintName("fk_report_reportedplayer");

            entity.HasOne(d => d.Reporter).WithMany(p => p.ReportplayerinmatchReporters)
                .HasForeignKey(d => d.Reporterid)
                .HasConstraintName("fk_report_reporter");
        });

        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.Roleid).HasName("roles_pkey");

            entity.ToTable("roles");

            entity.HasIndex(e => e.Rolename, "roles_rolename_key").IsUnique();

            entity.Property(e => e.Roleid).HasColumnName("roleid");
            entity.Property(e => e.Description)
                .HasMaxLength(254)
                .HasColumnName("description");
            entity.Property(e => e.Rolename)
                .HasMaxLength(32)
                .HasColumnName("rolename");
        });

        modelBuilder.Entity<Technique>(entity =>
        {
            entity.HasKey(e => e.Techniqueid).HasName("techniques_pkey");

            entity.ToTable("techniques");

            entity.HasIndex(e => e.Techniquename, "techniques_techniquename_key").IsUnique();

            entity.Property(e => e.Techniqueid).HasColumnName("techniqueid");
            entity.Property(e => e.Description)
                .HasMaxLength(254)
                .HasColumnName("description");
            entity.Property(e => e.Techniquename)
                .HasMaxLength(254)
                .HasColumnName("techniquename");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Userid).HasName("users_pkey");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "users_email_key").IsUnique();

            entity.Property(e => e.Userid)
                .HasDefaultValueSql("gen_random_uuid()")
                .HasColumnName("userid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("createdat");
            entity.Property(e => e.Email)
                .HasMaxLength(254)
                .HasColumnName("email");
            entity.Property(e => e.Passwordhash)
                .HasMaxLength(254)
                .HasColumnName("passwordhash");
            entity.Property(e => e.Roleid).HasColumnName("roleid");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValueSql("'ACTIVE'::character varying")
                .HasColumnName("status");

            entity.HasOne(d => d.Role).WithMany(p => p.Users)
                .HasForeignKey(d => d.Roleid)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_users_roles");
        });

        modelBuilder.Entity<Verificationemail>(entity =>
        {
            entity.HasKey(e => e.Verificationemailid).HasName("verificationemail_pkey");

            entity.ToTable("verificationemail");

            entity.Property(e => e.Verificationemailid).HasColumnName("verificationemailid");
            entity.Property(e => e.Createdat)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnName("createdat");
            entity.Property(e => e.Email)
                .HasMaxLength(254)
                .HasColumnName("email");
            entity.Property(e => e.Expiresat).HasColumnName("expiresat");
            entity.Property(e => e.Isverified).HasColumnName("isverified");
            entity.Property(e => e.Token)
                .HasMaxLength(10)
                .HasColumnName("token");
            entity.Property(e => e.Userid).HasColumnName("userid");

            entity.HasOne(d => d.User).WithMany(p => p.Verificationemails)
                .HasForeignKey(d => d.Userid)
                .HasConstraintName("fk_verificationemail_users");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
