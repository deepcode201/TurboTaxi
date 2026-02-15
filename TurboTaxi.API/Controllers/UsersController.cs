using MediatR;
using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.CQRS.Users.Commands.Delete;
using TurboTaxi.Application.CQRS.Users.Commands.Update;
using TurboTaxi.Application.CQRS.Users.Queries.GetAll;
using TurboTaxi.Application.CQRS.Users.Queries.GetById;
using TurboTaxi.Models.Users;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UsersController : ControllerBase
    {
        private readonly IMediator _mediator;
        public UsersController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllUsersQuery()));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<UserListItemDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetUserByIdQuery(id)));

        [HttpPut]
        public async Task<ActionResult<UserListItemDto>> Update([FromBody] UserUpdateRequest request)
            => Ok(await _mediator.Send(UpdateUserCommand.From(request)));

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<bool>> Delete(int id)
            => Ok(await _mediator.Send(new DeleteUserCommand(id)));
    }
}
