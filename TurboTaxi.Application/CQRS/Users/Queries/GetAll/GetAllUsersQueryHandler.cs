using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Users;

namespace TurboTaxi.Application.CQRS.Users.Queries.GetAll
{
    public sealed class GetAllUsersQueryHandler : IRequestHandler<GetAllUsersQuery, IReadOnlyList<UserListItemDto>>
    {
        private readonly IUserCrudService _userCrudService;
        public GetAllUsersQueryHandler(IUserCrudService userCrudService) => _userCrudService = userCrudService;
        public Task<IReadOnlyList<UserListItemDto>> Handle(GetAllUsersQuery request, CancellationToken cancellationToken)
            => _userCrudService.GetAllAsync(cancellationToken);
    }
}
