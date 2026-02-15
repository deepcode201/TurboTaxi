using MediatR;
using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.CQRS.Auth.Commands.Login;
using TurboTaxi.Application.CQRS.Auth.Commands.Register;
using TurboTaxi.Models.Auth;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IMediator _mediator;
        public AuthController(IMediator mediator) => _mediator = mediator;

        [HttpPost("register")]
        public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
        {
            var result = await _mediator.Send(RegisterCommand.FromDto(request));
            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
        {
            var result = await _mediator.Send(LoginCommand.FromDto(request));
            return Ok(result);
        }
    }
}
