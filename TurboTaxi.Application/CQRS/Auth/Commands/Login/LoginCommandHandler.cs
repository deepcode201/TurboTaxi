using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Auth;

namespace TurboTaxi.Application.CQRS.Auth.Commands.Login
{
    public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResponse>
    {
        private readonly IAuthService _authService;
        public LoginCommandHandler(IAuthService authService) => _authService = authService;
        public Task<AuthResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var dto = new LoginRequest
            {
                Email = request.Email,
                Password = request.Password
            };
            return _authService.LoginAsync(dto, cancellationToken);
        }
    }
}
