using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ChatApp.Data;
using ChatApp.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApp.Services
{
    public interface ITokenService
    {
        string GenerateAccessToken(User user);
        string GenerateRefreshToken();
        Task<RefreshToken> SaveRefreshTokenAsync(int userId, string token);
        Task<RefreshToken?> GetValidRefreshTokenAsync(string token);
        Task RevokeRefreshTokenAsync(string token);
        Task RevokeAllUserTokensAsync(int userId);
    }

    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;
        private readonly AppDbContext _db;

        public TokenService(IConfiguration config, AppDbContext db)
        {
            _config = config;
            _db = db;
        }

        public string GenerateAccessToken(User user)
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.MobilePhone, user.PhoneNumber ?? ""),
                new Claim("displayName", user.DisplayName),
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(int.Parse(_config["Jwt:AccessTokenMinutes"] ?? "60")),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public string GenerateRefreshToken()
        {
            var bytes = new byte[64];
            RandomNumberGenerator.Fill(bytes);
            return Convert.ToBase64String(bytes);
        }

        public async Task<RefreshToken> SaveRefreshTokenAsync(int userId, string token)
        {
            var rt = new RefreshToken
            {
                UserId = userId,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_config["Jwt:RefreshTokenDays"] ?? "30"))
            };
            _db.RefreshTokens.Add(rt);
            await _db.SaveChangesAsync();
            return rt;
        }

        public async Task<RefreshToken?> GetValidRefreshTokenAsync(string token)
        {
            return await _db.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt =>
                    rt.Token == token &&
                    !rt.IsRevoked &&
                    rt.ExpiresAt > DateTime.UtcNow);
        }

        public async Task RevokeRefreshTokenAsync(string token)
        {
            var rt = await _db.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token);
            if (rt != null)
            {
                rt.IsRevoked = true;
                await _db.SaveChangesAsync();
            }
        }

        public async Task RevokeAllUserTokensAsync(int userId)
        {
            var tokens = _db.RefreshTokens.Where(r => r.UserId == userId && !r.IsRevoked);
            await tokens.ForEachAsync(r => r.IsRevoked = true);
            await _db.SaveChangesAsync();
        }
    }
}