namespace PurrNet.Lobby
{
    public enum MatchmakingStatus
    {
        Queued,
        Searching,
        Found,
        Cancelled,
        Failed,
        Assembling,
        ReadyCheck,
        Allocating
    }
}
