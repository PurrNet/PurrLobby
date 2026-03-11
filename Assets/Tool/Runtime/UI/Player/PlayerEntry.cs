using PurrLobby;
using PurrNet.UI;
using PurrNet.Utils;
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

        public void Setup(IPlayer localPlayer, IPlayer player)
        {
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

            if  (_player.avatar)
            {
                _avatarGraphic.texture = _player.avatar;
                _avatarLetter.enabled = false;
            }
            else
            {
                var playerHash = Hasher.Hash(_player.id);
                var playerRandomColor = Color.HSVToRGB(playerHash % 1000 / 1000f, 0.5f, 0.8f);
                _avatarGraphic.color = playerRandomColor;
                _avatarGraphic.texture = null;
                _avatarLetter.enabled = true;
                _avatarLetter.text = !string.IsNullOrEmpty(_player.displayName) ? _player.displayName[..1].ToUpper() : "?";
            }
        }
    }
}
