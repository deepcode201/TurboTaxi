using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.Application.CQRS.Vehicles.Queries.GetAll
{
    public sealed class GetAllVehiclesQueryHandler : IRequestHandler<GetAllVehiclesQuery, IReadOnlyList<VehicleDto>>
    {
        private readonly IVehicleCrudService _service;
        public GetAllVehiclesQueryHandler(IVehicleCrudService service) => _service = service;
        public Task<IReadOnlyList<VehicleDto>> Handle(GetAllVehiclesQuery request, CancellationToken cancellationToken)
            => _service.GetAllAsync(cancellationToken);
    }
}
