using UnityEngine;

namespace PurrNet.Lobby
{
    public class OpenURL : MonoBehaviour
    {
        [SerializeField] private string _url = "purrnet.dev";

        public void Open()
        {
            Application.OpenURL(_url);
        }
    }
}
