using MediatR;
using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.Application.CQRS.Vehicles.Queries.GetAll
{
    public sealed record GetAllVehiclesQuery() : IRequest<IReadOnlyList<VehicleDto>>;
}
