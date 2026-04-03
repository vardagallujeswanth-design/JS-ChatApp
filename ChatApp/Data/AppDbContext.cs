using Microsoft.EntityFrameworkCore;
using ChatApp.Models;

namespace ChatApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users => Set<User>();
        public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<MessageEdit> MessageEdits => Set<MessageEdit>();
        public DbSet<MessageReaction> MessageReactions => Set<MessageReaction>();
        public DbSet<MessageRead> MessageReads => Set<MessageRead>();
        public DbSet<PinnedMessage> PinnedMessages => Set<PinnedMessage>();
        public DbSet<StarredMessage> StarredMessages => Set<StarredMessage>();
        public DbSet<Poll> Polls => Set<Poll>();
        public DbSet<PollOption> PollOptions => Set<PollOption>();
        public DbSet<PollVote> PollVotes => Set<PollVote>();
        public DbSet<Group> Groups => Set<Group>();
        public DbSet<GroupMember> GroupMembers => Set<GroupMember>();
        public DbSet<Channel> Channels => Set<Channel>();
        public DbSet<ChannelMember> ChannelMembers => Set<ChannelMember>();
        public DbSet<BotApp> BotApps => Set<BotApp>();

        protected override void OnModelCreating(ModelBuilder m)
        {
            // Unique constraints
            // Username/display name may be duplicated; phone number must be unique.
            m.Entity<User>().HasIndex(u => u.Username);
            m.Entity<User>().HasIndex(u => u.PhoneNumber).IsUnique().HasFilter("[PhoneNumber] IS NOT NULL");
            m.Entity<GroupMember>().HasIndex(gm => new { gm.GroupId, gm.UserId }).IsUnique();
            m.Entity<ChannelMember>().HasIndex(cm => new { cm.ChannelId, cm.UserId }).IsUnique();
            m.Entity<MessageRead>().HasIndex(mr => new { mr.MessageId, mr.UserId }).IsUnique();
            m.Entity<MessageReaction>().HasIndex(r => new { r.MessageId, r.UserId, r.Emoji }).IsUnique();
            m.Entity<StarredMessage>().HasIndex(s => new { s.MessageId, s.UserId }).IsUnique();
            m.Entity<PollVote>().HasIndex(v => new { v.PollOptionId, v.UserId }).IsUnique();
            m.Entity<BotApp>().HasIndex(b => b.ApiKey).IsUnique();

            // Message relationships — restrict delete to avoid cycles
            m.Entity<Message>()
                .HasOne(msg => msg.Sender)
                .WithMany(u => u.SentMessages)
                .HasForeignKey(msg => msg.SenderId)
                .OnDelete(DeleteBehavior.Restrict);

            m.Entity<Message>()
                .HasOne(msg => msg.Receiver)
                .WithMany(u => u.ReceivedMessages)
                .HasForeignKey(msg => msg.ReceiverId)
                .OnDelete(DeleteBehavior.Restrict);

            m.Entity<Message>()
                .HasOne(msg => msg.ThreadParent)
                .WithMany(msg => msg.ThreadReplies)
                .HasForeignKey(msg => msg.ThreadParentId)
                .OnDelete(DeleteBehavior.Restrict);

            m.Entity<MessageEdit>()
                .HasOne(e => e.Message)
                .WithMany(msg => msg.EditHistory)
                .HasForeignKey(e => e.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<MessageReaction>()
                .HasOne(r => r.Message)
                .WithMany(msg => msg.Reactions)
                .HasForeignKey(r => r.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<MessageRead>()
                .HasOne(r => r.Message)
                .WithMany(msg => msg.ReadBy)
                .HasForeignKey(r => r.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<Poll>()
                .HasOne(p => p.Message)
                .WithOne(m => m.Poll)
                .HasForeignKey<Poll>(p => p.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<PollOption>()
                .HasOne(o => o.Poll)
                .WithMany(p => p.Options)
                .HasForeignKey(o => o.PollId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<PollVote>()
                .HasOne(v => v.PollOption)
                .WithMany(o => o.Votes)
                .HasForeignKey(v => v.PollOptionId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<PinnedMessage>()
                .HasOne(p => p.Message)
                .WithMany(msg => msg.Pins)
                .HasForeignKey(p => p.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<PinnedMessage>()
                .HasOne(p => p.PinnedBy)
                .WithMany()
                .HasForeignKey(p => p.PinnedById)
                .OnDelete(DeleteBehavior.Restrict);

            m.Entity<StarredMessage>()
                .HasOne(s => s.Message)
                .WithMany(msg => msg.Stars)
                .HasForeignKey(s => s.MessageId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<StarredMessage>()
                .HasOne(s => s.User)
                .WithMany(u => u.StarredMessages)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // Refresh tokens
            m.Entity<RefreshToken>()
                .HasOne(rt => rt.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(rt => rt.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // Group and Channel relationships
            m.Entity<Group>()
                .HasOne(g => g.CreatedBy)
                .WithMany()
                .HasForeignKey(g => g.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            m.Entity<GroupMember>()
                .HasOne(gm => gm.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(gm => gm.GroupId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<GroupMember>()
                .HasOne(gm => gm.User)
                .WithMany(u => u.GroupMemberships)
                .HasForeignKey(gm => gm.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            m.Entity<Channel>()
                .HasOne(c => c.CreatedBy)
                .WithMany()
                .HasForeignKey(c => c.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            m.Entity<ChannelMember>()
                .HasOne(cm => cm.Channel)
                .WithMany(c => c.Members)
                .HasForeignKey(cm => cm.ChannelId)
                .OnDelete(DeleteBehavior.Cascade);

            m.Entity<ChannelMember>()
                .HasOne(cm => cm.User)
                .WithMany(u => u.ChannelMemberships)
                .HasForeignKey(cm => cm.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}