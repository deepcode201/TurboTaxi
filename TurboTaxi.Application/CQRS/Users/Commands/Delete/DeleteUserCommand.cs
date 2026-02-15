using MediatR;

namespace TurboTaxi.Application.CQRS.Users.Commands.Delete
{
    public sealed record DeleteUserCommand(int Id) : IRequest<bool>;
}
