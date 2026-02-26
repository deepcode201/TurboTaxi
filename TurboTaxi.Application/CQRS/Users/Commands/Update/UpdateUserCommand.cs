using MediatR;
using TurboTaxi.Models.Users;

namespace TurboTaxi.Application.CQRS.Users.Commands.Update
{
    public sealed record UpdateUserCommand(int Id, string Name, string Surname, string PhoneNumber, string Email) : IRequest<UserListItemDto>
    {
        public static UpdateUserCommand From(UserUpdateRequest r) => new(r.Id, r.Name, r.Surname, r.PhoneNumber, r.Email);
    }
}
