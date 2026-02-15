using FluentValidation;

namespace TurboTaxi.Application.CQRS.Drivers.Commands.Update
{
    public sealed class UpdateDriverCommandValidator : AbstractValidator<UpdateDriverCommand>
    {
        public UpdateDriverCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }
}
