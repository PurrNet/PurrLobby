using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PurrLobby.Utils
{
    internal sealed class LobbyApiClient
    {
        readonly PurrSession _session;

        string _baseUrl => _session.apiUrl.TrimEnd('/');

        string _apiKey => _session.projectClientKey;

        string _playerId => _session.playerId;

        string _playerName => _session.playerName;

        public LobbyApiClient(PurrSession session)
        {
            _session = session;
        }

        public Task<string> GetAsync(string path, Dictionary<string, string> queryParams = null, string playerToken = null)
        {
            string url = BuildUrl(path, queryParams);
            return SendAsync("GET", url, null, playerToken);
        }

        public Task<string> PostAsync(string path, Dictionary<string, object> body = null, string playerToken = null)
        {
            string url = BuildUrl(path);
            string jsonBody = body != null ? Json.Serialize(body) : null;
            return SendAsync("POST", url, jsonBody, playerToken);
        }

        public Task<string> PatchAsync(string path, Dictionary<string, object> body, string playerToken = null)
        {
            string url = BuildUrl(path);
            string jsonBody = Json.Serialize(body);
            return SendAsync("PATCH", url, jsonBody, playerToken);
        }

        public Task<string> DeleteAsync(string path, string playerToken = null)
        {
            string url = BuildUrl(path);
            return SendAsync("DELETE", url, null, playerToken);
        }

        string BuildUrl(string path, Dictionary<string, string> queryParams = null)
        {
            var sb = new StringBuilder(_baseUrl);
            sb.Append(path);

            if (queryParams is { Count: > 0 })
            {
                sb.Append('?');
                bool first = true;
                foreach (var kv in queryParams)
                {
                    if (!first) sb.Append('&');
                    first = false;
                    sb.Append(UnityWebRequest.EscapeURL(kv.Key));
                    sb.Append('=');
                    sb.Append(UnityWebRequest.EscapeURL(kv.Value));
                }
            }

            return sb.ToString();
        }

        async Task<string> SendAsync(string method, string url, string jsonBody, string playerToken = null)
        {
            using var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (jsonBody != null)
            {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.SetRequestHeader("Content-Type", "application/json");
            }
            else if (method is "POST" or "PATCH")
            {
                request.uploadHandler = new UploadHandlerRaw(Array.Empty<byte>());
                request.SetRequestHeader("Content-Type", "application/json");
            }

            request.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            request.SetRequestHeader("X-Player-Id", _playerId);
            request.SetRequestHeader("X-Player-Name", _playerName);

            if (!string.IsNullOrEmpty(playerToken))
                request.SetRequestHeader("X-Player-Token", playerToken);

            await request.SendWebRequest();

            string responseText = request.downloadHandler?.text;

            if (request.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = TryExtractError(responseText) ?? request.error;
                throw new LobbyApiException(errorMsg, (int)request.responseCode);
            }

            return responseText;
        }

        static string TryExtractError(string responseText)
        {
            if (string.IsNullOrEmpty(responseText)) return null;
            try
            {
                var obj = Json.ParseObject(responseText);
                return obj?.GetString("error");
            }
            catch
            {
                return null;
            }
        }
    }

    internal sealed class LobbyApiException : Exception
    {
        public int StatusCode { get; }

        public LobbyApiException(string message, int statusCode) : base(message)
        {
            StatusCode = statusCode;
        }
    }
}
