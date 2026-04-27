using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace PurrNet.Services
{
    [Serializable]
    public struct EdgegapDeployRequest
    {
        /// <summary>
        /// Player IDs for server region selection.
        /// The server resolves these to cached IP addresses.
        /// </summary>
        [JsonProperty("playerIds", NullValueHandling = NullValueHandling.Ignore)]
        public string[] playerIds;
    }

    [Serializable]
    public struct EdgegapDeployResponse
    {
        [JsonProperty("requestId")]
        public string requestId;
    }

    [Serializable]
    public struct EdgegapPortInfo
    {
        [JsonProperty("external")]
        public int external;

        [JsonProperty("internal")]
        public int @internal;

        [JsonProperty("protocol")]
        public string protocol;

        [JsonProperty("name")]
        public string name;
    }

    [Serializable]
    public struct EdgegapStatusResponse
    {
        [JsonProperty("requestId")]
        public string requestId;

        [JsonProperty("status")]
        public string status;

        [JsonProperty("ready")]
        public bool ready;

        [JsonProperty("fqdn")]
        public string fqdn;

        [JsonProperty("publicIp")]
        public string publicIp;

        [JsonProperty("ports")]
        public Dictionary<string, EdgegapPortInfo> ports;

        [JsonProperty("error")]
        public bool error;

        [JsonProperty("errorDetail")]
        public string errorDetail;
    }

    [Serializable]
    public struct EdgegapStopResponse
    {
        [JsonProperty("success")]
        public bool success;

        [JsonProperty("requestId")]
        public string requestId;
    }

    [Serializable]
    public struct EdgegapStoppedDeployment
    {
        [JsonProperty("requestId")]
        public string requestId;

        [JsonProperty("ok")]
        public bool ok;

        [JsonProperty("error")]
        public string error;
    }

    [Serializable]
    public struct EdgegapStopAllResponse
    {
        [JsonProperty("count")]
        public int count;

        [JsonProperty("stopped")]
        public EdgegapStoppedDeployment[] stopped;
    }

    public struct DeployResult
    {
        public bool success;
        public string requestId;
        public string error;
    }

    public struct DeploymentStatusResult
    {
        public bool success;
        public EdgegapStatusResponse deployment;
        public string error;
    }

    public struct DeploymentStopResult
    {
        public bool success;
        public string requestId;
        public string error;
    }

    public struct DeploymentStopAllResult
    {
        public bool success;
        public int count;
        public EdgegapStoppedDeployment[] stopped;
        public string error;
    }
}
