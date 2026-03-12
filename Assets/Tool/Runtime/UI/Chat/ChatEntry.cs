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

        public void Setup(IPlayer player, string message)
        {
            _username.text = player.displayName;
            _message.text = message;

            IPlayer.SetupAvatar(player, _avatarGraphic, _avatarLetter);
        }
    }
}
