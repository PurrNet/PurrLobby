using UnityEngine;

namespace PurrNet.Lobby
{
    [CreateAssetMenu(menuName = "PurrLobby/Game/Maps/Map Collection", order = -201)]
    public class GameMapCollection : ScriptableObject
    {
        public GameMapDefinition[] maps;
    }
}
