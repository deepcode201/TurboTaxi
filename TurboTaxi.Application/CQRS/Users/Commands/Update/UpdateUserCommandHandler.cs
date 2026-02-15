using MediatR;
using TurboTaxi.Models.Users;
using TurboTaxi.Application.Interfaces;

namespace TurboTaxi.Application.CQRS.Users.Commands.Update
{
    public sealed class UpdateUserCommandHandler : IRequestHandler<UpdateUserCommand, UserListItemDto>
    {
        private readonly IUserCrudService _userCrudService;
        public UpdateUserCommandHandler(IUserCrudService userCrudService) => _userCrudService = userCrudService;
        public Task<UserListItemDto> Handle(UpdateUserCommand request, CancellationToken cancellationToken)
            => _userCrudService.UpdateAsync(request.Id, request.FullName, request.PhoneNumber, request.Email, cancellationToken);
    }
}
