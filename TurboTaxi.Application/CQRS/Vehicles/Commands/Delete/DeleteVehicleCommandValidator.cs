using FluentValidation;

namespace TurboTaxi.Application.CQRS.Vehicles.Commands.Delete
{
    public sealed class DeleteVehicleCommandValidator : AbstractValidator<DeleteVehicleCommand>
    {
        public DeleteVehicleCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }
}
