using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PurrNet.Services
{
    [Serializable]
    public struct AuthRequest
    {
        [JsonProperty("provider")]
        public string provider;

        [JsonProperty("credentials")]
        public Dictionary<string, string> credentials;
    }

    [Serializable]
    public struct AuthResponse
    {
        [JsonProperty("sessionToken")]
        public string sessionToken;

        [JsonProperty("playerId")]
        public string playerId;

        [JsonProperty("displayName")]
        public string displayName;

        [JsonProperty("provider")]
        public string provider;

        [JsonProperty("expiresAt")]
        public string expiresAt;
    }

    [Serializable]
    public struct SessionValidationResponse
    {
        [JsonProperty("playerId")]
        public string playerId;

        [JsonProperty("displayName")]
        public string displayName;

        [JsonProperty("provider")]
        public string provider;

        [JsonProperty("expiresAt")]
        public string expiresAt;
    }

    public struct AuthResult
    {
        public bool success;
        public string playerId;
        public string displayName;
        public string error;
    }
}
