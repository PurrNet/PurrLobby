using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    public enum NakamaScheme
    {
        Http,
        Https,
    }

    [CreateAssetMenu(menuName = "PurrLobby/Nakama/Config", fileName = "Nakama Config", order = -202)]
    public class NakamaConfig : ScriptableObject
    {
        [Tooltip("Connection scheme. Use Http for local dev, Https for Heroic Cloud.")]
        [SerializeField] private NakamaScheme _scheme = NakamaScheme.Http;

        [Tooltip("Hostname or IP. Defaults to local Nakama server.")]
        [SerializeField] private string _host = "127.0.0.1";

        [Tooltip("Port. Default Nakama gRPC-gateway port is 7350.")]
        [SerializeField] private int _port = 7350;

        [Tooltip("Server key. Default Nakama server key is 'defaultkey'.")]
        [SerializeField] private string _serverKey = "defaultkey";

        public string scheme => _scheme == NakamaScheme.Https ? "https" : "http";
        public string host => _host;
        public int port => _port;
        public string serverKey => _serverKey;
    }
}
