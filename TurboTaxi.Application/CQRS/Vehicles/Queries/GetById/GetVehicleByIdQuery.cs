using MediatR;
using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.Application.CQRS.Vehicles.Queries.GetById
{
    public sealed record GetVehicleByIdQuery(int Id) : IRequest<VehicleDto>;
}
