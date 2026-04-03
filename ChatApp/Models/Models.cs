using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp.Models
{
    public class User
    {
        public int Id { get; set; }

        [Required, MaxLength(50)]
        public string Username { get; set; } = "";

        [MaxLength(150)]
        public string? Email { get; set; }

        [MaxLength(20)]
        public string? PhoneNumber { get; set; }

        [Required]
        public string PasswordHash { get; set; } = "";

        [MaxLength(100)]
        public string DisplayName { get; set; } = "";

        public string? AvatarUrl { get; set; }
        public string About { get; set; } = "Hey there! I am using ChatApp.";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
        public bool IsOnline { get; set; } = false;

        // E2E encryption — public key stored on server, private key stays in browser
        public string? PublicKey { get; set; }

        // Two-factor authentication (TOTP)
        public bool IsTwoFactorEnabled { get; set; } = false;
        public string? TwoFactorSecret { get; set; }

        public ICollection<Message> SentMessages { get; set; } = new List<Message>();
        public ICollection<Message> ReceivedMessages { get; set; } = new List<Message>();
        public ICollection<GroupMember> GroupMemberships { get; set; } = new List<GroupMember>();
        public ICollection<ChannelMember> ChannelMemberships { get; set; } = new List<ChannelMember>();
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
        public ICollection<StarredMessage> StarredMessages { get; set; } = new List<StarredMessage>();
    }

    public class RefreshToken
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string Token { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public bool IsRevoked { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }

    public class Message
    {
        public int Id { get; set; }
        public int SenderId { get; set; }
        public int? ReceiverId { get; set; }
        public int? GroupId { get; set; }
        public int? ChannelId { get; set; }
        public int? ThreadParentId { get; set; }

        [Required]
        public string Content { get; set; } = "";

        // Encrypted content — null if E2E not enabled
        public string? EncryptedContent { get; set; }
        public bool IsEncrypted { get; set; } = false;

        public MessageType Type { get; set; } = MessageType.Text;
        public string? FileUrl { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
        public string? FileMimeType { get; set; }
        public string? ThumbnailUrl { get; set; }

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
        public bool IsDeleted { get; set; } = false;
        public bool IsEdited { get; set; } = false;

        // Disappearing messages
        public DateTime? ExpiresAt { get; set; }

        // Silent/channel announcement message marker
        public bool IsSilent { get; set; } = false;

        // Scheduled messages
        public DateTime? ScheduledAt { get; set; }
        public bool IsSent { get; set; } = true;

        [ForeignKey("SenderId")]
        public User Sender { get; set; } = null!;

        [ForeignKey("ReceiverId")]
        public User? Receiver { get; set; }

        [ForeignKey("GroupId")]
        public Group? Group { get; set; }

        [ForeignKey("ChannelId")]
        public Channel? Channel { get; set; }

        [ForeignKey("ThreadParentId")]
        public Message? ThreadParent { get; set; }

        public ICollection<Message> ThreadReplies { get; set; } = new List<Message>();
        public ICollection<MessageRead> ReadBy { get; set; } = new List<MessageRead>();
        public ICollection<MessageReaction> Reactions { get; set; } = new List<MessageReaction>();
        public ICollection<MessageEdit> EditHistory { get; set; } = new List<MessageEdit>();
        public ICollection<PinnedMessage> Pins { get; set; } = new List<PinnedMessage>();
        public ICollection<StarredMessage> Stars { get; set; } = new List<StarredMessage>();
        public Poll? Poll { get; set; }
    }

    public enum MessageType { Text, Image, File, Audio, Video, Poll, Document }

    public class MessageEdit
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public string PreviousContent { get; set; } = "";
        public DateTime EditedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("MessageId")]
        public Message Message { get; set; } = null!;
    }

    public class MessageReaction
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int UserId { get; set; }
        public string Emoji { get; set; } = "";
        public DateTime ReactedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("MessageId")]
        public Message Message { get; set; } = null!;
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }

    public enum PinCategory
    {
        Important,
        ActionRequired,
        Reference,
        General
    }

    public class PinnedMessage
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int PinnedById { get; set; }
        public int? GroupId { get; set; }
        public int? ChannelId { get; set; }
        public int? DirectUserId { get; set; }
        public PinCategory Category { get; set; } = PinCategory.Important;
        public DateTime PinnedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("MessageId")]
        public Message Message { get; set; } = null!;
        [ForeignKey("PinnedById")]
        public User PinnedBy { get; set; } = null!;
    }

    public class StarredMessage
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int UserId { get; set; }
        public DateTime StarredAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("MessageId")]
        public Message Message { get; set; } = null!;
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }

    public class Poll
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public string Question { get; set; } = "";
        public bool IsMultipleChoice { get; set; } = false;
        public DateTime? ExpiresAt { get; set; }

        [ForeignKey("MessageId")]
        public Message Message { get; set; } = null!;
        public ICollection<PollOption> Options { get; set; } = new List<PollOption>();
    }

    public class PollOption
    {
        public int Id { get; set; }
        public int PollId { get; set; }
        public string Text { get; set; } = "";

        [ForeignKey("PollId")]
        public Poll Poll { get; set; } = null!;
        public ICollection<PollVote> Votes { get; set; } = new List<PollVote>();
    }

    public class PollVote
    {
        public int Id { get; set; }
        public int PollOptionId { get; set; }
        public int UserId { get; set; }
        public DateTime VotedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("PollOptionId")]
        public PollOption PollOption { get; set; } = null!;
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }

    public class Group
    {
        public int Id { get; set; }
        [Required, MaxLength(100)]
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }
        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CreatedById")]
        public User CreatedBy { get; set; } = null!;
        public ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
        public ICollection<PinnedMessage> PinnedMessages { get; set; } = new List<PinnedMessage>();
    }

    public class GroupMember
    {
        public int Id { get; set; }
        public int GroupId { get; set; }
        public int UserId { get; set; }
        public GroupRole Role { get; set; } = GroupRole.Member;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("GroupId")]
        public Group Group { get; set; } = null!;
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
          public bool IsAdmin { get; set; } = false; 
    }

    public enum GroupRole { Member, Admin, Owner }

    // Channels — one-to-many broadcast (like Telegram channels)
    public class Channel
    {
        public int Id { get; set; }
        [Required, MaxLength(100)]
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? AvatarUrl { get; set; }
        public bool IsPublic { get; set; } = true;
        public int CreatedById { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("CreatedById")]
        public User CreatedBy { get; set; } = null!;
        public ICollection<ChannelMember> Members { get; set; } = new List<ChannelMember>();
        public ICollection<Message> Messages { get; set; } = new List<Message>();
    }

    public class ChannelMember
    {
        public int Id { get; set; }
        public int ChannelId { get; set; }
        public int UserId { get; set; }
        public bool IsAdmin { get; set; } = false;
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ChannelId")]
        public Channel Channel { get; set; } = null!;
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }

    public class MessageRead
    {
        public int Id { get; set; }
        public int MessageId { get; set; }
        public int UserId { get; set; }
        public DateTime ReadAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("MessageId")]
        public Message Message { get; set; } = null!;
        [ForeignKey("UserId")]
        public User User { get; set; } = null!;
    }

    // Bot / Open API
    public class BotApp
    {
        public int Id { get; set; }
        public int OwnerId { get; set; }
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string ApiKey { get; set; } = "";
        public string? WebhookUrl { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("OwnerId")]
        public User Owner { get; set; } = null!;
    }
}