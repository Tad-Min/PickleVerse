using Entities.Models;
using Microsoft.EntityFrameworkCore;

namespace Repositories.Context;

public partial class PickleVerseContext
{
    static void ConfigureSocial(ModelBuilder modelBuilder)
    {
        var media = modelBuilder.Entity<SocialMedia>().ToTable("socialmedia");
        media.HasKey(x => x.MediaId);
        media.HasOne<Playerprofile>().WithMany().HasForeignKey(x => x.OwnerId).OnDelete(DeleteBehavior.Cascade);
        media.HasIndex(x => x.CreatedAt);

        var post = modelBuilder.Entity<SocialPost>().ToTable("socialposts");
        post.HasKey(x => x.PostId);
        post.Property(x => x.PostId).UseIdentityAlwaysColumn();
        post.Property(x => x.Audience).HasMaxLength(20);
        post.Property(x => x.Body).HasMaxLength(2000);
        post.HasIndex(x => new { x.AuthorId, x.PostId }).IsDescending(false, true);
        post.HasOne<Playerprofile>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);
        post.HasOne<SocialMedia>().WithOne().HasForeignKey<SocialPost>(x => x.MediaId).OnDelete(DeleteBehavior.SetNull);
        post.HasOne<Gamematch>().WithMany().HasForeignKey(x => x.ReplayMatchId).OnDelete(DeleteBehavior.Cascade);

        var exclusions = modelBuilder.Entity<SocialPostExclusion>().ToTable("socialpostexclusions");
        exclusions.HasKey(x => new { x.PostId, x.PlayerId });
        exclusions.HasOne<SocialPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        exclusions.HasOne<Playerprofile>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        var likes = modelBuilder.Entity<SocialPostLike>().ToTable("socialpostlikes");
        likes.HasKey(x => new { x.PostId, x.PlayerId });
        likes.HasOne<SocialPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        likes.HasOne<Playerprofile>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        var comments = modelBuilder.Entity<SocialPostComment>().ToTable("socialpostcomments");
        comments.HasKey(x => x.CommentId);
        comments.Property(x => x.CommentId).UseIdentityAlwaysColumn();
        comments.Property(x => x.Body).HasMaxLength(1000);
        comments.HasIndex(x => new { x.PostId, x.CommentId }).IsDescending(false, true);
        comments.HasOne<SocialPost>().WithMany().HasForeignKey(x => x.PostId).OnDelete(DeleteBehavior.Cascade);
        comments.HasOne<Playerprofile>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Cascade);

        var conversations = modelBuilder.Entity<SocialConversation>().ToTable("socialconversations");
        conversations.HasKey(x => x.ConversationId);
        conversations.HasIndex(x => new { x.FirstPlayerId, x.SecondPlayerId }).IsUnique();
        conversations.HasOne<Playerprofile>().WithMany().HasForeignKey(x => x.FirstPlayerId).OnDelete(DeleteBehavior.Cascade);
        conversations.HasOne<Playerprofile>().WithMany().HasForeignKey(x => x.SecondPlayerId).OnDelete(DeleteBehavior.Cascade);
        var members = modelBuilder.Entity<SocialConversationMember>().ToTable("socialconversationmembers");
        members.HasKey(x => new { x.ConversationId, x.PlayerId });
        members.HasIndex(x => x.PlayerId);
        members.HasOne<SocialConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        members.HasOne<Playerprofile>().WithMany().HasForeignKey(x => x.PlayerId).OnDelete(DeleteBehavior.Cascade);
        var messages = modelBuilder.Entity<SocialMessage>().ToTable("socialmessages");
        messages.HasKey(x => x.MessageId);
        messages.Property(x => x.MessageId).UseIdentityAlwaysColumn();
        messages.Property(x => x.Body).HasMaxLength(1000);
        messages.HasIndex(x => new { x.ConversationId, x.MessageId }).IsDescending(false, true);
        messages.HasIndex(x => new { x.SenderId, x.ClientMessageId }).IsUnique();
        messages.HasOne<SocialConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
        messages.HasOne<Playerprofile>().WithMany().HasForeignKey(x => x.SenderId).OnDelete(DeleteBehavior.Cascade);
        messages.HasOne<SocialConversationMember>().WithMany().HasForeignKey(x => new { x.ConversationId, x.SenderId }).OnDelete(DeleteBehavior.Cascade);

        foreach (var type in new[] { typeof(SocialMedia), typeof(SocialPost), typeof(SocialPostExclusion),
            typeof(SocialPostLike), typeof(SocialPostComment), typeof(SocialConversation), typeof(SocialConversationMember), typeof(SocialMessage) })
            foreach (var property in modelBuilder.Entity(type).Metadata.GetProperties())
                property.SetColumnName(property.Name.ToLowerInvariant());
    }
}
