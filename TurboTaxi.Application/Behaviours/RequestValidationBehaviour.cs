using System.Net;
using FluentValidation;
using MediatR;
using TurboTaxi.Application.Core;

namespace TurboTaxi.Application.Behaviours
{
    public class RequestValidationBehaviour<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    {
        private readonly IEnumerable<IValidator<TRequest>> _validators;
        public RequestValidationBehaviour(IEnumerable<IValidator<TRequest>> validators)
        {
            _validators = validators;
        }

        public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken cancellationToken)
        {
            var context = new ValidationContext<TRequest>(request);

            var failures = _validators.Select(x => x.Validate(context))
                                 .SelectMany(x => x.Errors)
                                 .Where(x => x is not null)
                                 .ToList();

            Dictionary<string, string> errors = new();

            foreach (var error in failures)
            {
                errors.TryAdd(TakePropName(error.PropertyName), error.ErrorMessage);
            }

            if (failures.Count > 0)
            {
                return await Task.FromResult(
                    (TResponse)
                    typeof(TResponse)
                    .GetMethod(
                        "Error",
                        new[] { typeof(ErrorCodes), typeof(Dictionary<string, string>), typeof(int) })
                    !.Invoke(
                        Activator.CreateInstance(typeof(TResponse))!,
                        new object[] { ErrorCodes.VALIDATION_ERROR, errors, (int)HttpStatusCode.BadRequest })!);
            }
            return await next();
        }


        private static string? TakePropName(string fullPropName)
        {
            return fullPropName.Split('.').LastOrDefault();
        }
    }
}
