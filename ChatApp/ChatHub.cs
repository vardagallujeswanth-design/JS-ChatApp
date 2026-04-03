using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Security.Claims;
using ChatApp.Data;
using ChatApp.DTOs;
using ChatApp.Models;
using ChatApp.Services;

namespace ChatApp.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        // userId -> set of connectionIds (multiple tabs/devices)
        private static readonly ConcurrentDictionary<int, HashSet<string>> _connections = new();
        private static readonly object _lock = new();

        private readonly AppDbContext _db;

        public ChatHub(AppDbContext db) => _db = db;

        private int Me => int.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

        // ── Connection lifecycle ──────────────────────────────────────────────

        public override async Task OnConnectedAsync()
        {
            var userId = Me;

            lock (_lock)
            {
                _connections.GetOrAdd(userId, _ => new HashSet<string>()).Add(Context.ConnectionId);
            }

            // Join group rooms
            var groupIds = await _db.GroupMembers
                .Where(gm => gm.UserId == userId)
                .Select(gm => gm.GroupId)
                .ToListAsync();
            foreach (var gid in groupIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, GroupRoom(gid));

            // Join channel rooms
            var channelIds = await _db.ChannelMembers
                .Where(cm => cm.UserId == userId)
                .Select(cm => cm.ChannelId)
                .ToListAsync();
            foreach (var cid in channelIds)
                await Groups.AddToGroupAsync(Context.ConnectionId, ChannelRoom(cid));

            // Mark online
            var user = await _db.Users.FindAsync(userId);
            if (user != null) { user.IsOnline = true; await _db.SaveChangesAsync(); }

            await NotifyPresence(userId, true);
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? ex)
        {
            var userId = Me;
            bool gone = false;

            lock (_lock)
            {
                if (_connections.TryGetValue(userId, out var set))
                {
                    set.Remove(Context.ConnectionId);
                    gone = set.Count == 0;
                    if (gone) _connections.TryRemove(userId, out _);
                }
            }

            if (gone)
            {
                var user = await _db.Users.FindAsync(userId);
                if (user != null) { user.IsOnline = false; user.LastSeen = DateTime.UtcNow; await _db.SaveChangesAsync(); }
                await NotifyPresence(userId, false);
            }

            await base.OnDisconnectedAsync(ex);
        }

        // ── Send messages ─────────────────────────────────────────────────────

        public async Task SendDirectMessage(
            int receiverId, string content,
            string? encryptedContent, bool isEncrypted,
            string type, string? fileUrl, string? fileName,
            long? fileSize, string? fileMimeType, string? thumbnailUrl,
            int? threadParentId, DateTime? expiresAt)
        {
            var senderId = Me;
            var msgType = Enum.TryParse<MessageType>(type, out var t) ? t : MessageType.Text;

            var msg = new Message
            {
                SenderId = senderId, ReceiverId = receiverId,
                Content = content, EncryptedContent = encryptedContent, IsEncrypted = isEncrypted,
                Type = msgType, FileUrl = fileUrl, FileName = fileName,
                FileSize = fileSize, FileMimeType = fileMimeType, ThumbnailUrl = thumbnailUrl,
                ThreadParentId = threadParentId, ExpiresAt = expiresAt, IsSent = true
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            await _db.Entry(msg).Reference(m => m.Sender).LoadAsync();

            var dto = MappingService.ToDto(msg, senderId);

            // Deliver to receiver (all devices)
            await SendToUser(receiverId, "ReceiveMessage", dto);
            // Deliver back to sender (other tabs)
            await SendToUser(senderId, "ReceiveMessage", dto);
        }

        public async Task SendGroupMessage(
            int groupId, string content,
            string? encryptedContent, bool isEncrypted,
            string type, string? fileUrl, string? fileName,
            long? fileSize, string? fileMimeType, string? thumbnailUrl,
            int? threadParentId, DateTime? expiresAt)
        {
            var senderId = Me;
            if (!await _db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == senderId)) return;

            var msgType = Enum.TryParse<MessageType>(type, out var t) ? t : MessageType.Text;
            var msg = new Message
            {
                SenderId = senderId, GroupId = groupId,
                Content = content, EncryptedContent = encryptedContent, IsEncrypted = isEncrypted,
                Type = msgType, FileUrl = fileUrl, FileName = fileName,
                FileSize = fileSize, FileMimeType = fileMimeType, ThumbnailUrl = thumbnailUrl,
                ThreadParentId = threadParentId, ExpiresAt = expiresAt, IsSent = true
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            await _db.Entry(msg).Reference(m => m.Sender).LoadAsync();
            await Clients.Group(GroupRoom(groupId)).SendAsync("ReceiveMessage", MappingService.ToDto(msg, senderId));
        }

        public async Task SendChannelMessage(
            int channelId, string content, string type,
            string? fileUrl, string? fileName, long? fileSize,
            bool isSilent = false)
        {
            var senderId = Me;
            var isAdmin = await _db.ChannelMembers.AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == senderId && cm.IsAdmin);
            if (!isAdmin) return;

            var msgType = Enum.TryParse<MessageType>(type, out var t) ? t : MessageType.Text;
            var msg = new Message
            {
                SenderId = senderId, ChannelId = channelId,
                Content = content, Type = msgType,
                FileUrl = fileUrl, FileName = fileName, FileSize = fileSize,
                IsSilent = isSilent,
                IsSent = true
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            await _db.Entry(msg).Reference(m => m.Sender).LoadAsync();
            await Clients.Group(ChannelRoom(channelId)).SendAsync("ReceiveMessage", MappingService.ToDto(msg, senderId));
        }

        // ── Typing indicators ─────────────────────────────────────────────────

        public async Task Typing(int chatId, string chatType)
        {
            var userId = Me;
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return;

            var payload = new { UserId = userId, Name = user.DisplayName ?? user.Username };

            if (chatType == "group")
                await Clients.OthersInGroup(GroupRoom(chatId)).SendAsync("UserTyping", payload);
            else if (chatType == "channel")
                await Clients.OthersInGroup(ChannelRoom(chatId)).SendAsync("UserTyping", payload);
            else
                await SendToUser(chatId, "UserTyping", payload);
        }

        public async Task StopTyping(int chatId, string chatType)
        {
            var userId = Me;
            if (chatType == "group")
                await Clients.OthersInGroup(GroupRoom(chatId)).SendAsync("UserStopTyping", userId);
            else if (chatType == "channel")
                await Clients.OthersInGroup(ChannelRoom(chatId)).SendAsync("UserStopTyping", userId);
            else
                await SendToUser(chatId, "UserStopTyping", userId);
        }

        // ── Read receipts ─────────────────────────────────────────────────────

        public async Task MarkRead(int chatId, string chatType)
        {
            var userId = Me;
            List<Message> unread;

            if (chatType == "group")
                unread = await _db.Messages.Include(m => m.ReadBy)
                    .Where(m => m.GroupId == chatId && m.SenderId != userId && !m.ReadBy.Any(r => r.UserId == userId))
                    .ToListAsync();
            else if (chatType == "channel")
                unread = await _db.Messages.Include(m => m.ReadBy)
                    .Where(m => m.ChannelId == chatId && !m.ReadBy.Any(r => r.UserId == userId))
                    .ToListAsync();
            else
                unread = await _db.Messages.Include(m => m.ReadBy)
                    .Where(m => m.SenderId == chatId && m.ReceiverId == userId && !m.ReadBy.Any(r => r.UserId == userId))
                    .ToListAsync();

            foreach (var m in unread)
                _db.MessageReads.Add(new MessageRead { MessageId = m.Id, UserId = userId });

            if (unread.Any())
            {
                await _db.SaveChangesAsync();
                if (chatType == "direct")
                    await SendToUser(chatId, "MessagesRead", userId);
            }
        }

        // ── Reactions (real-time broadcast) ───────────────────────────────────

        public async Task ReactToMessage(int messageId, string emoji)
        {
            var userId = Me;
            var msg = await _db.Messages.FindAsync(messageId);
            if (msg == null) return;

            var existing = await _db.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == messageId && r.UserId == userId && r.Emoji == emoji);

            if (existing != null) _db.MessageReactions.Remove(existing);
            else _db.MessageReactions.Add(new MessageReaction { MessageId = messageId, UserId = userId, Emoji = emoji });

            await _db.SaveChangesAsync();

            var reactions = await _db.MessageReactions
                .Where(r => r.MessageId == messageId)
                .GroupBy(r => r.Emoji)
                .Select(g => new { Emoji = g.Key, Count = g.Count() })
                .ToListAsync();

            var payload = new { messageId, reactions };

            if (msg.GroupId.HasValue)
                await Clients.Group(GroupRoom(msg.GroupId.Value)).SendAsync("ReactionsUpdated", payload);
            else if (msg.ChannelId.HasValue)
                await Clients.Group(ChannelRoom(msg.ChannelId.Value)).SendAsync("ReactionsUpdated", payload);
            else
            {
                if (msg.ReceiverId.HasValue) await SendToUser(msg.ReceiverId.Value, "ReactionsUpdated", payload);
                await SendToUser(msg.SenderId, "ReactionsUpdated", payload);
            }
        }

        // ── Polls (real-time broadcast) ───────────────────────────────────────

        private async Task<MessageDto> CreatePollMessage(int? receiverId, int? groupId, int? channelId,
            string question, List<string> options, bool isMultipleChoice, DateTime? expiresAt, int? threadParentId)
        {
            var senderId = Me;

            var msg = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                GroupId = groupId,
                ChannelId = channelId,
                Content = question,
                Type = MessageType.Poll,
                ThreadParentId = threadParentId,
                ExpiresAt = expiresAt,
                IsSent = true
            };
            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();

            var poll = new Poll
            {
                MessageId = msg.Id,
                Question = question,
                IsMultipleChoice = isMultipleChoice,
                ExpiresAt = expiresAt
            };
            _db.Polls.Add(poll);
            await _db.SaveChangesAsync();

            foreach (var optionText in options.Where(o => !string.IsNullOrWhiteSpace(o)).Distinct())
            {
                _db.PollOptions.Add(new PollOption { PollId = poll.Id, Text = optionText.Trim() });
            }
            await _db.SaveChangesAsync();

            await _db.Entry(msg).Reference(m => m.Sender).LoadAsync();
            await _db.Entry(msg).Reference(m => m.Poll).LoadAsync();
            if (msg.Poll != null)
            {
                await _db.Entry(msg.Poll).Collection(p => p.Options).LoadAsync();
                foreach (var o in msg.Poll.Options)
                    await _db.Entry(o).Collection(opt => opt.Votes).LoadAsync();
            }

            return MappingService.ToDto(msg, senderId);
        }

        public async Task SendDirectPoll(int receiverId, string question, List<string> options, bool isMultipleChoice, DateTime? expiresAt, int? threadParentId)
        {
            var dto = await CreatePollMessage(receiverId, null, null, question, options, isMultipleChoice, expiresAt, threadParentId);
            await SendToUser(receiverId, "ReceiveMessage", dto);
            await SendToUser(Me, "ReceiveMessage", dto);
        }

        public async Task SendGroupPoll(int groupId, string question, List<string> options, bool isMultipleChoice, DateTime? expiresAt, int? threadParentId)
        {
            var dto = await CreatePollMessage(null, groupId, null, question, options, isMultipleChoice, expiresAt, threadParentId);
            await Clients.Group(GroupRoom(groupId)).SendAsync("ReceiveMessage", dto);
        }

        public async Task SendChannelPoll(int channelId, string question, List<string> options, bool isMultipleChoice, DateTime? expiresAt)
        {
            var dto = await CreatePollMessage(null, null, channelId, question, options, isMultipleChoice, expiresAt, null);
            await Clients.Group(ChannelRoom(channelId)).SendAsync("ReceiveMessage", dto);
        }

        public async Task VotePoll(int pollOptionId)
        {
            var userId = Me;
            var option = await _db.PollOptions
                .Include(o => o.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes).ThenInclude(v => v.User)
                .FirstOrDefaultAsync(o => o.Id == pollOptionId);
            if (option == null || option.Poll == null) return;

            var poll = option.Poll;
            if (poll.ExpiresAt.HasValue && poll.ExpiresAt.Value < DateTime.UtcNow) return;

            var currentVotes = await _db.PollVotes
                .Where(v => v.PollOption.PollId == poll.Id && v.UserId == userId)
                .ToListAsync();

            var alreadyVotedThis = currentVotes.Any(v => v.PollOptionId == pollOptionId);

            if (poll.IsMultipleChoice)
            {
                if (alreadyVotedThis)
                {
                    _db.PollVotes.Remove(currentVotes.First(v => v.PollOptionId == pollOptionId));
                }
                else
                {
                    _db.PollVotes.Add(new PollVote { PollOptionId = pollOptionId, UserId = userId });
                }
            }
            else
            {
                _db.PollVotes.RemoveRange(currentVotes);
                if (!alreadyVotedThis) _db.PollVotes.Add(new PollVote { PollOptionId = pollOptionId, UserId = userId });
            }

            await _db.SaveChangesAsync();

            var msg = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes).ThenInclude(v => v.User)
                .FirstOrDefaultAsync(m => m.Id == poll.MessageId);
            if (msg == null) return;

            var pollDto = MappingService.ToDto(msg, userId).Poll;
            if (pollDto == null) return;

            if (msg.GroupId.HasValue)
                await Clients.Group(GroupRoom(msg.GroupId.Value)).SendAsync("PollUpdated", new { messageId = msg.Id, poll = pollDto });
            else if (msg.ChannelId.HasValue)
                await Clients.Group(ChannelRoom(msg.ChannelId.Value)).SendAsync("PollUpdated", new { messageId = msg.Id, poll = pollDto });
            else
            {
                if (msg.ReceiverId.HasValue) await SendToUser(msg.ReceiverId.Value, "PollUpdated", new { messageId = msg.Id, poll = pollDto });
                await SendToUser(msg.SenderId, "PollUpdated", new { messageId = msg.Id, poll = pollDto });
            }
        }

        // ── Message deleted / edited broadcast ─────────────────────────────── ─

        public async Task BroadcastEdit(int messageId)
        {
            var msg = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Reactions)
                .Include(m => m.ReadBy)
                .Include(m => m.Pins)
                .Include(m => m.Stars)
                .Include(m => m.ThreadReplies)
                .FirstOrDefaultAsync(m => m.Id == messageId);
            if (msg == null) return;

            var dto = MappingService.ToDto(msg, Me);
            await BroadcastToChat(msg, "MessageEdited", dto);
        }

        public async Task BroadcastDelete(int messageId)
        {
            var msg = await _db.Messages.FindAsync(messageId);
            if (msg == null) return;
            await BroadcastToChat(msg, "MessageDeleted", new { messageId });
        }

        public async Task PinMessage(int messageId, string category)
        {
            var userId = Me;
            var msg = await _db.Messages.FindAsync(messageId);
            if (msg == null) return;

            bool isAllowed = false;
            if (msg.GroupId.HasValue)
                isAllowed = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == msg.GroupId && gm.UserId == userId && gm.IsAdmin);
            else if (msg.ChannelId.HasValue)
                isAllowed = await _db.ChannelMembers.AnyAsync(cm => cm.ChannelId == msg.ChannelId && cm.UserId == userId && cm.IsAdmin);
            else
                isAllowed = msg.SenderId == userId || msg.ReceiverId == userId;

            if (!isAllowed) return;

            if (await _db.PinnedMessages.AnyAsync(p => p.MessageId == messageId)) return;

            if (!Enum.TryParse<PinCategory>(category, true, out var parsedCategory))
                parsedCategory = PinCategory.General;

            var pinned = new PinnedMessage
            {
                MessageId = messageId,
                PinnedById = userId,
                GroupId = msg.GroupId,
                ChannelId = msg.ChannelId,
                DirectUserId = msg.ReceiverId.HasValue ? msg.ReceiverId : (msg.SenderId == userId ? msg.ReceiverId : msg.SenderId),
                Category = parsedCategory,
                PinnedAt = DateTime.UtcNow
            };

            _db.PinnedMessages.Add(pinned);
            await _db.SaveChangesAsync();

            await BroadcastToChat(msg, "MessagePinned", new
            {
                pin = new
                {
                    pinned.Id,
                    pinned.MessageId,
                    pinned.PinnedById,
                    pinned.ChannelId,
                    pinned.GroupId,
                    pinned.Category,
                    pinned.PinnedAt
                },
                message = MappingService.ToDto(msg, userId)
            });
        }

        public async Task UnpinMessage(int messageId)
        {
            var userId = Me;
            var pin = await _db.PinnedMessages.Include(p => p.Message).FirstOrDefaultAsync(p => p.MessageId == messageId);
            if (pin == null) return;

            bool isAllowed = false;
            if (pin.GroupId.HasValue)
                isAllowed = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == pin.GroupId && gm.UserId == userId && gm.IsAdmin);
            else if (pin.ChannelId.HasValue)
                isAllowed = await _db.ChannelMembers.AnyAsync(cm => cm.ChannelId == pin.ChannelId && cm.UserId == userId && cm.IsAdmin);
            else
                isAllowed = pin.PinnedById == userId || pin.Message.SenderId == userId;

            if (!isAllowed) return;

            _db.PinnedMessages.Remove(pin);
            await _db.SaveChangesAsync();

            await BroadcastToChat(pin.Message, "MessageUnpinned", new
            {
                messageId,
                channelId = pin.ChannelId,
                groupId = pin.GroupId,
                pinId = pin.Id
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task SendToUser(int userId, string method, object payload)
        {
            if (_connections.TryGetValue(userId, out var conns))
                foreach (var connId in conns.ToArray())
                    await Clients.Client(connId).SendAsync(method, payload);
        }

        private async Task BroadcastToChat(Message msg, string method, object payload)
        {
            if (msg.GroupId.HasValue)
                await Clients.Group(GroupRoom(msg.GroupId.Value)).SendAsync(method, payload);
            else if (msg.ChannelId.HasValue)
                await Clients.Group(ChannelRoom(msg.ChannelId.Value)).SendAsync(method, payload);
            else
            {
                if (msg.ReceiverId.HasValue) await SendToUser(msg.ReceiverId.Value, method, payload);
                await SendToUser(msg.SenderId, method, payload);
            }
        }

        private async Task NotifyPresence(int userId, bool online)
        {
            var contactIds = await _db.Messages
                .Where(m => (m.SenderId == userId || m.ReceiverId == userId) && m.GroupId == null)
                .Select(m => m.SenderId == userId ? m.ReceiverId!.Value : m.SenderId)
                .Distinct().ToListAsync();

            foreach (var cid in contactIds)
                await SendToUser(cid, "UserPresence", new { userId, online });
        }

        private static string GroupRoom(int id) => $"group_{id}";
        private static string ChannelRoom(int id) => $"channel_{id}";
    }
}