using UnityEngine;

namespace PurrNet.Lobby
{
    [CreateAssetMenu(menuName = "PurrLobby/Game/Settings/Test Setting", order = -201)]
    public class TestSetting : SettingsDefinition
    {
        public GameMapCollection map;
        [Header("Settings")]
        public float movementSpeedMultiplier = 1f;
        [Range(0f, 1f)]
        public float somethingElse = 1f;
        public int numberOfBots = 0;
        public Vector2 mapSize = new Vector2(100f, 100f);
        public Vector3 spawnPosition = new Vector3(0f, 0f, 0f);
        [Header("Teams")]
        public string teamAName = "Team A";
        public Color teamAColor = Color.blue;
        public string teamBName = "Team B";
        public Color teamBColor = Color.red;
        [Header("Asset")]
        public Material someMaterial;
        public Sprite someSprite;
    }
}
