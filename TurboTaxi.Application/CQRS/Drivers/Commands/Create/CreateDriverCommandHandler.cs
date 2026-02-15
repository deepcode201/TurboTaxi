using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Drivers;

namespace TurboTaxi.Application.CQRS.Drivers.Commands.Create
{
    public sealed class CreateDriverCommandHandler : IRequestHandler<CreateDriverCommand, DriverDto>
    {
        private readonly IDriverCrudService _service;
        public CreateDriverCommandHandler(IDriverCrudService service) => _service = service;
        public Task<DriverDto> Handle(CreateDriverCommand request, CancellationToken cancellationToken)
            => _service.CreateAsync(request.Request, cancellationToken);
    }
}
