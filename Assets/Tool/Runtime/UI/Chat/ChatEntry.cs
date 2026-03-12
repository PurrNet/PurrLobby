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

            _username.text = player.displayName;
            _message.text = message;

            IPlayer.SetupAvatar(player, _avatarGraphic, _avatarLetter);
        }

        public void AppendNewMessage(string message)
        {
            _message.text += $"\n{message}";
        }
    }
}
