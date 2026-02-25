using FluentValidation;

namespace TurboTaxi.Application.CQRS.Users.Commands.Delete
{
    public sealed class DeleteUserCommandValidator : AbstractValidator<DeleteUserCommand>
    {
        public DeleteUserCommandValidator()
        {
            RuleFor(x => x.Id).GreaterThan(0);
        }
    }
}
