using MediatR.Pipeline;

namespace TurboTaxi.Application.Behaviours
{
    public class RequestPreProcessorBehavior<TRequest> : IRequestPreProcessor<TRequest>
    {
        public Task Process(TRequest request, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
