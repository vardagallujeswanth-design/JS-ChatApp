using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ChatApp.Data;
using ChatApp.DTOs;
using ChatApp.Services;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public UsersController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private int Me => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet("me")]
        public async Task<IActionResult> GetMe()
        {
            var user = await _db.Users.FindAsync(Me);
            return user == null ? NotFound() : Ok(MappingService.ToDto(user));
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest req)
        {
            var user = await _db.Users.FindAsync(Me);
            if (user == null) return NotFound();
            if (req.DisplayName != null) user.DisplayName = req.DisplayName.Trim();
            if (req.About != null) user.About = req.About.Trim();
            await _db.SaveChangesAsync();
            return Ok(MappingService.ToDto(user));
        }

        [HttpPut("me/public-key")]
        public async Task<IActionResult> UpdatePublicKey([FromBody] string publicKey)
        {
            var user = await _db.Users.FindAsync(Me);
            if (user == null) return NotFound();
            user.PublicKey = publicKey;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Public key updated." });
        }

        [HttpPost("me/verify-device")]
        public async Task<IActionResult> VerifyDevice([FromBody] string signedChallenge)
        {
            var user = await _db.Users.FindAsync(Me);
            if (user == null) return NotFound();
            if (string.IsNullOrWhiteSpace(user.PublicKey))
                return BadRequest(new { message = "No public key registered for this account." });

            // Simple placeholder: in production verify signature with RSA/ECDSA.
            if (signedChallenge != "verified")
                return Unauthorized(new { message = "Device verification failed." });

            return Ok(new { message = "Device verified." });
        }

        [HttpPost("me/avatar")]
        public async Task<IActionResult> UploadAvatar(IFormFile file)
        {
            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "Avatar must be under 5 MB." });

            var ext = Path.GetExtension(file.FileName).ToLower();
            if (ext is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
                return BadRequest(new { message = "Only JPG, PNG and WebP are allowed." });

            var user = await _db.Users.FindAsync(Me);
            if (user == null) return NotFound();

            var dir = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(dir);

            var fileName = $"u{Me}_{Guid.NewGuid():N}{ext}";
            await using var stream = new FileStream(Path.Combine(dir, fileName), FileMode.Create);
            await file.CopyToAsync(stream);

            user.AvatarUrl = $"/uploads/avatars/{fileName}";
            await _db.SaveChangesAsync();

            return Ok(new { avatarUrl = user.AvatarUrl });
        }

        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q)
        {
            if (string.IsNullOrWhiteSpace(q) || q.Length < 2)
                return Ok(new List<UserDto>());

            var lower = q.ToLower();
            var users = await _db.Users
                .Where(u => u.Id != Me && (
                    u.Username.Contains(lower) ||
                    u.DisplayName.Contains(lower) ||
                    u.PhoneNumber.Contains(lower)))
                .OrderBy(u => u.Username)
                .Take(20)
                .Select(u => MappingService.ToDto(u))
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetUser(int id)
        {
            var user = await _db.Users.FindAsync(id);
            return user == null ? NotFound() : Ok(MappingService.ToDto(user));
        }

        [HttpGet("conversations")]
        public async Task<IActionResult> GetConversations()
        {
            var userId = Me;
            var result = new List<ConversationDto>();

            // Direct messages — get latest message per other user
            var directPairs = await _db.Messages
                .Where(msg =>
                    msg.GroupId == null && msg.ChannelId == null &&
                    !msg.IsDeleted && msg.IsSent &&
                    (msg.SenderId == userId || msg.ReceiverId == userId))
                .GroupBy(msg => msg.SenderId == userId ? msg.ReceiverId!.Value : msg.SenderId)
                .Select(g => new { OtherUserId = g.Key, LastMsg = g.OrderByDescending(m => m.SentAt).First() })
                .ToListAsync();

            foreach (var pair in directPairs)
            {
                var other = await _db.Users.FindAsync(pair.OtherUserId);
                if (other == null) continue;

                var unread = await _db.Messages.CountAsync(msg =>
                    msg.SenderId == pair.OtherUserId &&
                    msg.ReceiverId == userId && !msg.IsDeleted &&
                    !msg.ReadBy.Any(r => r.UserId == userId));

                result.Add(new ConversationDto(
                    "direct", other.Id,
                    other.DisplayName ?? other.Username,
                    other.AvatarUrl,
                    pair.LastMsg.IsDeleted ? "Message deleted" : pair.LastMsg.Content,
                    null,
                    pair.LastMsg.SentAt,
                    unread, other.IsOnline, false
                ));
            }

            // Groups
            var memberships = await _db.GroupMembers
                .Where(gm => gm.UserId == userId)
                .Include(gm => gm.Group)
                .ToListAsync();

            foreach (var gm in memberships)
            {
                var lastMsg = await _db.Messages
                    .Include(m => m.Sender)
                    .Where(m => m.GroupId == gm.GroupId && !m.IsDeleted && m.IsSent)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                var unread = await _db.Messages.CountAsync(m =>
                    m.GroupId == gm.GroupId && m.SenderId != userId &&
                    !m.IsDeleted && !m.ReadBy.Any(r => r.UserId == userId));

                result.Add(new ConversationDto(
                    "group", gm.GroupId,
                    gm.Group.Name, gm.Group.AvatarUrl,
                    lastMsg?.IsDeleted == true ? "Message deleted" : lastMsg?.Content,
                    lastMsg?.Sender?.DisplayName ?? lastMsg?.Sender?.Username,
                    lastMsg?.SentAt,
                    unread, false, false
                ));
            }

            // Channels
            var channelMemberships = await _db.ChannelMembers
                .Where(cm => cm.UserId == userId)
                .Include(cm => cm.Channel)
                .ToListAsync();

            foreach (var cm in channelMemberships)
            {
                var lastMsg = await _db.Messages
                    .Include(m => m.Sender)
                    .Where(m => m.ChannelId == cm.ChannelId && !m.IsDeleted && m.IsSent)
                    .OrderByDescending(m => m.SentAt)
                    .FirstOrDefaultAsync();

                var unread = await _db.Messages.CountAsync(m =>
                    m.ChannelId == cm.ChannelId && !m.IsDeleted &&
                    !m.ReadBy.Any(r => r.UserId == userId));

                result.Add(new ConversationDto(
                    "channel", cm.ChannelId,
                    cm.Channel.Name, cm.Channel.AvatarUrl,
                    lastMsg?.IsDeleted == true ? "Message deleted" : lastMsg?.Content,
                    lastMsg?.Sender?.DisplayName ?? lastMsg?.Sender?.Username,
                    lastMsg?.SentAt,
                    unread, false, false
                ));
            }

            return Ok(result.OrderByDescending(c => c.LastMessageAt));
        }

        // Starred messages for current user
        [HttpGet("me/starred")]
        public async Task<IActionResult> GetStarred()
        {
            var userId = Me;
            var stars = await _db.StarredMessages
                .Where(s => s.UserId == userId)
                .Include(s => s.Message)
                    .ThenInclude(m => m.Sender)
                .Include(s => s.Message)
                    .ThenInclude(m => m.Reactions)
                .Include(s => s.Message)
                    .ThenInclude(m => m.ReadBy)
                .Include(s => s.Message)
                    .ThenInclude(m => m.Pins)
                .Include(s => s.Message)
                    .ThenInclude(m => m.Stars)
                .OrderByDescending(s => s.StarredAt)
                .Take(100)
                .ToListAsync();

            return Ok(stars.Select(s => MappingService.ToDto(s.Message, userId)));
        }
    }
}