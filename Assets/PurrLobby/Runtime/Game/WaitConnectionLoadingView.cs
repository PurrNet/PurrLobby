using PurrNet.Transports;
using PurrNet.UI;
using UnityEngine;

namespace PurrNet.Lobby
{
    public class WaitConnectionLoadingView : MonoBehaviour
    {
        [SerializeField] private ViewStack _stack;

        private FullScreenLoadingView _loadingScreen;

        private void Awake()
        {
            if (!NetworkManager.isClientStatic)
            {
                _loadingScreen = _stack.Push<FullScreenLoadingView>();
                _loadingScreen.Setup("Connecting...");
            }
        }

        private void OnEnable()
        {
            NetworkManager.onAnyClientConnectionState += OnConnectionStateEvent;
        }

        private void OnDisable()
        {
            NetworkManager.onAnyClientConnectionState -= OnConnectionStateEvent;
        }

        private void OnConnectionStateEvent(ConnectionState conn)
        {
            if (!_loadingScreen)
                return;

            _loadingScreen.Setup($"{conn}...");

            if (conn == ConnectionState.Connected)
                _stack.Pop(_loadingScreen);
        }
    }
}
