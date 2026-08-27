using System;
using System.Collections.Generic;
using PurrNet.UI;
using UnityEngine;
using UnityEngine.EventSystems;
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

    public class PlayerEntry : MonoBehaviour, IPointerClickHandler
    {
        /// <summary>
        /// Raised while building a player's context menu (right-click or
        /// <see cref="OpenContextMenu()"/>). Games append their own actions —
        /// mute, view profile, etc. — without touching the entry prefab.
        /// </summary>
        public static event Action<ILobby, IPlayer, List<PlayerContextAction>> onBuildContextActions;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => onBuildContextActions = null;

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
        private IPlayer _localPlayer => _lobby?.localPlayer;
        private IPlayer _player;

        // The exact references we subscribed to, so we can unsubscribe even if
        // the lobby's localPlayer changes between Setup and teardown.
        private IPlayer _subscribedLocal;
        private IPlayer _subscribedPlayer;

        private Action<IPlayer>  _onKickPlayer;

        private void Awake()
        {
            // Kicking is exposed through the player context menu. Keep the old
            // prefab reference disabled so existing prefab variants cannot bring
            // the direct kick button back.
            if (_options)
                _options.SetActive(false);

#if PURR_VOICE
            _phonemeSprite.color = new Color(1, 1, 1, 0);
#else
            _phonemeSprite.enabled = false;
#endif
        }

        public void Setup(ILobby lobby, IPlayer player, Action<IPlayer> onKick)
        {
            Unsubscribe();

            _onKickPlayer = onKick;
            _lobby = lobby;
            _player = player;
            UpdatePlayerInfo();

            var local = _localPlayer;
            if (local != null && !ReferenceEquals(local, player))
            {
                _subscribedLocal = local;
                local.onPlayerUpdated += UpdatePlayerInfo;
                local.onPlayerMetadataUpdated += UpdatePlayerInfo;
            }

            _subscribedPlayer = player;
            player.onPlayerUpdated += UpdatePlayerInfo;
            player.onPlayerMetadataUpdated += UpdatePlayerInfo;
        }

        public void Kick()
        {
            if (_player != null)
                _onKickPlayer?.Invoke(_player);
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
                OpenContextMenu(eventData.position);
        }

        /// <summary>Opens the player context menu at this entry. UnityEvent-friendly (e.g. a "…" button).</summary>
        public void OpenContextMenu()
        {
            OpenContextMenu(RectTransformUtility.WorldToScreenPoint(null, transform.position));
        }

        private void OpenContextMenu(Vector2 screenPos)
        {
            if (_player == null || _lobby == null)
                return;

            var actions = new List<PlayerContextAction>();

            bool iAmHost = _localPlayer?.isOwner == true;
            bool isMe = _localPlayer?.id == _player.id;

            if (iAmHost && !isMe && !_player.isOwner)
            {
                actions.Add(new PlayerContextAction
                {
                    option = new ContextOption
                    {
                        name = "Kick",
                        description = $"Remove {_player.displayName} from the lobby",
                        isDangerous = true
                    },
                    action = (_, player) => _onKickPlayer?.Invoke(player)
                });
            }

            onBuildContextActions?.Invoke(_lobby, _player, actions);

            if (actions.Count == 0)
                return;

            var parentView = GetComponentInParent<MonoView>();
            if (!parentView || !parentView.parentStack)
                return;

            var stack = parentView.parentStack;

            var options = new ContextOption[actions.Count];
            for (int i = 0; i < actions.Count; i++)
                options[i] = actions[i].option;

            var lobby = _lobby;
            var player = _player;
            stack.Push<ContextMenuView>().Setup(screenPos, _player.displayName, options,
                index => actions[index].action?.Invoke(lobby, player));
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Unsubscribe()
        {
            if (_subscribedPlayer != null)
            {
                _subscribedPlayer.onPlayerUpdated -= UpdatePlayerInfo;
                _subscribedPlayer.onPlayerMetadataUpdated -= UpdatePlayerInfo;
                _subscribedPlayer = null;
            }

            if (_subscribedLocal != null)
            {
                _subscribedLocal.onPlayerUpdated -= UpdatePlayerInfo;
                _subscribedLocal.onPlayerMetadataUpdated -= UpdatePlayerInfo;
                _subscribedLocal = null;
            }
        }

        private void UpdatePlayerInfo()
        {
            _hostIndicator.enabled = _player.isOwner;
            _username.text = _player.displayName;

            bool isMe = _localPlayer?.id == _player.id;

            _outline.outlineWidthNotSelected = isMe ? 1f : 0f;

            _status.text = _player.isReady ? "Ready" : "Not Ready";
            _status.color = _player.isReady ? _statusReady : _statusUnready;

            PlayerAvatarUI.SetupAvatar(_player, _avatarGraphic, _avatarLetter);
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
