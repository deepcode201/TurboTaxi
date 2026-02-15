using MediatR;
using TurboTaxi.Models.Drivers;

namespace TurboTaxi.Application.CQRS.Drivers.Queries.GetById
{
    public sealed record GetDriverByIdQuery(int Id) : IRequest<DriverDto>;
}
