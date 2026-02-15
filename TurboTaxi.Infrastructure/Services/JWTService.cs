using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Entities;

namespace TurboTaxi.Infrastructure.Services
{
    public class JWTService : IJWTService
    {
        private readonly IConfiguration _configuration;
        public JWTService(IConfiguration configuration) => _configuration = configuration;

        public string GenerateJwtToken(User user)
        {
            var issuer = _configuration["JWTSettings:Issuer"];
            var audience = _configuration["JWTSettings:Audience"];
            var keyString = _configuration["JWTSettings:Key"] ?? throw new InvalidOperationException("JWT key missing");

            byte[] originalKeyBytes = keyString.StartsWith("base64:")
                ? Convert.FromBase64String(keyString.Substring(7))
                : Encoding.UTF8.GetBytes(keyString);

            // Ensure minimum length: if shorter than 32 bytes, hash to 32 bytes (HS256 requirement)
            byte[] keyBytes = originalKeyBytes.Length < 32 ? SHA256.HashData(originalKeyBytes) : originalKeyBytes;

            string algorithm = keyBytes.Length >= 64
                ? SecurityAlgorithms.HmacSha512
                : keyBytes.Length >= 48
                    ? SecurityAlgorithms.HmacSha384
                    : SecurityAlgorithms.HmacSha256;

            var credentials = new SigningCredentials(new SymmetricSecurityKey(keyBytes), algorithm);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
            };

            var descriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Issuer = issuer,
                Audience = audience,
                Expires = DateTime.UtcNow.AddHours(Convert.ToInt32(_configuration["JWTSettings:Expiration"] ?? "1")),
                SigningCredentials = credentials
            };

            var handler = new JwtSecurityTokenHandler();
            var token = handler.CreateToken(descriptor);
            return handler.WriteToken(token);
        }

        public string ValidateJwtToken(string token)
        {
            var keyString = _configuration["JWTSettings:Key"] ?? string.Empty;
            byte[] originalKeyBytes = keyString.StartsWith("base64:")
                ? Convert.FromBase64String(keyString.Substring(7))
                : Encoding.UTF8.GetBytes(keyString);
            byte[] keyBytes = originalKeyBytes.Length < 32 ? SHA256.HashData(originalKeyBytes) : originalKeyBytes;

            var handler = new JwtSecurityTokenHandler();
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken _);

            return token;
        }
    }
}
