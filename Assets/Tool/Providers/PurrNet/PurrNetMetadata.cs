using System.Collections.Generic;
using PurrNet.Services;

namespace PurrNet.Lobby.PurrNet
{
    public class PurrNetMetadata : LobbyMetadataBase
    {
        private readonly LobbyService _service;
        private readonly string _lobbyId;
        private readonly bool _isPlayerMetadata;

        public PurrNetMetadata(LobbyService service, string lobbyId, bool isPlayerMetadata)
        {
            _service = service;
            _lobbyId = lobbyId;
            _isPlayerMetadata = isPlayerMetadata;
        }

        protected override void PushChange(string key, string value)
        {
            // The service API replicates full dictionaries, so push the whole snapshot.
            var full = new Dictionary<string, string>(Snapshot());
            (_isPlayerMetadata ?
                _service.SetPlayerMetadataAsync(_lobbyId, full) :
                _service.SetMetadataAsync(_lobbyId, full))
                .Forget("[PurrNetMetadata] Metadata update failed");
        }
    }
}
