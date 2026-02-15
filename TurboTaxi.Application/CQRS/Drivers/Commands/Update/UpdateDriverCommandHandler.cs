using MediatR;
using TurboTaxi.Application.Interfaces;
using TurboTaxi.Models.Drivers;

namespace TurboTaxi.Application.CQRS.Drivers.Commands.Update
{
    public sealed class UpdateDriverCommandHandler : IRequestHandler<UpdateDriverCommand, DriverDto>
    {
        private readonly IDriverCrudService _service;
        public UpdateDriverCommandHandler(IDriverCrudService service) => _service = service;
        public Task<DriverDto> Handle(UpdateDriverCommand request, CancellationToken cancellationToken)
            => _service.UpdateAsync(request.Id, request.Request, cancellationToken);
    }
}
