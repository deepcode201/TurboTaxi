using MediatR;
using TurboTaxi.Application.Interfaces;

namespace TurboTaxi.Application.CQRS.Vehicles.Commands.Delete
{
    public sealed class DeleteVehicleCommandHandler : IRequestHandler<DeleteVehicleCommand, bool>
    {
        private readonly IVehicleCrudService _service;
        public DeleteVehicleCommandHandler(IVehicleCrudService service) => _service = service;
        public Task<bool> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
            => _service.DeleteAsync(request.Id, cancellationToken);
    }
}
