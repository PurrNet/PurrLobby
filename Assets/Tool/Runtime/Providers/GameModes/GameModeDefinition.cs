using UnityEngine;

namespace PurrNet.Lobby
{
    public class GameModeDefinition : ScriptableObject
    {
        public GameMapCollection maps;
        public SettingsDefinition[] settings;

        public bool HasSettings<T>() where T : SettingsDefinition
        {
            return TryGetSettings<T>(out _);
        }

        public bool TryGetSettings<T>(out T result) where T : SettingsDefinition
        {
            for (var i = 0; i < settings.Length; i++)
            {
                if (settings[i] is T casted)
                {
                    result = casted;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
