using FluentValidation;

namespace TurboTaxi.Application.CQRS.Vehicles.Commands.Update
{
    public sealed class UpdateVehicleCommandValidator : AbstractValidator<UpdateVehicleCommand>
    {
        public UpdateVehicleCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
            RuleFor(x => x.Request.PlateNumber).NotEmpty().MaximumLength(20);
            RuleFor(x => x.Request.Brand).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Request.Model).NotEmpty().MaximumLength(50);
            RuleFor(x => x.Request.Color).NotEmpty().MaximumLength(30);
            RuleFor(x => x.Request.Year).InclusiveBetween(1980, DateTime.UtcNow.Year + 1);
            RuleFor(x => x.Request.VehicleType).NotEmpty();
        }
    }
}
