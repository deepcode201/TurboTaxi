using MediatR;
using Microsoft.AspNetCore.Mvc;
using TurboTaxi.Application.CQRS.Vehicles.Commands.Create;
using TurboTaxi.Application.CQRS.Vehicles.Commands.Delete;
using TurboTaxi.Application.CQRS.Vehicles.Commands.Update;
using TurboTaxi.Application.CQRS.Vehicles.Queries.GetAll;
using TurboTaxi.Application.CQRS.Vehicles.Queries.GetById;
using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class VehiclesController : ControllerBase
    {
        private readonly IMediator _mediator;
        public VehiclesController(IMediator mediator) => _mediator = mediator;

        [HttpGet]
        public async Task<ActionResult<IReadOnlyList<VehicleDto>>> GetAll()
            => Ok(await _mediator.Send(new GetAllVehiclesQuery()));

        [HttpGet("{id:int}")]
        public async Task<ActionResult<VehicleDto>> GetById(int id)
            => Ok(await _mediator.Send(new GetVehicleByIdQuery(id)));

        [HttpPost]
        public async Task<ActionResult<VehicleDto>> Create([FromBody] VehicleCreateRequest request)
            => Ok(await _mediator.Send(new CreateVehicleCommand(request)));

        // Id only in body now
        [HttpPut]
        public async Task<ActionResult<VehicleDto>> Update([FromBody] VehicleUpdateRequest request)
            => Ok(await _mediator.Send(new UpdateVehicleCommand(request.Id, request)));

        [HttpDelete("{id:int}")]
        public async Task<ActionResult<bool>> Delete(int id)
            => Ok(await _mediator.Send(new DeleteVehicleCommand(id)));
    }
}
