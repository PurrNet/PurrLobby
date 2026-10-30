#if PURR_SERVICES_MATCHMAKING
using UnityEditor;
using UnityEngine;

namespace PurrNet.Lobby.PurrNet.Editor
{
    /// <summary>Creates independent presets without replacing a project's existing lobby setup.</summary>
    public static class MatchmakingPresetFactory
    {
        const string Parent = "Assets/PurrLobby/Providers/PurrNet/Preset";
        const string Folder = Parent + "/Matchmaking";

        [MenuItem("Tools/PurrNet/PurrLobby/Create Service Matchmaking Presets")]
        public static void CreatePresets()
        {
            if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Parent, "Matchmaking");
            Create("Casual", "casual", "Orchestrator.PurrNet.asset", PurrServicesMatchTransport.Relay);
            Create("Ranked", "ranked", "Orchestrator.PurrNetSteam.asset", PurrServicesMatchTransport.DirectUdp);
            AssetDatabase.SaveAssets();
            Debug.Log("Service matchmaking presets are ready. Configure queue IDs, regional probes and the game scene before playing.");
        }

        static void Create(string label, string queue, string template, PurrServicesMatchTransport transport)
        {
            string orchestratorPath = $"{Folder}/Orchestrator.Service{label}.asset";
            if (AssetDatabase.LoadAssetAtPath<GameOrchestrator>(orchestratorPath)) return;
            var source = AssetDatabase.LoadAssetAtPath<GameOrchestrator>($"{Parent}/{template}");
            if (!source) throw new System.InvalidOperationException($"Missing lobby preset: {template}");

            var matchmaker = CreateAsset<PurrServicesMatchmakingProvider>($"{Folder}/Matchmaker.{label}.asset");
            var settings = new SerializedObject(matchmaker);
            settings.FindProperty("_queueId").stringValue = queue;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var allocator = CreateAsset<PurrServicesMatchGameAllocator>($"{Folder}/GameAllocator.{label}.asset");
            settings = new SerializedObject(allocator);
            settings.FindProperty("_gameScene").stringValue = "GameScene";
            settings.FindProperty("_transport").enumValueIndex = (int)transport;
            settings.ApplyModifiedPropertiesWithoutUndo();

            var orchestrator = Object.Instantiate(source);
            orchestrator.name = $"Orchestrator.Service{label}";
            orchestrator.matchmakingProvider = matchmaker;
            orchestrator.gameAllocator = allocator;
            AssetDatabase.CreateAsset(orchestrator, orchestratorPath);
        }

        static T CreateAsset<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing) return existing;
            var asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }
    }
}
#endif
