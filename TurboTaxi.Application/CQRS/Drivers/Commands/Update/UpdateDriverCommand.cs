using MediatR;
using TurboTaxi.Models.Drivers;

namespace TurboTaxi.Application.CQRS.Drivers.Commands.Update
{
    public sealed record UpdateDriverCommand(int Id, DriverUpdateRequest Request) : IRequest<DriverDto>;
}
