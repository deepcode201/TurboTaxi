using MediatR;

namespace TurboTaxi.Application.CQRS.Drivers.Commands.Delete
{
    public sealed record DeleteDriverCommand(int Id) : IRequest<bool>;
}
