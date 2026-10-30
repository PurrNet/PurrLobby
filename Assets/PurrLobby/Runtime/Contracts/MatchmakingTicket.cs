namespace PurrNet.Lobby
{
    public struct MatchmakingTicket
    {
        public string ticketId;
        /// <summary>Private party invitation, returned only to the creating leader.</summary>
        public string inviteCode;
    }
}
