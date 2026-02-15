using MediatR;
using TurboTaxi.Models.Users;

namespace TurboTaxi.Application.CQRS.Users.Queries.GetAll
{
    public sealed record GetAllUsersQuery() : IRequest<IReadOnlyList<UserListItemDto>>;
}
