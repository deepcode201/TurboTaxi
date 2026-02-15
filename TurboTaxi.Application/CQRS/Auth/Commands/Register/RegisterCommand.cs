using MediatR;
using TurboTaxi.Models.Auth;

namespace TurboTaxi.Application.CQRS.Auth.Commands.Register
{
    public sealed record RegisterCommand(string FullName, string PhoneNumber, string Email, string Password) : IRequest<AuthResponse>
    {
        public static RegisterCommand FromDto(RegisterRequest dto) => new(dto.FullName, dto.PhoneNumber, dto.Email, dto.Password);
    }
}
