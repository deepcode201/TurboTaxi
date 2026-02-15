using MediatR;
using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.Application.CQRS.Vehicles.Commands.Create
{
    public sealed record CreateVehicleCommand(VehicleCreateRequest Request) : IRequest<VehicleDto>;
}
