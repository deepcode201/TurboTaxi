using MediatR;
using TurboTaxi.Models.Auth;

namespace TurboTaxi.Application.CQRS.Auth.Commands.Login
{
    public sealed record LoginCommand(string Email, string Password) : IRequest<AuthResponse>
    {
        public static LoginCommand FromDto(LoginRequest dto) => new(dto.Email, dto.Password);
    }
}
