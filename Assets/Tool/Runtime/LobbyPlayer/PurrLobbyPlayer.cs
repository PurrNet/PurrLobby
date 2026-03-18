using JetBrains.Annotations;
using PurrNet.Logging;
using UnityEngine;
#if PURR_VOICE
using PurrNet.Voice;
using PurrNet.Voice.LipSync;
#endif

namespace PurrNet.Lobby
{
    public class PurrLobbyPlayer : NetworkIdentity
    {
#if PURR_VOICE
        [SerializeField] PurrVoicePlayer _purrVoicePlayer;
        [SerializeField] private PurrLipSync _lipSync;
#endif

        public LobbyView lobbyView { get; private set; }
        public ILobby lobby { get; private set; }
        public IPlayer player { get; private set; }
        public PlayerEntry playerUIEntry { get; private set; }

#if PURR_VOICE
        private void Awake()
        {
            if (_purrVoicePlayer)
                _purrVoicePlayer.muted = true;
        }
#endif

        protected override void OnSpawned()
        {
#if PURR_VOICE
            if (!isOwner)
            {
                if (_purrVoicePlayer)
                    _purrVoicePlayer.muted = false;
            }
            else
            {
                if (_purrVoicePlayer)
                    _purrVoicePlayer.muted = !lobbyView.localMicEnabled;
                lobbyView.onLocalMicEnabledChanged += OnLocalMicEnabledChanged;
            }

            if (isOwner && _purrVoicePlayer && _lipSync)
                lobbyView.EnableMicrophoneFeature();
#endif
        }

#if PURR_VOICE
        private void OnLocalMicEnabledChanged(bool micEnabled)
        {
            _purrVoicePlayer.muted = !micEnabled;
        }
#endif

        [ObserversRpc(bufferLast: true, runLocally: true)]
        public void Setup(string playerId)
        {
            var nm = networkManager;
            var view = nm.GetComponentInParent<LobbyView>();

            if (!view)
            {
                PurrLogger.LogError($"LobbyPlayer `{playerId}` not found as a parent of the `NetworkManager`.");
                return;
            }

            lobbyView = view;
            lobby = lobbyView.lobby;

            if (lobby.TryGetPlayer(playerId, out var playerRef))
                player = playerRef;
            else PurrLogger.LogError($"Player `{playerId}` not found in lobby.");

            playerUIEntry = lobbyView.TryGetPlayerEntry(player, out var entry) ? entry : null;
        }

        private void OnEnable()
        {
#if PURR_VOICE
            if (_lipSync)
            {
                _lipSync.onPhonemeChanged += OnPhonemeChanged;
            }
#endif
        }

        private void OnDisable()
        {
#if PURR_VOICE
            if (_lipSync)
                _lipSync.onPhonemeChanged -= OnPhonemeChanged;
            if (lobbyView)
                lobbyView.onLocalMicEnabledChanged -= OnLocalMicEnabledChanged;
#endif
        }

#if PURR_VOICE
        [UsedImplicitly]
        private void OnPhonemeChanged(string phoneme)
        {
            if (playerUIEntry)
                playerUIEntry.PhonemeChanged(phoneme);
        }
#endif
    }
}
