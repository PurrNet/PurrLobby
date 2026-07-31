using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class ChatEntry : MonoBehaviour
    {
        [SerializeField] private RectangleGraphic _avatarGraphic;
        [SerializeField] private TMPro.TMP_Text _avatarLetter;
        [SerializeField] private TMPro.TMP_Text _username;
        [SerializeField] private TMPro.TMP_Text _message;

        public IPlayer player { get; private set; }

        public void Setup(IPlayer player, string message)
        {
            this.player = player;

            if (_username && player != null)
                _username.text = player.displayName;

            if (_message)
                _message.text = message;

            if (_avatarGraphic && _avatarLetter && player != null)
                PlayerAvatarUI.SetupAvatar(player, _avatarGraphic, _avatarLetter);
        }

        public void AppendNewMessage(string message)
        {
            if (_message)
                _message.text += $"\n{message}";
        }
    }
}
