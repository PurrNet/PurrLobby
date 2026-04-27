using PurrNet.Utils;

namespace PurrNet.Services.Telemetry
{
    public static class PurrTelemetrySettings
    {
        public const string KeyPublicKey = "PurrTelemetry.publicKey";
        public const string KeyProjectId = "PurrTelemetry.projectId";
        public const string KeyProjectName = "PurrTelemetry.projectName";
        public const string KeyBaseUrl = "PurrTelemetry.baseUrl";

        public const string DefaultBaseUrl = "https://purrnet.dev";

        public const int FlushBatchThreshold = 25;
        public const int FlushIntervalSeconds = 30;
        public const int MaxBatchSize = 50;
        public const int MaxRetries = 4;

        public static string publicKey =>
            ApplicationConstants.TryGet(KeyPublicKey, out var v) ? v : null;

        public static string projectId =>
            ApplicationConstants.TryGet(KeyProjectId, out var v) ? v : null;

        public static string projectName =>
            ApplicationConstants.TryGet(KeyProjectName, out var v) ? v : null;

        public static string baseUrl =>
            ApplicationConstants.TryGet(KeyBaseUrl, out var v) && !string.IsNullOrEmpty(v)
                ? v
                : DefaultBaseUrl;

        public static bool isLinked =>
            !string.IsNullOrEmpty(publicKey) && !string.IsNullOrEmpty(projectId);
    }
}
