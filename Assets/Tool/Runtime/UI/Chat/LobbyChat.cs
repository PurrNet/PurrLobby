using UnityEngine;

namespace PurrNet.Lobby
{
    public class LobbyChat : MonoBehaviour
    {
        [SerializeField] private ChatEntry _chatEntry;
        [SerializeField] private RectTransform _content;
        [SerializeField] private TMPro.TMP_InputField _input;

        private ILobby _lobby;

        public void Setup(ILobby lobby)
        {
            _lobby = lobby;
            _lobby.chat.onMessageReceived += OnMessageReceived;
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

        private void OnMessageReceived(IPlayer player, string message)
        {
            var entry = Instantiate(_chatEntry, _content);
            entry.Setup(player, message);
        }
    }
}
