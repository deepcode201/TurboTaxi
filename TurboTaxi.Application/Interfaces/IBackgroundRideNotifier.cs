namespace TurboTaxi.Application.Interfaces
{
    /// <summary>Fire-and-forget work (e.g. SignalR) so HTTP/hub handlers return immediately.</summary>
    public interface IBackgroundRideNotifier
    {
        void Run(Func<IServiceProvider, CancellationToken, Task> work);
    }
}
