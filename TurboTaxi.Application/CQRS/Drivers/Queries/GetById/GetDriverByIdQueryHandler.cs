using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Drivers;

namespace TurboTaxi.Application.CQRS.Drivers.Queries.GetById
{
    public sealed class GetDriverByIdQueryHandler : IRequestHandler<GetDriverByIdQuery, DriverDto>
    {
        private readonly IDriverCrudService _service;
        public GetDriverByIdQueryHandler(IDriverCrudService service) => _service = service;
        public async Task<DriverDto> Handle(GetDriverByIdQuery request, CancellationToken cancellationToken)
        {
            var d = await _service.GetByIdAsync(request.Id, cancellationToken);
            if (d is null) throw new KeyNotFoundException("Driver not found");
            return d;
        }
    }
}
