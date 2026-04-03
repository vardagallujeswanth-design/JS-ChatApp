using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.Data;
using ChatApp.Models;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CallsController : ControllerBase
    {
        private readonly AppDbContext _db;

        public CallsController(AppDbContext db) => _db = db;

        private int Me => int.Parse(User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier)!);

        [HttpGet("history")]
        public async Task<IActionResult> History(int page = 1, int pageSize = 50)
        {
            var entries = await _db.CallLogs
                .Where(c => c.InitiatorId == Me || c.PeerId == Me)
                .OrderByDescending(c => c.StartedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(entries.Select(c => new {
                c.Id, c.Type, c.Direction, c.State, c.InitiatorId, c.PeerId,
                c.StartedAt, c.EndedAt, c.DurationSeconds, c.LowBandwidthMode
            }));
        }

        [HttpPost("start")]
        public async Task<IActionResult> Start([FromBody] StartCallRequest req)
        {
            var call = new CallLog
            {
                Type = req.Type,
                Direction = CallDirection.Outgoing,
                State = CallState.Ringing,
                InitiatorId = Me,
                PeerId = req.PeerId,
                LowBandwidthMode = req.LowBandwidthMode
            };
            _db.CallLogs.Add(call);
            await _db.SaveChangesAsync();
            return Ok(call);
        }

        [HttpPost("missed/{id}")]
        public async Task<IActionResult> Missed(int id)
        {
            var call = await _db.CallLogs.FindAsync(id);
            if (call == null || call.InitiatorId != Me && call.PeerId != Me) return NotFound();
            call.State = CallState.Ended;
            call.DurationSeconds = 0;
            call.EndedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return Ok(call);
        }

        [HttpPost("end/{id}")]
        public async Task<IActionResult> End(int id, [FromBody] EndCallRequest req)
        {
            var call = await _db.CallLogs.FindAsync(id);
            if (call == null || call.InitiatorId != Me && call.PeerId != Me) return NotFound();
            call.State = CallState.Ended;
            call.EndedAt = DateTime.UtcNow;
            call.DurationSeconds = req.DurationSeconds;
            await _db.SaveChangesAsync();
            return Ok(call);
        }

        public record StartCallRequest(int PeerId, CallType Type, bool LowBandwidthMode);
        public record EndCallRequest(int DurationSeconds);
    }
}
