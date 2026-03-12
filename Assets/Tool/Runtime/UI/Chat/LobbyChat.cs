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
        [SerializeField] private RectTransform _content;
        [SerializeField] private TMPro.TMP_InputField _input;

        private ILobby _lobby;

        private ChatEntry _lastChatEntry;

        public void Setup(ILobby lobby)
        {
            _lobby = lobby;
            _lobby.chat.onMessageReceived += OnMessageReceived;
            _scrollRect.verticalNormalizedPosition = 0f;
        }

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
        }

        private void OnDisable()
        {
            _input.onSubmit.RemoveListener(OnSubmit);

            if (_lobby != null)
                _lobby.chat.onMessageReceived -= OnMessageReceived;
        }

        public void OnSubmit(string _)
        {
            SendMessage();
            _input.ActivateInputField();
        }

        public void SendMessage()
        {
            const int MAX_LEN = 200;

            string message = _input.text;
            _input.text = string.Empty;

            if (message.Length > MAX_LEN)
                message = message[..MAX_LEN];

            if (!string.IsNullOrWhiteSpace(message))
            {
                _lobby.chat.SendMessage(message);
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

        private void OnMessageReceived(IPlayer player, string message)
        {
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
