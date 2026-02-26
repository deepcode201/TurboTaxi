using MediatR;
using TurboTaxi.Application.Interfaces;

namespace TurboTaxi.Application.CQRS.Drivers.Commands.Delete
{
    public sealed class DeleteDriverCommandHandler : IRequestHandler<DeleteDriverCommand, bool>
    {
        private readonly IDriverCrudService _service;
        public DeleteDriverCommandHandler(IDriverCrudService service) => _service = service;
        public Task<bool> Handle(DeleteDriverCommand request, CancellationToken cancellationToken)
            => _service.DeleteAsync(request.Id, cancellationToken);
    }
}
