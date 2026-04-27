using System;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using PurrNet.Editor;
using UnityEngine.Networking;

namespace PurrNet.Services.Editor
{
    public class ProjectInfo
    {
        [JsonProperty("id")]
        public string id;

        [JsonProperty("name")]
        public string name;

        [JsonProperty("slug")]
        public string slug;

        [JsonProperty("plan")]
        public string plan;

        [JsonProperty("publicKey")]
        public string publicKey;

        [JsonProperty("createdAt")]
        public string createdAt;

        [JsonProperty("updatedAt")]
        public string updatedAt;
    }

    public class ProjectsResponse
    {
        [JsonProperty("projects")]
        public ProjectInfo[] projects;
    }

    public class CreateProjectResponse
    {
        [JsonProperty("project")]
        public ProjectInfo project;
    }

    public class LobbyInfo
    {
        [JsonProperty("id")]
        public string id;

        [JsonProperty("name")]
        public string name;

        [JsonProperty("joinable")]
        public bool joinable;

        [JsonProperty("visibility")]
        public string visibility;

        [JsonProperty("code")]
        public string code;

        [JsonProperty("maxPlayers")]
        public int maxPlayers;

        [JsonProperty("playerCount")]
        public int playerCount;

        [JsonProperty("createdAt")]
        public long createdAt;
    }

    public class LobbyStats
    {
        [JsonProperty("activeLobbies")]
        public int activeLobbies;

        [JsonProperty("totalPlayers")]
        public int totalPlayers;
    }

    public class ProjectLobbiesResponse
    {
        [JsonProperty("stats")]
        public LobbyStats stats;

        [JsonProperty("lobbies")]
        public LobbyInfo[] lobbies;
    }

    public static class PurrServicesAPI
    {
        const string BASE_URL = "https://purrnet.dev/api";

        public static async Task<Result<ProjectsResponse>> GetProjects(string apiKey)
        {
            return await SendGet<ProjectsResponse>($"{BASE_URL}/projects", apiKey);
        }

        public static async Task<Result<CreateProjectResponse>> CreateProject(string apiKey, string name)
        {
            var body = JsonConvert.SerializeObject(new { name });
            return await SendPost<CreateProjectResponse>($"{BASE_URL}/projects", apiKey, body);
        }

        public static async Task<Result<ProjectLobbiesResponse>> GetProjectLobbies(string apiKey, string projectId)
        {
            return await SendGet<ProjectLobbiesResponse>($"{BASE_URL}/projects/{projectId}/lobbies", apiKey);
        }

        static async Task<Result<T>> SendGet<T>(string url, string apiKey)
        {
            try
            {
                var request = UnityWebRequest.Get(url);
                if (!string.IsNullOrEmpty(apiKey))
                    request.SetRequestHeader("Authorization", "Bearer " + apiKey);

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                    return Result<T>.Fail(ParseError(request));

                var result = JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
                return Result<T>.Ok(result);
            }
            catch (Exception e)
            {
                return Result<T>.Fail(e.Message);
            }
        }

        static async Task<Result<T>> SendPost<T>(string url, string apiKey, string jsonBody)
        {
            try
            {
                var request = new UnityWebRequest(url, "POST");
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");

                if (!string.IsNullOrEmpty(apiKey))
                    request.SetRequestHeader("Authorization", "Bearer " + apiKey);

                await request.SendWebRequest();

                if (request.result != UnityWebRequest.Result.Success)
                    return Result<T>.Fail(ParseError(request));

                var result = JsonConvert.DeserializeObject<T>(request.downloadHandler.text);
                return Result<T>.Ok(result);
            }
            catch (Exception e)
            {
                return Result<T>.Fail(e.Message);
            }
        }

        static string ParseError(UnityWebRequest request)
        {
            try
            {
                var err = JsonConvert.DeserializeObject<ApiError>(request.downloadHandler.text);
                if (err != null && !string.IsNullOrEmpty(err.Error))
                    return err.Error;
            }
            catch
            {
                // ignore
            }
            return request.error;
        }
    }
}
