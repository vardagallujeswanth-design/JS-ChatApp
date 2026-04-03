using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.Data;
using ChatApp.DTOs;
using ChatApp.Models;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BotsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public BotsController(AppDbContext db) => _db = db;

        private int Me => int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> List() => Ok(await _db.BotApps.Where(b => b.OwnerId == Me).ToListAsync());

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBotRequest req)
        {
            var bot = new BotApp
            {
                Name = req.Name.Trim(),
                Description = req.Description,
                WebhookUrl = req.WebhookUrl,
                OwnerId = Me,
                ApiKey = Guid.NewGuid().ToString("N")
            };
            _db.BotApps.Add(bot);
            await _db.SaveChangesAsync();
            return Ok(new BotDto(bot.Id, bot.Name, bot.Description, bot.ApiKey, bot.WebhookUrl, bot.IsActive, bot.CreatedAt));
        }

        [HttpPost("{id}/toggle")]
        public async Task<IActionResult> SetActive(int id)
        {
            var bot = await _db.BotApps.FindAsync(id);
            if (bot == null) return NotFound();
            if (bot.OwnerId != Me) return Forbid();
            bot.IsActive = !bot.IsActive;
            await _db.SaveChangesAsync();
            return Ok(new { bot.Id, bot.IsActive });
        }

        [AllowAnonymous]
        [HttpPost("/api/openapi/messages")]
        public async Task<IActionResult> OpenApiSend([FromHeader(Name = "x-api-key")] string apiKey, [FromBody] OpenApiSendRequest req)
        {
            var bot = await _db.BotApps.FirstOrDefaultAsync(b => b.ApiKey == apiKey && b.IsActive);
            if (bot == null) return Unauthorized(new { message = "Invalid API key." });

            var users = await _db.Users.Where(u => req.TargetUserIds.Contains(u.Id)).ToListAsync();
            if (users.Count == 0) return BadRequest(new { message = "No target users." });

            foreach (var target in users)
            {
                var message = new Message
                {
                    SenderId = bot.OwnerId,
                    ReceiverId = target.Id,
                    Content = req.Content,
                    Type = MessageType.Text,
                    IsSent = true
                };
                _db.Messages.Add(message);
            }
            await _db.SaveChangesAsync();

            return Ok(new { message = "Broadcast from bot queued." });
        }

        public record OpenApiSendRequest(string Content, List<int> TargetUserIds);
    }
}
