using Microsoft.EntityFrameworkCore;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Domain.Entities;
using TurboTaxi.Infrastructure.Data;
using TurboTaxi.Models.Auth;

namespace TurboTaxi.Infrastructure.Services
{
    public class AuthService : IAuthService
    {
        private readonly ApplicationDbContext _db;
        private readonly IJWTService _jwt;
        public AuthService(ApplicationDbContext db, IJWTService jwt)
        {
            _db = db;
            _jwt = jwt;
        }
        public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
        {
            var exists = await _db.Users.AnyAsync(u => u.Email == request.Email, ct);
            if (exists)
            {
                return new AuthResponse { Success = false, Message = "Email already registered." };
            }
            var user = new User
            {
                FullName = request.FullName,
                Email = request.Email,
                PhoneNumber = request.PhoneNumber,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };
            _db.Users.Add(user);
            await _db.SaveChangesAsync(ct);

            return new AuthResponse
            {
                Success = true,
                Message = "Registration successful.",
                Data = new AuthResponseData
                {
                    User = new UserDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        PhoneNumber = user.PhoneNumber,
                        Email = user.Email,
                        CreatedAt = user.CreatedTime
                    },
                    Tokens = null! // tokens are not returned on registration
                }
            };
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct);
            if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                return new AuthResponse { Success = false, Message = "Invalid credentials." };
            }
            return await BuildAuthResponseAsync(user, "Login successful.", ct);
        }

        private Task<AuthResponse> BuildAuthResponseAsync(User user, string message, CancellationToken ct)
        {
            var accessToken = _jwt.GenerateJwtToken(user);
            var refreshToken = Guid.NewGuid().ToString("N");
            var response = new AuthResponse
            {
                Success = true,
                Message = message,
                Data = new AuthResponseData
                {
                    User = new UserDto
                    {
                        Id = user.Id,
                        FullName = user.FullName,
                        PhoneNumber = user.PhoneNumber,
                        Email = user.Email,
                        CreatedAt = user.CreatedTime
                    },
                    Tokens = new AuthTokensDto
                    {
                        AccessToken = accessToken,
                        RefreshToken = refreshToken,
                        ExpiresIn = 3600,
                        TokenType = "Bearer"
                    }
                }
            };
            return Task.FromResult(response);
        }
    }
}
