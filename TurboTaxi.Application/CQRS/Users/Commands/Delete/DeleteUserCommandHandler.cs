using MediatR;
using TurboTaxi.Application.Interfaces;

namespace TurboTaxi.Application.CQRS.Users.Commands.Delete
{
    public sealed class DeleteUserCommandHandler : IRequestHandler<DeleteUserCommand, bool>
    {
        private readonly IUserCrudService _userCrudService;
        public DeleteUserCommandHandler(IUserCrudService userCrudService) => _userCrudService = userCrudService;
        public Task<bool> Handle(DeleteUserCommand request, CancellationToken cancellationToken)
            => _userCrudService.DeleteAsync(request.Id, cancellationToken);
    }
}
