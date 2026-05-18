namespace TurboTaxi.Application.Interfaces
{
    /// <summary>
    /// Validates that a user or driver may access a ride's chat (assigned driver must exist).
    /// </summary>
    public interface IRideChatAccess
    {
        Task<bool> CanAccessAsync(int rideId, string actorRole, int actorId, CancellationToken cancellationToken = default);
    }
}
