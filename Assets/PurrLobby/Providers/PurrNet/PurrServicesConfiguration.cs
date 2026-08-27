#if PURR_SERVICES
using PurrNet.Services;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet
{
    internal static class PurrServicesConfiguration
    {
        public const string UserError =
            "Online services are unavailable right now. Please try again later.";

        public const string DeveloperError =
            "PurrServices is installed but the active runtime profile is not linked to a project. " +
            "Open Tools > PurrNet > PurrServices, then create or select a project and assign it to " +
            "Player Builds or the Unity Editor override. Authentication was not started.";

        public static bool IsConfigured(PurrServices services) =>
            services.isConfigured && !string.IsNullOrWhiteSpace(PurrServicesSettings.projectId);

        public static void LogDeveloperError(Object context)
        {
            Debug.LogError(DeveloperError, context);
        }
    }
}
#endif
