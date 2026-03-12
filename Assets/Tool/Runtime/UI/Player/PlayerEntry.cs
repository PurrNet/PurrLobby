using System;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class PlayerEntry : MonoBehaviour
    {
        [SerializeField] private RectangleGraphic _graphic;
        [SerializeField] private RectangleGraphic _hostIndicator;
        [SerializeField] private RectangleGraphic _avatarGraphic;
        [SerializeField] private SelectedOutline _outline;
        [SerializeField] private TMPro.TMP_Text _avatarLetter;
        [SerializeField] private TMPro.TMP_Text _username;
        [SerializeField] private TMPro.TMP_Text _status;
        [SerializeField] private GameObject _options;

        private IPlayer _localPlayer;
        private IPlayer _player;

        private Action<IPlayer>  _onKickPlayer;

        public void Setup(IPlayer localPlayer, IPlayer player, Action<IPlayer> onKick)
        {
            _onKickPlayer  = onKick;
            _localPlayer = localPlayer;
            _player = player;
            UpdatePlayerInfo();

            if (_localPlayer != null)
            {
                _localPlayer.onPlayerUpdated += UpdatePlayerInfo;
                _localPlayer.onPlayerMetadataUpdated += UpdatePlayerInfo;
            }

            player.onPlayerUpdated += UpdatePlayerInfo;
            player.onPlayerMetadataUpdated += UpdatePlayerInfo;
        }

        public void Kick()
        {
            if (_player != null)
                _onKickPlayer?.Invoke(_player);
        }

        private void OnDisable()
        {
            if (_player != null)
            {
                _player.onPlayerUpdated -= UpdatePlayerInfo;
                _player.onPlayerMetadataUpdated -= UpdatePlayerInfo;
            }

            if (_localPlayer != null)
            {
                _localPlayer.onPlayerUpdated -= UpdatePlayerInfo;
                _localPlayer.onPlayerMetadataUpdated -= UpdatePlayerInfo;
            }
        }

        private void UpdatePlayerInfo()
        {
            _hostIndicator.enabled = _player.isHost;
            _username.text = _player.displayName;

            bool iAmHost = _localPlayer?.isHost == true;
            bool isMe = _localPlayer?.id == _player.id;

            _outline.outlineWidthNotSelected = isMe ? 1f : 0f;
            _options.SetActive(iAmHost && !_player.isHost);

            _status.text = _player.isReady ? "Ready" : "Not Ready";

            IPlayer.SetupAvatar(_player, _avatarGraphic, _avatarLetter);
        }
    }
}
