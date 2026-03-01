using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;

namespace PurrLobby.Internal
{
    internal sealed class LobbyApiClient
    {
        readonly string _baseUrl;
        readonly string _apiKey;
        readonly string _gameId;
        readonly string _playerId;
        readonly string _playerName;

        public LobbyApiClient(string baseUrl, string apiKey, string gameId, string playerId, string playerName)
        {
            _baseUrl = baseUrl.TrimEnd('/');
            _apiKey = apiKey;
            _gameId = gameId;
            _playerId = playerId;
            _playerName = playerName;
        }

        public Task<string> GetAsync(string path, Dictionary<string, string> queryParams = null)
        {
            string url = BuildUrl(path, queryParams);
            return SendAsync("GET", url, null);
        }

        public Task<string> PostAsync(string path, Dictionary<string, object> body = null)
        {
            string url = BuildUrl(path);
            string jsonBody = body != null ? Json.Serialize(body) : null;
            return SendAsync("POST", url, jsonBody);
        }

        public Task<string> PatchAsync(string path, Dictionary<string, object> body)
        {
            string url = BuildUrl(path);
            string jsonBody = Json.Serialize(body);
            return SendAsync("PATCH", url, jsonBody);
        }

        public Task<string> DeleteAsync(string path)
        {
            string url = BuildUrl(path);
            return SendAsync("DELETE", url, null);
        }

        string BuildUrl(string path, Dictionary<string, string> queryParams = null)
        {
            var sb = new StringBuilder(_baseUrl);
            sb.Append(path);

            if (queryParams != null && queryParams.Count > 0)
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

        async Task<string> SendAsync(string method, string url, string jsonBody)
        {
            using var request = new UnityWebRequest(url, method);
            request.downloadHandler = new DownloadHandlerBuffer();

            if (jsonBody != null)
            {
                byte[] bodyBytes = Encoding.UTF8.GetBytes(jsonBody);
                request.uploadHandler = new UploadHandlerRaw(bodyBytes);
                request.SetRequestHeader("Content-Type", "application/json");
            }

            if (!string.IsNullOrEmpty(_apiKey))
                request.SetRequestHeader("Authorization", $"Bearer {_apiKey}");
            else if (!string.IsNullOrEmpty(_gameId))
                request.SetRequestHeader("X-Game-Id", _gameId);
            request.SetRequestHeader("X-Player-Id", _playerId);
            request.SetRequestHeader("X-Player-Name", _playerName);

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
