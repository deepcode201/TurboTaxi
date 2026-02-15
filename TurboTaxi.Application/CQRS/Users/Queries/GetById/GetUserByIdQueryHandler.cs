using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Users;

namespace TurboTaxi.Application.CQRS.Users.Queries.GetById
{
    public sealed class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserListItemDto>
    {
        private readonly IUserCrudService _userCrudService;
        public GetUserByIdQueryHandler(IUserCrudService userCrudService) => _userCrudService = userCrudService;
        public async Task<UserListItemDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
        {
            var user = await _userCrudService.GetByIdAsync(request.Id, cancellationToken);
            if (user is null) throw new KeyNotFoundException("User not found");
            return user;
        }
    }
}
