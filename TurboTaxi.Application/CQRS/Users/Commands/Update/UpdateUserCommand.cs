using MediatR;
using TurboTaxi.Models.Users;

namespace TurboTaxi.Application.CQRS.Users.Commands.Update
{
    public sealed record UpdateUserCommand(int Id, string FullName, string PhoneNumber, string Email) : IRequest<UserListItemDto>
    {
        public static UpdateUserCommand From(UserUpdateRequest r) => new(r.Id, r.FullName, r.PhoneNumber, r.Email);
    }
}
