using FluentValidation;

namespace TurboTaxi.Application.CQRS.Drivers.Commands.Create
{
    public sealed class CreateDriverCommandValidator : AbstractValidator<CreateDriverCommand>
    {
        public CreateDriverCommandValidator()
        {
            RuleFor(x => x.Request.UserId).GreaterThan(0);
        }
    }
}
