using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace PurrNet.Services
{
    public struct HttpResult<T>
    {
        public bool success;
        public T data;
        public int statusCode;
        public string error;
    }

    public class ServiceHttp
    {
        readonly Func<string> _getBaseUrl;
        readonly Func<string> _getApiKey;
        readonly Func<string> _getSessionToken;
        readonly Func<string> _getPlayerToken;

        public ServiceHttp(
            Func<string> getBaseUrl,
            Func<string> getApiKey,
            Func<string> getSessionToken,
            Func<string> getPlayerToken)
        {
            _getBaseUrl = getBaseUrl;
            _getApiKey = getApiKey;
            _getSessionToken = getSessionToken;
            _getPlayerToken = getPlayerToken;
        }

        internal string BuildUrl(string path)
        {
            var baseUrl = _getBaseUrl().TrimEnd('/');
            return $"{baseUrl}{path}";
        }

        void SetHeaders(UnityWebRequest request)
        {
            request.SetRequestHeader("Content-Type", "application/json");

            var apiKey = _getApiKey();
            if (!string.IsNullOrEmpty(apiKey))
                request.SetRequestHeader("Authorization", $"Bearer {apiKey}");

            var session = _getSessionToken();
            if (!string.IsNullOrEmpty(session))
                request.SetRequestHeader("X-Player-Session", session);

            var playerToken = _getPlayerToken();
            if (!string.IsNullOrEmpty(playerToken))
                request.SetRequestHeader("X-Player-Token", playerToken);
        }

        HttpResult<T> ParseResponse<T>(UnityWebRequest request)
        {
            var result = new HttpResult<T>
            {
                statusCode = (int)request.responseCode
            };

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    result.data = JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
                    result.success = true;
                }
                catch (Exception e)
                {
                    result.error = $"Failed to parse response: {e.Message}";
                }
            }
            else
            {
                result.error = ParseErrorMessage(request);
            }

            return result;
        }

        static string ParseErrorMessage(UnityWebRequest request)
        {
            var body = request.downloadHandler?.text;
            if (string.IsNullOrEmpty(body))
                return request.error;

            try
            {
                var obj = JsonConvert.DeserializeObject<ErrorResponse>(body);
                if (!string.IsNullOrEmpty(obj.error))
                    return obj.error;
            }
            catch
            {
                // not JSON or wrong shape — fall through
            }

            return body;
        }

        [Serializable]
        struct ErrorResponse
        {
            [JsonProperty("error")]
            public string error;
        }

        public async Task<HttpResult<T>> GetAsync<T>(string path)
        {
            using var request = UnityWebRequest.Get(BuildUrl(path));
            SetHeaders(request);
            await request.SendWebRequest();
            return ParseResponse<T>(request);
        }

        public async Task<HttpResult<T>> PostAsync<T>(string path, object body = null)
        {
            var url = BuildUrl(path);
            using var request = new UnityWebRequest(url, "POST");
            request.downloadHandler = new DownloadHandlerBuffer();

            if (body != null)
            {
                var json = JsonConvert.SerializeObject(body);
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            }

            SetHeaders(request);
            await request.SendWebRequest();
            return ParseResponse<T>(request);
        }

        public async Task<HttpResult<T>> PatchAsync<T>(string path, object body)
        {
            var url = BuildUrl(path);
            using var request = new UnityWebRequest(url, "PATCH");
            request.downloadHandler = new DownloadHandlerBuffer();

            var json = JsonConvert.SerializeObject(body);
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));

            SetHeaders(request);
            await request.SendWebRequest();
            return ParseResponse<T>(request);
        }

        public async Task<HttpResult<T>> DeleteAsync<T>(string path, object body = null)
        {
            var url = BuildUrl(path);
            using var request = new UnityWebRequest(url, "DELETE");
            request.downloadHandler = new DownloadHandlerBuffer();

            if (body != null)
            {
                var json = JsonConvert.SerializeObject(body);
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            }

            SetHeaders(request);
            await request.SendWebRequest();
            return ParseResponse<T>(request);
        }
    }
}
