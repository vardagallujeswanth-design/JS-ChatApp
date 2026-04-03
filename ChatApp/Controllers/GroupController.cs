using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ChatApp.Data;
using ChatApp.DTOs;
using ChatApp.Models;

namespace ChatApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GroupsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IWebHostEnvironment _env;

        public GroupsController(AppDbContext db, IWebHostEnvironment env)
        {
            _db = db;
            _env = env;
        }

        private int CurrentUserId => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        [HttpPost]
        public async Task<IActionResult> CreateGroup(CreateGroupRequest req)
        {
            var group = new Group
            {
                Name = req.Name,
                Description = req.Description,
                CreatedById = CurrentUserId
            };
            _db.Groups.Add(group);
            await _db.SaveChangesAsync();

            // Add creator as admin
            _db.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = CurrentUserId, IsAdmin = true });

            // Add other members
            foreach (var memberId in req.MemberIds.Distinct().Where(id => id != CurrentUserId))
                _db.GroupMembers.Add(new GroupMember { GroupId = group.Id, UserId = memberId });

            await _db.SaveChangesAsync();

            return Ok(await GetGroupDtoAsync(group.Id));
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetGroup(int id)
        {
            var isMember = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == id && gm.UserId == CurrentUserId);
            if (!isMember) return Forbid();

            return Ok(await GetGroupDtoAsync(id));
        }

        [HttpPost("{id}/members")]
        public async Task<IActionResult> AddMember(int id, [FromBody] int userId)
        {
            var isAdmin = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == id && gm.UserId == CurrentUserId && gm.IsAdmin);
            if (!isAdmin) return Forbid();

            if (await _db.GroupMembers.AnyAsync(gm => gm.GroupId == id && gm.UserId == userId))
                return BadRequest(new { message = "User is already a member." });

            _db.GroupMembers.Add(new GroupMember { GroupId = id, UserId = userId });
            await _db.SaveChangesAsync();

            return Ok(await GetGroupDtoAsync(id));
        }

        [HttpDelete("{id}/members/{userId}")]
        public async Task<IActionResult> RemoveMember(int id, int userId)
        {
            var isAdmin = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == id && gm.UserId == CurrentUserId && gm.IsAdmin);
            if (!isAdmin && CurrentUserId != userId) return Forbid();

            var member = await _db.GroupMembers.FirstOrDefaultAsync(gm => gm.GroupId == id && gm.UserId == userId);
            if (member == null) return NotFound();

            _db.GroupMembers.Remove(member);
            await _db.SaveChangesAsync();

            return NoContent();
        }

        [HttpPost("{id}/avatar")]
        public async Task<IActionResult> UploadAvatar(int id, IFormFile file)
        {
            var isAdmin = await _db.GroupMembers.AnyAsync(gm => gm.GroupId == id && gm.UserId == CurrentUserId && gm.IsAdmin);
            if (!isAdmin) return Forbid();

            var group = await _db.Groups.FindAsync(id);
            if (group == null) return NotFound();

            var uploadsPath = Path.Combine(_env.WebRootPath, "uploads", "avatars");
            Directory.CreateDirectory(uploadsPath);

            var fileName = $"group_{id}_{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(uploadsPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
                await file.CopyToAsync(stream);

            group.AvatarUrl = $"/uploads/avatars/{fileName}";
            await _db.SaveChangesAsync();

            return Ok(new { avatarUrl = group.AvatarUrl });
        }

        private async Task<GroupDto> GetGroupDtoAsync(int groupId)
        {
            var group = await _db.Groups
                .Include(g => g.Members).ThenInclude(gm => gm.User)
                .FirstAsync(g => g.Id == groupId);

            return new GroupDto(
                group.Id, group.Name, group.Description, group.AvatarUrl,
                group.CreatedById, group.CreatedAt,
                group.Members.Select(gm => new GroupMemberDto(
                    gm.UserId, gm.User.Username, gm.User.DisplayName ?? gm.User.Username,
                    gm.User.AvatarUrl, gm.IsAdmin ? "true" : "false"
                )).ToList()
            );
        }
    }
}