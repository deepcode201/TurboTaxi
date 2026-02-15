using MediatR;
using TurboTaxi.Models.Drivers;

namespace TurboTaxi.Application.CQRS.Drivers.Queries.GetAll
{
    public sealed record GetAllDriversQuery() : IRequest<IReadOnlyList<DriverDto>>;
}
