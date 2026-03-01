using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using PurrLobby.Internal;
using UnityEngine;

namespace PurrLobby
{
    internal sealed class PurrLobbyChat : ILobbyChat
    {
        readonly LobbyApiClient _api;
        readonly string _lobbyId;
        readonly Func<string, IPlayer> _resolvePlayer;

        internal int lastSeq;

        public event Action<IPlayer, ArraySegment<byte>> onMessageReceived;

        public PurrLobbyChat(LobbyApiClient api, string lobbyId, Func<string, IPlayer> resolvePlayer)
        {
            _api = api;
            _lobbyId = lobbyId;
            _resolvePlayer = resolvePlayer;
        }

        public void SendMessage(ArraySegment<byte> data)
        {
            SendMessageAsync(data).Forget();
        }

        async Task SendMessageAsync(ArraySegment<byte> data)
        {
            byte[] bytes;
            if (data.Offset == 0 && data.Count == data.Array!.Length)
            {
                bytes = data.Array;
            }
            else
            {
                bytes = new byte[data.Count];
                Buffer.BlockCopy(data.Array!, data.Offset, bytes, 0, data.Count);
            }

            string base64 = Convert.ToBase64String(bytes);
            await _api.PostAsync($"/api/lobby/{_lobbyId}/chat", new Dictionary<string, object>
            {
                { "data", base64 }
            });
        }

        /// <summary>
        /// Process chat messages from a poll response, firing events for new messages.
        /// </summary>
        internal void ProcessMessages(JArray chatArray)
        {
            if (chatArray == null) return;

            foreach (var item in chatArray)
            {
                if (item is not JObject msg) continue;

                int seq = msg.GetInt("seq");
                if (seq <= lastSeq) continue;
                lastSeq = seq;

                string playerId = msg.GetString("playerId");
                string data = msg.GetString("data");
                if (data == null) continue;

                try
                {
                    byte[] decoded = Convert.FromBase64String(data);
                    var player = _resolvePlayer(playerId);
                    onMessageReceived?.Invoke(player, new ArraySegment<byte>(decoded));
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[PurrLobby] Failed to decode chat message: {ex.Message}");
                }
            }
        }
    }
}
