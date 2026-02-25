using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Auth;

namespace TurboTaxi.Application.CQRS.Auth.Commands.Register
{
    public sealed class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResponse>
    {
        private readonly IAuthService _authService;
        public RegisterCommandHandler(IAuthService authService) => _authService = authService;
        public Task<AuthResponse> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var dto = new RegisterRequest
            {
                FullName = request.FullName,
                PhoneNumber = request.PhoneNumber,
                Email = request.Email,
                Password = request.Password
            };
            return _authService.RegisterAsync(dto, cancellationToken);
        }
    }
}
