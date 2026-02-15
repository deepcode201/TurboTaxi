using FluentValidation;

namespace TurboTaxi.Application.CQRS.Auth.Commands.Register
{
    public sealed class RegisterCommandValidator : AbstractValidator<RegisterCommand>
    {
        public RegisterCommandValidator()
        {
            RuleFor(x => x.FullName).NotEmpty().MaximumLength(150);
            RuleFor(x => x.Email).NotEmpty().EmailAddress();
            RuleFor(x => x.PhoneNumber).NotEmpty().MinimumLength(7).MaximumLength(20);
            RuleFor(x => x.Password).NotEmpty().MinimumLength(6);
        }
    }
}
