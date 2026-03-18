using System;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.UI;

namespace PurrNet.Lobby
{
#if PURR_VOICE
    [Serializable]
    public struct PhonemeEntry
    {
        public string phoneme;
        public Sprite sprite;
    }
#endif

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
        [SerializeField] private Color _statusReady = Color.green;
        [SerializeField] private Color _statusUnready = Color.gray;
        [SerializeField] private Image _phonemeSprite;
#if PURR_VOICE
        [Space]
        [SerializeField] private PhonemeEntry[] _phonemeEntries;
        [SerializeField] private Sprite _phonemeRest;
#endif

        private ILobby _lobby;
        private IPlayer _localPlayer => _lobby.localPlayer;
        private IPlayer _player;

        private Action<IPlayer>  _onKickPlayer;

        private void Awake()
        {
#if PURR_VOICE
            _phonemeSprite.color = new Color(1, 1, 1, 0);
#else
            _phonemeSprite.enabled = false;
#endif
        }

        public void Setup(ILobby lobby, IPlayer player, Action<IPlayer> onKick)
        {
            _onKickPlayer = onKick;
            _lobby = lobby;
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
            _status.color = _player.isReady ? _statusReady : _statusUnready;

            IPlayer.SetupAvatar(_player, _avatarGraphic, _avatarLetter);
        }

        private void Update()
        {
#if PURR_VOICE
            UpdatePhonemes();
#endif
        }

#if PURR_VOICE
        private string _lastPhoneme = "";
        private float _lastPhonemeChangedTime = -100f;

        private void UpdatePhonemes()
        {
            bool shouldHide = string.IsNullOrWhiteSpace(_lastPhoneme) && Time.time - _lastPhonemeChangedTime > 1f;
            Vector4 currentColor = _phonemeSprite.color;
            var targetColor = shouldHide ? new Vector4(1, 1, 1, 0) : new Vector4(1, 1, 1, 1);
            var newColor = Vector4.MoveTowards(currentColor, targetColor, Time.deltaTime * 5f);
            _phonemeSprite.color = newColor;
            _phonemeSprite.gameObject.SetActive(newColor.w != 0f);
        }

        public void PhonemeChanged(string phoneme)
        {
            if (phoneme != _lastPhoneme)
            {
                _lastPhoneme = phoneme;
                _lastPhonemeChangedTime = Time.time;
                OnPhonemeChanged();
            }
        }

        private void OnPhonemeChanged()
        {
            for (int i = 0; i < _phonemeEntries.Length; i++)
            {
                if (_phonemeEntries[i].phoneme == _lastPhoneme)
                {
                    _phonemeSprite.sprite = _phonemeEntries[i].sprite;
                    return;
                }
            }

            _phonemeSprite.sprite = _phonemeRest;
        }
#endif
    }
}
