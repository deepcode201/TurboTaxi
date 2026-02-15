using MediatR;
using TurboTaxi.Models.Users;

namespace TurboTaxi.Application.CQRS.Users.Queries.GetById
{
    public sealed record GetUserByIdQuery(int Id) : IRequest<UserListItemDto>;
}
