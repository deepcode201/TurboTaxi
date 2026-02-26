using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.Application.CQRS.Vehicles.Commands.Create
{
    public sealed class CreateVehicleCommandHandler : IRequestHandler<CreateVehicleCommand, VehicleDto>
    {
        private readonly IVehicleCrudService _service;
        public CreateVehicleCommandHandler(IVehicleCrudService service) => _service = service;
        public Task<VehicleDto> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
            => _service.CreateAsync(request.Request, cancellationToken);
    }
}
