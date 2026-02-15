using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.Application.CQRS.Vehicles.Commands.Update
{
    public sealed class UpdateVehicleCommandHandler : IRequestHandler<UpdateVehicleCommand, VehicleDto>
    {
        private readonly IVehicleCrudService _service;
        public UpdateVehicleCommandHandler(IVehicleCrudService service) => _service = service;
        public Task<VehicleDto> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
            => _service.UpdateAsync(request.Id, request.Request, cancellationToken);
    }
}
