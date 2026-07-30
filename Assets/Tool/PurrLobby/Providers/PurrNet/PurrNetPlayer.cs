using PurrNet.Services;

namespace PurrNet.Lobby.PurrNet
{
    public class PurrNetPlayer : LobbyPlayerBase
    {
        public override IMetadata userData => _metadata;

        private readonly PurrNetMetadata _metadata;

        public PurrNetPlayer(LobbyService service, string lobbyId)
            : base(id: null, displayName: null, isOwner: false)
        {
            _metadata = new PurrNetMetadata(service, lobbyId, true);
        }

        internal PurrNetMetadata GetMetadata() => _metadata;

        /// <summary>Syncs identity from a service snapshot. Returns true if anything changed. Ownership is handled by the lobby.</summary>
        internal bool Update(LobbyPlayer player)
        {
            bool changed = false;

            if (player.id != id)
            {
                id = player.id;
                changed = true;
            }

            if (player.displayName != displayName)
            {
                displayName = player.displayName;
                changed = true;
            }

            return changed;
        }
    }
}
