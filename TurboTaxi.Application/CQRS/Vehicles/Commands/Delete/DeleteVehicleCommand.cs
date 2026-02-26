using MediatR;

namespace TurboTaxi.Application.CQRS.Vehicles.Commands.Delete
{
    public sealed record DeleteVehicleCommand(int Id) : IRequest<bool>;
}
