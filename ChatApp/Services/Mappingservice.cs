
using ChatApp.DTOs;
using ChatApp.Models;

namespace ChatApp.Services
{
    public static class MappingService
    {
        public static UserDto ToDto(User u) => new(
            u.Id, u.Username, u.PhoneNumber ?? "",
            u.DisplayName ?? u.Username,
            u.AvatarUrl, u.About,
            u.IsOnline, u.LastSeen,
            u.PublicKey,
            u.IsTwoFactorEnabled
        );

        public static MessageDto ToDto(Message m, int currentUserId)
        {
            var reactionGroups = m.Reactions
                .GroupBy(r => r.Emoji)
                .Select(g => new ReactionSummaryDto(
                    g.Key,
                    g.Count(),
                    g.Any(r => r.UserId == currentUserId)
                ))
                .ToList();

            PollDto? pollDto = null;
            if (m.Type == MessageType.Poll && m.Poll != null)
            {
                var totalVotes = m.Poll.Options?.Sum(o => o.Votes.Count) ?? 0;
                var hasVoted = m.Poll.Options?.Any(o => o.Votes.Any(v => v.UserId == currentUserId)) ?? false;
                var isCreator = m.SenderId == currentUserId;

                pollDto = new PollDto(
                    m.Poll.Id,
                    m.Poll.Question,
                    m.Poll.IsMultipleChoice,
                    m.Poll.ExpiresAt,
                    m.Poll.Options?.Select(o => new PollOptionDto(
                        o.Id,
                        o.Text,
                        o.Votes.Count,
                        o.Votes.Any(v => v.UserId == currentUserId),
                        isCreator ? o.Votes.Select(v => v.User?.DisplayName ?? v.User?.Username ?? "Unknown").ToList() : new List<string>()
                    )).ToList() ?? new List<PollOptionDto>(),
                    hasVoted,
                    isCreator
                );
            }

            return new MessageDto(
                m.Id,
                m.SenderId,
                m.Sender?.DisplayName ?? m.Sender?.Username ?? "",
                m.Sender?.AvatarUrl,
                m.ReceiverId,
                m.GroupId,
                m.ChannelId,
                m.ThreadParentId,
                m.ThreadReplies?.Count ?? 0,
                m.IsDeleted ? "This message was deleted" : m.Content,
                m.IsDeleted ? null : m.EncryptedContent,
                m.IsEncrypted,
                m.IsSilent,
                m.Type.ToString(),
                m.IsDeleted ? null : m.FileUrl,
                m.IsDeleted ? null : m.FileName,
                m.FileSize,
                m.IsDeleted ? null : m.FileMimeType,
                m.IsDeleted ? null : m.ThumbnailUrl,
                m.SentAt,
                m.IsDeleted,
                m.IsEdited,
                m.ExpiresAt,
                reactionGroups,
                m.ReadBy?.Select(r => r.UserId.ToString()).ToList() ?? new(),
                m.Pins?.Any() ?? false,
                m.Stars?.Any(s => s.UserId == currentUserId) ?? false,
                pollDto
            );
        }

        public static GroupDto ToDto(Group g) => new(
            g.Id, g.Name, g.Description, g.AvatarUrl,
            g.CreatedById, g.CreatedAt,
            g.Members?.Select(gm => new GroupMemberDto(
                gm.UserId,
                gm.User?.Username ?? "",
                gm.User?.DisplayName ?? gm.User?.Username ?? "",
                gm.User?.AvatarUrl,
                gm.Role.ToString()
            )).ToList() ?? new()
        );

        public static ChannelDto ToDto(Channel c, int currentUserId) => new(
            c.Id, c.Name, c.Description, c.AvatarUrl, c.IsPublic,
            c.Members?.Count ?? 0,
            c.Members?.Any(cm => cm.UserId == currentUserId) ?? false,
            c.Members?.Any(cm => cm.UserId == currentUserId && cm.IsAdmin) ?? false
        );
    }
}