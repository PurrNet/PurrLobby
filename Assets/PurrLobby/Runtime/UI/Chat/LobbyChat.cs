using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace PurrNet.Lobby
{
    public class LobbyChat : MonoBehaviour
    {
        [SerializeField] private ScrollRect _scrollRect;
        [SerializeField] private ChatEntry _chatEntry;
        [SerializeField] private ChatEntry _systemEntry;
        [SerializeField] private RectTransform _content;
        [SerializeField] private TMPro.TMP_InputField _input;

        private ILobby _lobby;

        private ChatEntry _lastChatEntry;

        public void Setup(ILobby lobby)
        {
            if (_lobby != null)
                Unsubscribe();

            _lobby = lobby;
            if (isActiveAndEnabled)
                Subscribe();
            _scrollRect.verticalNormalizedPosition = 0f;
        }

        private void Subscribe()
        {
            if (_lobby == null || _subscribed)
                return;
            _subscribed = true;
            _lobby.chat.onMessageReceived += OnMessageReceived;
            _lobby.onPlayerJoined += OnPlayerJoined;
            _lobby.onPlayerLeft += OnPlayerLeft;
        }

        private void Unsubscribe()
        {
            if (_lobby == null || !_subscribed)
                return;
            _subscribed = false;
            _lobby.chat.onMessageReceived -= OnMessageReceived;
            _lobby.onPlayerJoined -= OnPlayerJoined;
            _lobby.onPlayerLeft -= OnPlayerLeft;
        }

        private bool _subscribed;

        private void Update()
        {
            if (!WasEnterPressed())
                return;

            var selected = EventSystem.current?.currentSelectedGameObject;
            if (selected)
                return;

            _input.ActivateInputField();
        }

        static bool WasEnterPressed()
        {
#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            return kb != null && (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame);
#else
            return Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter);
#endif
        }

        private void OnEnable()
        {
            _input.onSubmit.AddListener(OnSubmit);
            Subscribe();
        }

        private void OnDisable()
        {
            _input.onSubmit.RemoveListener(OnSubmit);
            Unsubscribe();
        }

        private void OnPlayerJoined(IPlayer player)
        {
            SystemMessage($"{player.displayName} joined the lobby.");
        }

        private void OnPlayerLeft(IPlayer player)
        {
            SystemMessage($"{player.displayName} left the lobby.");
        }

        public void OnSubmit(string _)
        {
            SendChatMessage();
            _input.ActivateInputField();
        }

        public void SendChatMessage()
        {
            const int MAX_LEN = 200;

            string message = _input.text;
            _input.text = string.Empty;

            if (message.Length > MAX_LEN)
                message = $"{message[..MAX_LEN]}...";

            if (_lobby != null && !string.IsNullOrWhiteSpace(message))
            {
                _lobby.chat.SendMessage(Encoding.UTF8.GetBytes(message));
            }
        }

        private bool IsScrolledToBottom()
        {
            float scrollPos = _scrollRect.verticalNormalizedPosition;
            return scrollPos <= 0.001f;
        }

        private bool CanScrollVertically()
        {
            float contentHeight = _content.rect.height;
            float viewportHeight = _scrollRect.viewport.rect.height;
            return contentHeight > viewportHeight;
        }

        private void SystemMessage(string message)
        {
            _lastChatEntry = null;
            var entry = Instantiate(_systemEntry, _content);
            string final = $"<icon=server> {message}";
            entry.Setup(null, final);
        }

        private void OnMessageReceived(IPlayer player, byte[] data)
        {
            var message = Encoding.UTF8.GetString(data);
            bool wasScrolledToBottom = IsScrolledToBottom() || !CanScrollVertically();

            if (_lastChatEntry && _lastChatEntry.player == player)
            {
                _lastChatEntry.AppendNewMessage(message);
                if (wasScrolledToBottom)
                {
                    Canvas.ForceUpdateCanvases();
                    _scrollRect.verticalNormalizedPosition = 0f;
                }
                return;
            }

            var entry = Instantiate(_chatEntry, _content);
            entry.Setup(player, message);
            _lastChatEntry  = entry;
            if (wasScrolledToBottom)
            {
                Canvas.ForceUpdateCanvases();
                _scrollRect.verticalNormalizedPosition = 0f;
            }
        }
    }
}
