namespace ChatApp.DTOs
{
    // ── Auth ──────────────────────────────────────────────────────────────────
    public record RegisterRequest(
        string Username,
        string PhoneNumber,
        string Password,
        string? DisplayName,
        string? PublicKey  // RSA public key for E2E encryption
    );

    public record LoginRequest(string PhoneNumber, string Password);

    public record AuthResponse(string AccessToken, string RefreshToken, UserDto User);

    public record RefreshRequest(string RefreshToken);

    public record TwoFactorSetupResponse(string Secret, string ProvisioningUri);
    public record TwoFactorVerifyRequest(string Code);
    public record LoginTwoFactorRequest(string PhoneNumber, string Password, string Code);

    // ── Users ─────────────────────────────────────────────────────────────────
    public record UserDto(
        int Id,
        string Username,
        string PhoneNumber,
        string DisplayName,
        string? AvatarUrl,
        string? About,
        bool IsOnline,
        DateTime LastSeen,
        string? PublicKey,
        bool IsTwoFactorEnabled
    );

    public record UpdateProfileRequest(string? DisplayName, string? About);

    // ── Messages ──────────────────────────────────────────────────────────────
    public record MessageDto(
        int Id,
        int SenderId,
        string SenderName,
        string? SenderAvatar,
        int? ReceiverId,
        int? GroupId,
        int? ChannelId,
        int? ThreadParentId,
        int ThreadReplyCount,
        string Content,
        string? EncryptedContent,
        bool IsEncrypted,
        bool IsSilent,
        string Type,
        string? FileUrl,
        string? FileName,
        long? FileSize,
        string? FileMimeType,
        string? ThumbnailUrl,
        DateTime SentAt,
        bool IsDeleted,
        bool IsEdited,
        DateTime? ExpiresAt,
        List<ReactionSummaryDto> Reactions,
        List<string> ReadBy,
        bool IsPinned,
        bool IsStarred,
        PollDto? Poll
    );

    public record ReactionSummaryDto(string Emoji, int Count, bool ReactedByMe);

    public record SendMessageRequest(
        int? ReceiverId,
        int? GroupId,
        int? ChannelId,
        int? ThreadParentId,
        string Content,
        string? EncryptedContent,
        bool IsEncrypted,
        string Type,
        string? FileUrl,
        string? FileName,
        long? FileSize,
        string? FileMimeType,
        string? ThumbnailUrl,
        DateTime? ExpiresAt,
        DateTime? ScheduledAt,
        CreatePollRequest? Poll
    );

    public record EditMessageRequest(string NewContent);

    public record MessageEditDto(int Id, string PreviousContent, DateTime EditedAt);

    // ── Polls ─────────────────────────────────────────────────────────────────
    public record CreatePollRequest(string Question, List<string> Options, bool IsMultipleChoice, DateTime? ExpiresAt);

    public record PollDto(
        int Id,
        string Question,
        bool IsMultipleChoice,
        DateTime? ExpiresAt,
        List<PollOptionDto> Options,
        bool HasVoted,
        bool IsCreator
    );

    public record PollOptionDto(int Id, string Text, int VoteCount, bool VotedByMe, List<string> VoterNames);

    // ── Groups ────────────────────────────────────────────────────────────────
    public record CreateGroupRequest(string Name, string? Description, List<int> MemberIds);

    public record GroupDto(
        int Id,
        string Name,
        string? Description,
        string? AvatarUrl,
        int CreatedById,
        DateTime CreatedAt,
        List<GroupMemberDto> Members
    );

    public record GroupMemberDto(int UserId, string Username, string DisplayName, string? AvatarUrl, string Role);

    // ── Channels ──────────────────────────────────────────────────────────────
    public record CreateChannelRequest(string Name, string? Description, bool IsPublic);

    public record ChannelDto(
        int Id,
        string Name,
        string? Description,
        string? AvatarUrl,
        bool IsPublic,
        int SubscriberCount,
        bool IsSubscribed,
        bool IsAdmin
    );

    // ── Conversations list ────────────────────────────────────────────────────
    public record ConversationDto(
        string Type,          // "direct" | "group" | "channel"
        int Id,
        string Name,
        string? AvatarUrl,
        string? LastMessage,
        string? LastMessageSenderName,
        DateTime? LastMessageAt,
        int UnreadCount,
        bool IsOnline,
        bool IsMuted
    );

    // ── Reactions ─────────────────────────────────────────────────────────────
    public record ReactRequest(string Emoji);

    // ── Pinned / Starred ─────────────────────────────────────────────────────
    public record PinnedMessageDto(int PinId, MessageDto Message, string PinnedByName, DateTime PinnedAt, string Category);

    // ── Bots ──────────────────────────────────────────────────────────────────
    public record CreateBotRequest(string Name, string? Description, string? WebhookUrl);

    public record BotDto(int Id, string Name, string? Description, string ApiKey, string? WebhookUrl, bool IsActive, DateTime CreatedAt);

    // ── File upload ───────────────────────────────────────────────────────────
    public record FileUploadResponse(string FileUrl, string FileName, long FileSize, string MimeType, string Type, string? ThumbnailUrl);

    // ── Pagination ────────────────────────────────────────────────────────────
    public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize, bool HasMore);
}