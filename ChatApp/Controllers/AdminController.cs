using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.Data;
using ChatApp.Models;
using ChatApp.DTOs;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _db;

        public AdminController(AppDbContext db) => _db = db;

        [HttpGet("stats")]
        public async Task<IActionResult> Stats()
        {
            var userCount = await _db.Users.CountAsync();
            var bannedCount = await _db.Users.CountAsync(u => u.IsBanned);
            var channelCount = await _db.Channels.CountAsync();
            var messageCount = await _db.Messages.CountAsync();
            var activeUsers = await _db.Users.CountAsync(u => u.IsOnline);

            return Ok(new
            {
                userCount,
                bannedCount,
                channelCount,
                messageCount,
                activeUsers
            });
        }

        [HttpPost("users/{id}/ban")]
        public async Task<IActionResult> Ban(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsBanned = true;
            await _db.SaveChangesAsync();
            return Ok(new { message = "User banned." });
        }

        [HttpPost("users/{id}/unban")]
        public async Task<IActionResult> Unban(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.IsBanned = false;
            await _db.SaveChangesAsync();
            return Ok(new { message = "User unbanned." });
        }

        [HttpDelete("messages/{id}")]
        public async Task<IActionResult> DeleteMessage(int id)
        {
            var msg = await _db.Messages.FindAsync(id);
            if (msg == null) return NotFound();
            msg.IsDeleted = true;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Message marked as deleted." });
        }
    }
}
