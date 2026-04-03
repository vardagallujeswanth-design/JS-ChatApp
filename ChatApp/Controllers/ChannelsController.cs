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
    public class ChannelsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public ChannelsController(AppDbContext db) => _db = db;

        private int Me => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> Search([FromQuery] string? q)
        {
            var query = _db.Channels.Include(c => c.Members).Where(c => c.IsPublic);
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(c => c.Name.Contains(q) || (c.Description != null && c.Description.Contains(q)));

            var channels = await query.Take(30).ToListAsync();
            return Ok(channels.Select(c => MappingService.ToDto(c, Me)));
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateChannelRequest req)
        {
            var channel = new Channel
            {
                Name = req.Name.Trim(),
                Description = req.Description?.Trim(),
                IsPublic = req.IsPublic,
                CreatedById = Me
            };
            _db.Channels.Add(channel);
            await _db.SaveChangesAsync();

            _db.ChannelMembers.Add(new ChannelMember { ChannelId = channel.Id, UserId = Me, IsAdmin = true });
            await _db.SaveChangesAsync();

            return Ok(MappingService.ToDto(channel, Me));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(int id)
        {
            var c = await _db.Channels.Include(c => c.Members).FirstOrDefaultAsync(c => c.Id == id);
            if (c == null) return NotFound();
            return Ok(MappingService.ToDto(c, Me));
        }

        [HttpGet("{id}/analytics")]
        public async Task<IActionResult> Analytics(int id)
        {
            var channel = await _db.Channels.FindAsync(id);
            if (channel == null) return NotFound();

            var totalMessages = await _db.Messages.CountAsync(m => m.ChannelId == id && m.IsSent);
            var silentMessages = await _db.Messages.CountAsync(m => m.ChannelId == id && m.IsSilent && m.IsSent);
            var activeMembers = await _db.ChannelMembers.CountAsync(cm => cm.ChannelId == id);
            var recentActivity = await _db.Messages
                .Where(m => m.ChannelId == id && m.IsSent)
                .OrderByDescending(m => m.SentAt)
                .Select(m => m.SentAt)
                .FirstOrDefaultAsync();

            return Ok(new
            {
                channel.Id,
                channel.Name,
                channel.Description,
                SubscriberCount = activeMembers,
                TotalMessages = totalMessages,
                SilentMessages = silentMessages,
                LastMessageAt = recentActivity
            });
        }

        [HttpGet("discover")]
        public async Task<IActionResult> Discover([FromQuery] string? q)
        {
            var query = _db.Channels.Where(c => c.IsPublic);
            if (!string.IsNullOrWhiteSpace(q))
                query = query.Where(c => c.Name.Contains(q) || (c.Description != null && c.Description.Contains(q)));

            var page = await query.OrderByDescending(c => c.CreatedAt).Take(40).ToListAsync();
            return Ok(page.Select(c => MappingService.ToDto(c, Me)));
        }

        [HttpPost("{id}/subscribe")]
        public async Task<IActionResult> Subscribe(int id)
        {
            var channel = await _db.Channels.FindAsync(id);
            if (channel == null) return NotFound();
            if (!channel.IsPublic) return Forbid();
            if (await _db.ChannelMembers.AnyAsync(cm => cm.ChannelId == id && cm.UserId == Me))
                return BadRequest(new { message = "Already subscribed." });

            _db.ChannelMembers.Add(new ChannelMember { ChannelId = id, UserId = Me });
            await _db.SaveChangesAsync();
            return Ok(new { message = "Subscribed." });
        }

        [HttpDelete("{id}/subscribe")]
        public async Task<IActionResult> Unsubscribe(int id)
        {
            var cm = await _db.ChannelMembers.FirstOrDefaultAsync(cm => cm.ChannelId == id && cm.UserId == Me);
            if (cm == null) return NotFound();
            _db.ChannelMembers.Remove(cm);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Unsubscribed." });
        }
    }
}