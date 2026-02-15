using FluentValidation;

namespace TurboTaxi.Application.CQRS.Drivers.Commands.Delete
{
    public sealed class DeleteDriverCommandValidator : AbstractValidator<DeleteDriverCommand>
    {
        public DeleteDriverCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }
}
