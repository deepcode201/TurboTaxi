using FluentValidation;

namespace TurboTaxi.Application.CQRS.Vehicles.Commands.Create
{
    public sealed class CreateVehicleCommandValidator : AbstractValidator<CreateVehicleCommand>
    {
        public CreateVehicleCommandValidator()
        {
            RuleFor(x => x.Request.PlateNumber).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Request.Brand).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Request.Model).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Request.Color).NotEmpty().MaximumLength(30);
            RuleFor(x => x.Request.Year).InclusiveBetween(1980, DateTime.UtcNow.Year + 1);
            RuleFor(x => x.Request.VehicleType).NotEmpty();
        }
    }
}
