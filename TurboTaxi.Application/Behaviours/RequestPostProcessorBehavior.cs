using MediatR.Pipeline;

namespace TurboTaxi.Application.Behaviours
{
    public class RequestPostProcessorBehavior<TRequest, TResponse> : IRequestPostProcessor<TRequest, TResponse>
    {
        public Task Process(TRequest request, TResponse response, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
