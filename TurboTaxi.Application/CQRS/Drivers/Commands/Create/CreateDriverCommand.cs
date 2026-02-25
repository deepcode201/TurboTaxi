using MediatR;
using TurboTaxi.Models.Drivers;

namespace TurboTaxi.Application.CQRS.Drivers.Commands.Create
{
    public sealed record CreateDriverCommand(DriverCreateRequest Request) : IRequest<DriverDto>;
}
