using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Drivers;

namespace TurboTaxi.Application.CQRS.Drivers.Queries.GetAll
{
    public sealed class GetAllDriversQueryHandler : IRequestHandler<GetAllDriversQuery, IReadOnlyList<DriverDto>>
    {
        private readonly IDriverCrudService _service;
        public GetAllDriversQueryHandler(IDriverCrudService service) => _service = service;
        public Task<IReadOnlyList<DriverDto>> Handle(GetAllDriversQuery request, CancellationToken cancellationToken)
            => _service.GetAllAsync(cancellationToken);
    }
}
