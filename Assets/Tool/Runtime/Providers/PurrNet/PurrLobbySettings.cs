using PurrLobby.Internal;
using UnityEngine;

namespace PurrLobby
{
    [CreateAssetMenu(fileName = "PurrLobbySettings", menuName = "PurrNet/Lobby/PurrNet Settings")]
    public sealed class PurrLobbySettings : ScriptableObject
    {
        [Header("Server")]
        [Tooltip("Base URL of the PurrNet website API (e.g. https://purrnet.dev)")]
        public string apiUrl = "https://purrnet.dev";

        [Tooltip("Optional API key from the PurrNet dashboard. Leave empty for the free tier.")]
        public string apiKey;

        [Tooltip("Game identifier used for free-tier lobby namespacing. Required when no API key is set.")]
        public string gameId;

        public PurrLobbyProvider CreateProvider(string playerId, string playerName = null)
        {
            string pname = playerName ?? SystemInfo.deviceName;
            return new PurrLobbyProvider(apiUrl, apiKey, gameId, playerId, pname);
        }

        public PurrGameStarter CreateGameStarter(string playerId, string playerName = null)
        {
            string pname = playerName ?? SystemInfo.deviceName;
            var api = new LobbyApiClient(apiUrl, apiKey, gameId, playerId, pname);
            return new PurrGameStarter(api);
        }
    }
}
