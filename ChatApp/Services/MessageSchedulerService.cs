using Hangfire;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ChatApp.Data;
using ChatApp.Hubs;
using ChatApp.Models;
using ChatApp.Services;

namespace ChatApp.Services
{
    public class MessageSchedulerService
    {
        private readonly AppDbContext _db;
        private readonly IHubContext<ChatHub> _hub;

        public MessageSchedulerService(AppDbContext db, IHubContext<ChatHub> hub)
        {
            _db = db;
            _hub = hub;
        }

        public async Task ProcessScheduledMessages()
        {
            var now = DateTime.UtcNow;
            var scheduled = await _db.Messages
                .Where(m => !m.IsSent && m.ScheduledAt.HasValue && m.ScheduledAt <= now)
                .ToListAsync();

            if (!scheduled.Any()) return;

            foreach (var msg in scheduled)
            {
                msg.IsSent = true;
                msg.SentAt = msg.ScheduledAt ?? now;
                await BroadcastNewMessage(msg);
            }

            await _db.SaveChangesAsync();
        }

        public async Task ProcessExpiredMessages()
        {
            var now = DateTime.UtcNow;
            var expired = await _db.Messages
                .Where(m => m.IsSent && !m.IsDeleted && m.ExpiresAt.HasValue && m.ExpiresAt <= now)
                .ToListAsync();

            if (!expired.Any()) return;

            foreach (var msg in expired)
            {
                msg.IsDeleted = true;
                msg.Content = "This message disappeared";
                msg.FileUrl = null;
                msg.FileName = null;
                msg.EncryptedContent = null;

                await BroadcastDisappearedMessage(msg);
            }

            await _db.SaveChangesAsync();
        }

        private async Task BroadcastNewMessage(Message msg)
        {
            await _db.Entry(msg).Reference(m => m.Sender).LoadAsync();
            var dto = MappingService.ToDto(msg, msg.SenderId);

            if (msg.GroupId.HasValue)
            {
                await _hub.Clients.Group($"group_{msg.GroupId.Value}").SendAsync("ReceiveMessage", dto);
            }
            else if (msg.ChannelId.HasValue)
            {
                await _hub.Clients.Group($"channel_{msg.ChannelId.Value}").SendAsync("ReceiveMessage", dto);
            }
            else
            {
                if (msg.ReceiverId.HasValue)
                    await _hub.Clients.User(msg.ReceiverId.Value.ToString()).SendAsync("ReceiveMessage", dto);

                await _hub.Clients.User(msg.SenderId.ToString()).SendAsync("ReceiveMessage", dto);
            }
        }

        private async Task BroadcastDisappearedMessage(Message msg)
        {
            if (msg.GroupId.HasValue)
            {
                await _hub.Clients.Group($"group_{msg.GroupId.Value}").SendAsync("MessageDisappeared", new { messageId = msg.Id });
            }
            else if (msg.ChannelId.HasValue)
            {
                await _hub.Clients.Group($"channel_{msg.ChannelId.Value}").SendAsync("MessageDisappeared", new { messageId = msg.Id });
            }
            else
            {
                if (msg.ReceiverId.HasValue)
                    await _hub.Clients.User(msg.ReceiverId.Value.ToString()).SendAsync("MessageDisappeared", new { messageId = msg.Id });

                await _hub.Clients.User(msg.SenderId.ToString()).SendAsync("MessageDisappeared", new { messageId = msg.Id });
            }
        }
    }
}
