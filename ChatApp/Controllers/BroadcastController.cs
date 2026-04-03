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
    public class BroadcastController : ControllerBase
    {
        private readonly AppDbContext _db;

        public BroadcastController(AppDbContext db) => _db = db;

        private int Me => int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);

        [HttpGet]
        public async Task<IActionResult> GetAll() => Ok(await _db.BroadcastLists.Where(b => b.CreatedById == Me).Include(b => b.Members).ToListAsync());

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] BroadcastCreateRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return BadRequest(new { message = "Name required." });

            var broadcast = new BroadcastList { Name = req.Name, CreatedById = Me };
            _db.BroadcastLists.Add(broadcast);
            await _db.SaveChangesAsync();

            if (req.MemberIds != null && req.MemberIds.Any())
            {
                _db.BroadcastListMembers.AddRange(req.MemberIds.Distinct().Select(id => new BroadcastListMember { BroadcastListId = broadcast.Id, UserId = id }));
                await _db.SaveChangesAsync();
            }

            return Ok(broadcast);
        }

        [HttpPost("{id}/members")]
        public async Task<IActionResult> AddMembers(int id, [FromBody] List<int> memberIds)
        {
            var list = await _db.BroadcastLists.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id && b.CreatedById == Me);
            if (list == null) return NotFound();
            var existing = list.Members.Select(m => m.UserId).ToHashSet();
            var toAdd = memberIds.Except(existing).Distinct().Select(uid => new BroadcastListMember { BroadcastListId = id, UserId = uid });
            _db.BroadcastListMembers.AddRange(toAdd);
            await _db.SaveChangesAsync();
            return Ok(list);
        }

        [HttpPost("{id}/send")]
        public async Task<IActionResult> Send(int id, [FromBody] BroadcastSendRequest req)
        {
            var list = await _db.BroadcastLists.Include(b => b.Members).FirstOrDefaultAsync(b => b.Id == id && b.CreatedById == Me);
            if (list == null) return NotFound();

            var members = list.Members.Select(m => m.UserId).ToList();
            if (!members.Any()) return BadRequest(new { message = "No recipients." });

            foreach (var memberId in members)
            {
                _db.Messages.Add(new Message
                {
                    SenderId = Me,
                    ReceiverId = memberId,
                    Content = req.Content,
                    IsSent = true,
                    Type = MessageType.Text
                });
            }
            await _db.SaveChangesAsync();
            return Ok(new { message = "Broadcast sent." });
        }

        public record BroadcastCreateRequest(string Name, List<int>? MemberIds);
        public record BroadcastSendRequest(string Content);
    }
}
