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
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITokenService _tokens;

        public AuthController(AppDbContext db, ITokenService tokens)
        {
            _db = db;
            _tokens = tokens;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.Username) || req.Username.Length < 3)
                return BadRequest(new { message = "Username must be at least 3 characters." });

            if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 6)
                return BadRequest(new { message = "Password must be at least 6 characters." });

            if (await _db.Users.AnyAsync(u => u.PhoneNumber == req.PhoneNumber))
                return BadRequest(new { message = "Phone number is already in use." });

            // Allow duplicate username/display name, but require unique phone number.
            var normalizedPhone = req.PhoneNumber.Trim();

            var user = new User
            {
                Username = req.Username.Trim().ToLower(),
                PhoneNumber = normalizedPhone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
                DisplayName = req.DisplayName?.Trim() ?? req.Username,
                PublicKey = req.PublicKey
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();

            var accessToken = _tokens.GenerateAccessToken(user);
            var refreshToken = _tokens.GenerateRefreshToken();
            await _tokens.SaveRefreshTokenAsync(user.Id, refreshToken);

            return Ok(new AuthResponse(accessToken, refreshToken, MappingService.ToDto(user)));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            var normalizedPhone = req.PhoneNumber.Trim();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhone);
            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid phone number or password." });

            if (user.IsBanned)
                return Forbid("User is banned.");

            if (user.IsTwoFactorEnabled)
            {
                return Ok(new { twoFactorRequired = true, message = "Two-factor authentication code required." });
            }

            user.IsOnline = true;
            user.LastSeen = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var accessToken = _tokens.GenerateAccessToken(user);
            var refreshToken = _tokens.GenerateRefreshToken();
            await _tokens.SaveRefreshTokenAsync(user.Id, refreshToken);

            return Ok(new AuthResponse(accessToken, refreshToken, MappingService.ToDto(user)));
        }

        [HttpPost("login-2fa")]
        public async Task<IActionResult> LoginTwoFactor([FromBody] LoginTwoFactorRequest req)
        {
            var normalizedPhone = req.PhoneNumber.Trim();
            var user = await _db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == normalizedPhone);
            if (user == null || !BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash))
                return Unauthorized(new { message = "Invalid phone number, password, or code." });

            if (user.IsBanned)
                return Forbid("User is banned.");

            if (!user.IsTwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecret))
                return BadRequest(new { message = "Two-factor authentication is not enabled for this account." });

            if (!TwoFactorService.ValidateTotpCode(user.TwoFactorSecret, req.Code))
                return Unauthorized(new { message = "Invalid two-factor authentication code." });

            user.IsOnline = true;
            user.LastSeen = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            var accessToken = _tokens.GenerateAccessToken(user);
            var refreshToken = _tokens.GenerateRefreshToken();
            await _tokens.SaveRefreshTokenAsync(user.Id, refreshToken);

            return Ok(new AuthResponse(accessToken, refreshToken, MappingService.ToDto(user)));
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh([FromBody] RefreshRequest req)
        {
            var rt = await _tokens.GetValidRefreshTokenAsync(req.RefreshToken);
            if (rt == null)
                return Unauthorized(new { message = "Invalid or expired refresh token." });

            await _tokens.RevokeRefreshTokenAsync(req.RefreshToken);

            var newAccess = _tokens.GenerateAccessToken(rt.User);
            var newRefresh = _tokens.GenerateRefreshToken();
            await _tokens.SaveRefreshTokenAsync(rt.UserId, newRefresh);

            return Ok(new AuthResponse(newAccess, newRefresh, MappingService.ToDto(rt.User)));
        }

        [Authorize]
        [HttpPost("2fa/setup")]
        public async Task<IActionResult> SetupTwoFactor()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });

            var secret = TwoFactorService.GenerateSecret();
            user.TwoFactorSecret = secret;
            user.IsTwoFactorEnabled = false;
            await _db.SaveChangesAsync();

            var provisioningUri = TwoFactorService.GetProvisioningUri("ChatApp", user.Username, secret);
            return Ok(new TwoFactorSetupResponse(secret, provisioningUri));
        }

        [Authorize]
        [HttpPost("2fa/verify")]
        public async Task<IActionResult> VerifyTwoFactor([FromBody] TwoFactorVerifyRequest req)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });
            if (string.IsNullOrWhiteSpace(user.TwoFactorSecret)) return BadRequest(new { message = "Two-factor setup is required first." });

            if (!TwoFactorService.ValidateTotpCode(user.TwoFactorSecret, req.Code))
                return Unauthorized(new { message = "Invalid two-factor code." });

            user.IsTwoFactorEnabled = true;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Two-factor authentication enabled." });
        }

        [Authorize]
        [HttpPost("2fa/disable")]
        public async Task<IActionResult> DisableTwoFactor([FromBody] TwoFactorVerifyRequest req)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });
            if (!user.IsTwoFactorEnabled || string.IsNullOrWhiteSpace(user.TwoFactorSecret))
                return BadRequest(new { message = "Two-factor authentication is not enabled." });

            if (!TwoFactorService.ValidateTotpCode(user.TwoFactorSecret, req.Code))
                return Unauthorized(new { message = "Invalid two-factor code." });

            user.IsTwoFactorEnabled = false;
            user.TwoFactorSecret = null;
            await _db.SaveChangesAsync();

            return Ok(new { message = "Two-factor authentication disabled." });
        }

        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout([FromBody] RefreshRequest req)
        {
            await _tokens.RevokeRefreshTokenAsync(req.RefreshToken);

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _db.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsOnline = false;
                user.LastSeen = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }

            return Ok(new { message = "Logged out." });
        }

        [Authorize]
        [HttpPost("logout-all")]
        public async Task<IActionResult> LogoutAll()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await _tokens.RevokeAllUserTokensAsync(userId);
            return Ok(new { message = "All sessions revoked." });
        }
    }
}