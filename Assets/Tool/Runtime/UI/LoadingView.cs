using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class LoadingView : MonoView
    {
        [SerializeField] private TMPro.TMP_Text _messageText;

        public void Setup(string message = "Loading...")
        {
            _messageText.text = message;
        }

        public void SetMessage(string message)
        {
            _messageText.text = message;
        }
    }
}
