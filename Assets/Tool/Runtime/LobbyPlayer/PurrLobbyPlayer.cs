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
                return;
            }

            // The buffered Setup RPC that assigns lobbyView can arrive after
            // OnSpawned on remote clients; wire from whichever lands last.
            WireLocalVoice();
#endif
        }

#if PURR_VOICE
        private bool _voiceWired;

        private void WireLocalVoice()
        {
            if (_voiceWired || !isOwner || !lobbyView)
                return;

            _voiceWired = true;

            if (_purrVoicePlayer)
                _purrVoicePlayer.muted = !lobbyView.localMicEnabled;
            lobbyView.onLocalMicEnabledChanged += OnLocalMicEnabledChanged;

            if (_purrVoicePlayer && _lipSync)
                lobbyView.EnableMicrophoneFeature();
        }

        private void OnLocalMicEnabledChanged(bool micEnabled)
        {
            if (_purrVoicePlayer)
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

#if PURR_VOICE
            WireLocalVoice();
#endif
        }

        private void OnEnable()
        {
#if PURR_VOICE
            if (_lipSync)
            {
                _lipSync.onPhonemeChanged += OnPhonemeChanged;
            }
            WireLocalVoice();
#endif
        }

        private void OnDisable()
        {
#if PURR_VOICE
            if (_lipSync)
                _lipSync.onPhonemeChanged -= OnPhonemeChanged;
            if (_voiceWired && lobbyView)
            {
                lobbyView.onLocalMicEnabledChanged -= OnLocalMicEnabledChanged;
                _voiceWired = false;
            }
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
