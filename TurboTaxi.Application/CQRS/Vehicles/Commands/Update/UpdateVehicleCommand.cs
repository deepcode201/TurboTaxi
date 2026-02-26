using MediatR;
using TurboTaxi.Models.Vehicles;

namespace TurboTaxi.Application.CQRS.Vehicles.Commands.Update
{
    public sealed record UpdateVehicleCommand(int Id, VehicleUpdateRequest Request) : IRequest<VehicleDto>;
}
