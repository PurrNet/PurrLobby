#if NAKAMA
namespace PurrNet.Lobby.Nakama
{
    public class NakamaPlayer : LobbyPlayerBase
    {
        public override IMetadata userData => _metadata;

        private readonly NakamaMetadata _metadata;

        internal NakamaPlayer(NakamaLobby lobby, string userId, string displayName, bool isHost, bool isLocal)
            : base(userId, displayName, isHost)
        {
            _metadata = new NakamaMetadata(lobby, userId, isLocal);
        }

        internal NakamaMetadata GetMetadata() => _metadata;
    }
}
#endif
