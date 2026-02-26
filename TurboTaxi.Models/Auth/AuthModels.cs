namespace TurboTaxi.Models.Auth
{
    public class RegisterRequest
    {
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class LoginRequest
    {
        public string Email { get; set; } = null!;
        public string Password { get; set; } = null!;
    }

    public class AuthTokensDto
    {
        public string AccessToken { get; set; } = null!;
        public string RefreshToken { get; set; } = null!;
        public int ExpiresIn { get; set; }
        public string TokenType { get; set; } = "Bearer";
    }

    public class UserDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Surname { get; set; } = null!;
        public string PhoneNumber { get; set; } = null!;
        public string Email { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
    }

    public class AuthResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = null!;
        public AuthResponseData Data { get; set; } = null!;
    }

    public class AuthResponseData
    {
        public UserDto User { get; set; } = null!;
        public AuthTokensDto Tokens { get; set; } = null!;
    }
}
