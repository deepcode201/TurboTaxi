using MediatR;
using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.CQRS.Drivers.Commands.Create;
using TurboTaxi.Application.CQRS.Drivers.Commands.Delete;
using TurboTaxi.Application.CQRS.Drivers.Commands.Update;
using TurboTaxi.Application.CQRS.Drivers.Queries.GetAll;
using TurboTaxi.Application.CQRS.Drivers.Queries.GetById;
using TurboTaxi.Models.Drivers;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DriversController : ControllerBase
    {
        private readonly IMediator _mediator;
        public DriversController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<DriverDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllDriversQuery()));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<DriverDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetDriverByIdQuery(id)));

        [HttpPost]
        public async Task<ActionResult<DriverDto>> Create([FromBody] DriverCreateRequest request)
            => Ok(await _mediator.Send(new CreateDriverCommand(request)));

        [HttpPut("{id:int}")]
        public async Task<ActionResult<DriverDto>> Update(int id, [FromBody] DriverUpdateRequest request)
            => Ok(await _mediator.Send(new UpdateDriverCommand(id, request)));

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<bool>> Delete(int id)
            => Ok(await _mediator.Send(new DeleteDriverCommand(id)));
    }
}
