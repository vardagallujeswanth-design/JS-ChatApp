using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ChatApp.Data;
using ChatApp.DTOs;
using ChatApp.Models;
using ChatApp.Services;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public MessagesController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private int Me
        {
            get
            {
                var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(idValue, out var id)) return id;
                throw new InvalidOperationException("Authenticated user ID claim is missing or invalid.");
            }
        }

        // ── Fetch messages ────────────────────────────────────────────────────

        [HttpGet("direct/{otherId}")]
        public async Task<IActionResult> GetDirect(int otherId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userId = Me;
            var query = _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes).ThenInclude(v => v.User)
                .Include(m => m.Reactions).ThenInclude(r => r.User)
                .Include(m => m.ReadBy)
                .Include(m => m.Pins)
                .Include(m => m.Stars)
                .Include(m => m.ThreadReplies)
                .Where(m =>
                    m.GroupId == null && m.ChannelId == null &&
                    m.ThreadParentId == null && m.IsSent &&
                    ((m.SenderId == userId && m.ReceiverId == otherId) ||
                     (m.SenderId == otherId && m.ReceiverId == userId)));

            var total = await query.CountAsync();
            var messages = await query
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Mark as read
            await MarkReadAsync(messages, userId);

            return Ok(new PagedResult<MessageDto>(
                messages.OrderBy(m => m.SentAt).Select(m => MappingService.ToDto(m, userId)).ToList(),
                total, page, pageSize, total > page * pageSize
            ));
        }

        [HttpGet("group/{groupId}")]
        public async Task<IActionResult> GetGroup(int groupId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userId = Me;
            if (!await _db.GroupMembers.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId))
                return Forbid();

            var query = _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes).ThenInclude(v => v.User)
                .Include(m => m.Reactions).ThenInclude(r => r.User)
                .Include(m => m.ReadBy)
                .Include(m => m.Pins)
                .Include(m => m.Stars)
                .Include(m => m.ThreadReplies)
                .Where(m => m.GroupId == groupId && m.ThreadParentId == null && m.IsSent);

            var total = await query.CountAsync();
            var messages = await query
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            await MarkReadAsync(messages, userId);

            return Ok(new PagedResult<MessageDto>(
                messages.OrderBy(m => m.SentAt).Select(m => MappingService.ToDto(m, userId)).ToList(),
                total, page, pageSize, total > page * pageSize
            ));
        }

        [HttpGet("channel/{channelId}")]
        public async Task<IActionResult> GetChannel(int channelId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userId = Me;
            if (!await _db.ChannelMembers.AnyAsync(cm => cm.ChannelId == channelId && cm.UserId == userId))
                return Forbid();

            var query = _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes).ThenInclude(v => v.User)
                .Include(m => m.Reactions).ThenInclude(r => r.User)
                .Include(m => m.ReadBy)
                .Include(m => m.Pins)
                .Include(m => m.Stars)
                .Include(m => m.ThreadReplies)
                .Where(m => m.ChannelId == channelId && m.ThreadParentId == null && m.IsSent);

            var total = await query.CountAsync();
            var messages = await query
                .OrderByDescending(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            await MarkReadAsync(messages, userId);

            return Ok(new PagedResult<MessageDto>(
                messages.OrderBy(m => m.SentAt).Select(m => MappingService.ToDto(m, userId)).ToList(),
                total, page, pageSize, total > page * pageSize
            ));
        }

        // Thread replies
        [HttpGet("{messageId}/thread")]
        public async Task<IActionResult> GetThread(int messageId, [FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var userId = Me;
            var parent = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes).ThenInclude(v => v.User)
                .Include(m => m.Reactions).ThenInclude(r => r.User)
                .Include(m => m.ReadBy)
                .Include(m => m.Pins)
                .Include(m => m.Stars)
                .Include(m => m.ThreadReplies)
                .FirstOrDefaultAsync(m => m.Id == messageId);

            if (parent == null) return NotFound();

            var replies = await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes).ThenInclude(v => v.User)
                .Include(m => m.Reactions).ThenInclude(r => r.User)
                .Include(m => m.ReadBy)
                .Include(m => m.Pins)
                .Include(m => m.Stars)
                .Where(m => m.ThreadParentId == messageId && m.IsSent)
                .OrderBy(m => m.SentAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                parent = MappingService.ToDto(parent, userId),
                replies = replies.Select(r => MappingService.ToDto(r, userId)),
                total = await _db.Messages.CountAsync(m => m.ThreadParentId == messageId && m.IsSent)
            });
        }

        // Edit message
        [HttpPut("{id}")]
        public async Task<IActionResult> Edit(int id, [FromBody] EditMessageRequest req)
        {
            var msg = await _db.Messages.FindAsync(id);
            if (msg == null) return NotFound();
            if (msg.SenderId != Me) return Forbid();
            if (msg.IsDeleted) return BadRequest(new { message = "Cannot edit a deleted message." });

            _db.MessageEdits.Add(new MessageEdit { MessageId = id, PreviousContent = msg.Content });
            msg.Content = req.NewContent;
            msg.IsEdited = true;
            await _db.SaveChangesAsync();

            var updated = await LoadFullMessage(id);
            return Ok(MappingService.ToDto(updated!, Me));
        }

        [HttpGet("scheduled")]
        public async Task<IActionResult> GetScheduled()
        {
            var userId = Me;
            var scheduled = await _db.Messages
                .Where(m => m.SenderId == userId && !m.IsSent && m.ScheduledAt.HasValue)
                .OrderBy(m => m.ScheduledAt)
                .Select(m => MappingService.ToDto(m, userId))
                .ToListAsync();
            return Ok(scheduled);
        }

        [HttpPost("scheduled")]
        public async Task<IActionResult> Schedule([FromBody] SendMessageRequest req)
        {
            var senderId = Me;
            if (req.ScheduledAt == null) return BadRequest(new { message = "Schedule time required." });
            if (req.ScheduledAt <= DateTime.UtcNow) return BadRequest(new { message = "Scheduled time must be in the future." });

            var messageType = MessageType.Text;
            if (!string.IsNullOrWhiteSpace(req.Type) && Enum.TryParse<MessageType>(req.Type, true, out var parsedType))
                messageType = parsedType;

            var msg = new Message
            {
                SenderId = senderId,
                ReceiverId = req.ReceiverId,
                GroupId = req.GroupId,
                ChannelId = req.ChannelId,
                ThreadParentId = req.ThreadParentId,
                Content = req.Content ?? string.Empty,
                EncryptedContent = req.EncryptedContent,
                IsEncrypted = req.IsEncrypted,
                Type = messageType,
                FileUrl = req.FileUrl,
                FileName = req.FileName,
                FileSize = req.FileSize,
                FileMimeType = req.FileMimeType,
                ThumbnailUrl = req.ThumbnailUrl,
                ScheduledAt = req.ScheduledAt,
                ExpiresAt = req.ExpiresAt,
                IsSent = false,
                SentAt = DateTime.UtcNow
            };

            _db.Messages.Add(msg);
            await _db.SaveChangesAsync();
            return Ok(MappingService.ToDto(msg, senderId));
        }

        [HttpDelete("scheduled/{id}")]
        public async Task<IActionResult> CancelScheduled(int id)
        {
            var userId = Me;
            var msg = await _db.Messages.FindAsync(id);
            if (msg == null || msg.SenderId != userId || msg.IsSent) return NotFound();

            _db.Messages.Remove(msg);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Scheduled message canceled." });
        }

        // Edit history
        [HttpGet("{id}/history")]
        public async Task<IActionResult> GetHistory(int id)
        {
            var history = await _db.MessageEdits
                .Where(e => e.MessageId == id)
                .OrderByDescending(e => e.EditedAt)
                .Select(e => new MessageEditDto(e.Id, e.PreviousContent, e.EditedAt))
                .ToListAsync();

            return Ok(history);
        }

        // Delete message
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var msg = await _db.Messages.FindAsync(id);
            if (msg == null) return NotFound();
            if (msg.SenderId != Me) return Forbid();

            msg.IsDeleted = true;
            msg.Content = "This message was deleted";
            msg.FileUrl = null;
            msg.EncryptedContent = null;
            await _db.SaveChangesAsync();

            return Ok(new { id });
        }

        // ── Reactions ─────────────────────────────────────────────────────────

        [HttpPost("{id}/react")]
        public async Task<IActionResult> React(int id, [FromBody] ReactRequest req)
        {
            var userId = Me;
            var existing = await _db.MessageReactions
                .FirstOrDefaultAsync(r => r.MessageId == id && r.UserId == userId && r.Emoji == req.Emoji);

            if (existing != null)
            {
                _db.MessageReactions.Remove(existing);
            }
            else
            {
                _db.MessageReactions.Add(new MessageReaction { MessageId = id, UserId = userId, Emoji = req.Emoji });
            }

            await _db.SaveChangesAsync();

            var reactions = await _db.MessageReactions
                .Where(r => r.MessageId == id)
                .GroupBy(r => r.Emoji)
                .Select(g => new ReactionSummaryDto(g.Key, g.Count(), g.Any(r => r.UserId == userId)))
                .ToListAsync();

            return Ok(reactions);
        }

        // ── Pin ───────────────────────────────────────────────────────────────

        [HttpPost("{id}/pin")]
        public async Task<IActionResult> Pin(int id)
        {
            var msg = await _db.Messages.FindAsync(id);
            if (msg == null) return NotFound();

            var existing = await _db.PinnedMessages.FirstOrDefaultAsync(p => p.MessageId == id);
            if (existing != null) return BadRequest(new { message = "Already pinned." });

            _db.PinnedMessages.Add(new PinnedMessage
            {
                MessageId = id,
                PinnedById = Me,
                GroupId = msg.GroupId,
                ChannelId = msg.ChannelId,
                DirectUserId = msg.ReceiverId
            });
            await _db.SaveChangesAsync();
            return Ok(new { message = "Pinned." });
        }

        [HttpDelete("{id}/pin")]
        public async Task<IActionResult> Unpin(int id)
        {
            var pin = await _db.PinnedMessages.FirstOrDefaultAsync(p => p.MessageId == id);
            if (pin == null) return NotFound();
            _db.PinnedMessages.Remove(pin);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Unpinned." });
        }

        // ── Star ──────────────────────────────────────────────────────────────

        [HttpPost("{id}/star")]
        public async Task<IActionResult> Star(int id)
        {
            var userId = Me;
            var existing = await _db.StarredMessages.FirstOrDefaultAsync(s => s.MessageId == id && s.UserId == userId);
            if (existing != null)
            {
                _db.StarredMessages.Remove(existing);
                await _db.SaveChangesAsync();
                return Ok(new { starred = false });
            }

            _db.StarredMessages.Add(new StarredMessage { MessageId = id, UserId = userId });
            await _db.SaveChangesAsync();
            return Ok(new { starred = true });
        }

        // ── File upload (chunked for large files) ─────────────────────────────

        [HttpPost("upload")]
        [RequestSizeLimit(1L * 1024 * 1024 * 1024)] // 1 GB
        public async Task<IActionResult> Upload(IFormFile file)
        {
            var dir = Path.Combine(_env.WebRootPath, "uploads", "files");
            Directory.CreateDirectory(dir);

            var ext = Path.GetExtension(file.FileName).ToLower();
            var fileName = $"{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(dir, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);
            await file.CopyToAsync(stream);

            var mime = file.ContentType;
            var type = DetermineType(ext, mime);
            var fileUrl = $"/uploads/files/{fileName}";

            return Ok(new FileUploadResponse(fileUrl, file.FileName, file.Length, mime, type, null));
        }

        // ── Pinned messages for a chat ────────────────────────────────────────

        [HttpGet("pinned/direct/{otherId}")]
        public async Task<IActionResult> GetDirectPinned(int otherId)
        {
            var userId = Me;
            var pins = await _db.PinnedMessages
                .Include(p => p.Message).ThenInclude(m => m.Sender)
                .Include(p => p.Message).ThenInclude(m => m.Reactions)
                .Include(p => p.Message).ThenInclude(m => m.ReadBy)
                .Include(p => p.Message).ThenInclude(m => m.Stars)
                .Include(p => p.PinnedBy)
                .Where(p => p.Message.GroupId == null &&
                    ((p.Message.SenderId == userId && p.Message.ReceiverId == otherId) ||
                     (p.Message.SenderId == otherId && p.Message.ReceiverId == userId)))
                .OrderByDescending(p => p.PinnedAt)
                .ToListAsync();

            return Ok(pins.Select(p => new PinnedMessageDto(
                p.Id, MappingService.ToDto(p.Message, userId),
                p.PinnedBy.DisplayName ?? p.PinnedBy.Username, p.PinnedAt,
                p.Category.ToString())));
        }

        [HttpGet("pinned/group/{groupId}")]
        public async Task<IActionResult> GetGroupPinned(int groupId)
        {
            var userId = Me;
            var pins = await _db.PinnedMessages
                .Include(p => p.Message).ThenInclude(m => m.Sender)
                .Include(p => p.Message).ThenInclude(m => m.Reactions)
                .Include(p => p.Message).ThenInclude(m => m.ReadBy)
                .Include(p => p.Message).ThenInclude(m => m.Stars)
                .Include(p => p.PinnedBy)
                .Where(p => p.GroupId == groupId)
                .OrderByDescending(p => p.PinnedAt)
                .ToListAsync();

            return Ok(pins.Select(p => new PinnedMessageDto(
                p.Id, MappingService.ToDto(p.Message, userId),
                p.PinnedBy.DisplayName ?? p.PinnedBy.Username, p.PinnedAt,
                p.Category.ToString())));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private async Task MarkReadAsync(List<Message> messages, int userId)
        {
            var unread = messages
                .Where(m => m.SenderId != userId && !m.ReadBy.Any(r => r.UserId == userId))
                .ToList();

            foreach (var m in unread)
                _db.MessageReads.Add(new MessageRead { MessageId = m.Id, UserId = userId });

            if (unread.Any()) await _db.SaveChangesAsync();
        }

        private async Task<Message?> LoadFullMessage(int id)
        {
            return await _db.Messages
                .Include(m => m.Sender)
                .Include(m => m.Poll).ThenInclude(p => p.Options).ThenInclude(o => o.Votes).ThenInclude(v => v.User)
                .Include(m => m.Reactions).ThenInclude(r => r.User)
                .Include(m => m.ReadBy)
                .Include(m => m.Pins)
                .Include(m => m.Stars)
                .Include(m => m.ThreadReplies)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        private static string DetermineType(string ext, string mime)
        {
            if (ext is ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".avif") return "Image";
            if (ext is ".mp4" or ".webm" or ".mov" or ".avi") return "Video";
            if (ext is ".mp3" or ".ogg" or ".wav" or ".m4a") return "Audio";
            if (ext is ".pdf" or ".doc" or ".docx" or ".xls" or ".xlsx" or ".ppt" or ".pptx") return "Document";
            return "File";
        }
    }
}