#if NAKAMA
using System.Collections.Generic;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Metadata backed by Nakama match-state messages.
    /// Lobby metadata is host-authoritative; player metadata is owner-authoritative.
    /// </summary>
    public class NakamaMetadata : LobbyMetadataBase
    {
        private readonly NakamaLobby _lobby;
        private readonly string _ownerId;
        private readonly bool _isLocalOwner;

        internal NakamaMetadata(NakamaLobby lobby, string ownerId, bool isLocalOwner)
        {
            _lobby = lobby;
            _ownerId = ownerId;
            _isLocalOwner = isLocalOwner;
        }

        protected override bool CanWrite =>
            _ownerId == null ? _lobby.isOwner : _isLocalOwner;

        protected override void PushChange(string key, string value)
        {
            if (_ownerId == null)
                _lobby.SubmitLobbyMetadataPatch(new Dictionary<string, string> { { key, value } });
            else
                _lobby.BroadcastLocalPlayerMetadata(new Dictionary<string, string>(Snapshot()));
        }
    }
}
#endif
