using UnityEngine;

namespace PurrNet.Lobby
{
    [CreateAssetMenu(menuName = "PurrLobby/Game/Maps/Map Definition", order = -201)]
    public class GameMapDefinition : ScriptableObject
    {
        public string displayName;

        public Sprite preview;

        [Tooltip("What target scene to load")]
        [PurrScene] public string scene;

        [Tooltip("Per-map custom settings")]
        public SettingsDefinition[] settings;
    }
}
