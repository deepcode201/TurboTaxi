namespace TurboTaxi.Domain.Enums
{
    /// <summary>
    /// Who sent the chat message. SenderId is User.Id or Driver.Id depending on this value.
    /// </summary>
    public enum ChatSenderRole
    {
        User = 0,
        Driver = 1
    }
}
