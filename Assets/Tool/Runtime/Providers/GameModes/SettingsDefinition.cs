using PurrNet.Logging;
using UnityEngine;

namespace PurrNet.Lobby
{
    public abstract class SettingsDefinition : ScriptableObject
    {
        public string key;

        public abstract T GetValue<T>();
    }

    public class SettingsDefinition<T> : SettingsDefinition
    {
        public T value;

        public override V GetValue<V>()
        {
            if (value is V result)
                return result;

            PurrLogger.LogError($"Can't convert {typeof(T).Name} to {typeof(T).Name}");
            return default;
        }
    }
}
