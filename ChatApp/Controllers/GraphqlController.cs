using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ChatApp.Data;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GraphqlController : ControllerBase
    {
        private readonly AppDbContext _db;

        public GraphqlController(AppDbContext db) => _db = db;

        public class GraphqlQuery { public string Query { get; set; } = ""; }

        [HttpPost]
        public async Task<IActionResult> Query([FromBody] GraphqlQuery q)
        {
            if (string.IsNullOrWhiteSpace(q.Query)) return BadRequest(new { message = "Query is required." });

            if (q.Query.Contains("channels", StringComparison.OrdinalIgnoreCase))
            {
                var channels = await _db.Channels.Include(c => c.Members).ToListAsync();
                return Ok(new { data = new { channels = channels.Select(c => new { c.Id, c.Name, c.Description, SubscriberCount = c.Members.Count }) } });
            }

            if (q.Query.Contains("users", StringComparison.OrdinalIgnoreCase))
            {
                var users = await _db.Users.Select(u => new { u.Id, u.Username, u.DisplayName, u.IsOnline }).Take(50).ToListAsync();
                return Ok(new { data = new { users } });
            }

            return BadRequest(new { message = "Unsupported query template." });
        }
    }
}
