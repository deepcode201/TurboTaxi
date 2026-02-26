using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.Application.CQRS.Vehicles.Queries.GetById
{
    public sealed class GetVehicleByIdQueryHandler : IRequestHandler<GetVehicleByIdQuery, VehicleDto>
    {
        private readonly IVehicleCrudService _service;
        public GetVehicleByIdQueryHandler(IVehicleCrudService service) => _service = service;
        public async Task<VehicleDto> Handle(GetVehicleByIdQuery request, CancellationToken cancellationToken)
        {
            var v = await _service.GetByIdAsync(request.Id, cancellationToken);
            if (v is null) throw new KeyNotFoundException("Vehicle not found");
            return v;
        }
    }
}
