using PurrNet.UI;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet.Lobby
{
    public interface IPlayer
    {
        public const string READY_KEY = "PLAYER_READY_KEY";
        public const string READY_TRUTHY_VALUE = "1";

        string id { get; }

        string displayName { get; }

        Texture2D avatar { get; }

        bool isHost { get; }

        bool isReady { get; }

        IMetadata userData { get; }

        public event System.Action onPlayerUpdated;

        public event System.Action onPlayerMetadataUpdated;

        public void SetReady(bool isReady);

        public static void SetupAvatar(IPlayer player, RectangleGraphic graphic, TMPro.TMP_Text letter)
        {
            if  (player.avatar)
            {
                graphic.texture = player.avatar;
                letter.enabled = false;
            }
            else
            {
                var playerHash = Hasher.Hash(player.id);
                var playerRandomColor = Color.HSVToRGB(playerHash % 1000 / 1000f, 0.5f, 0.8f);
                graphic.color = playerRandomColor;
                graphic.texture = null;
                letter.enabled = true;
                letter.text = !string.IsNullOrEmpty(player.displayName) ? player.displayName[..1].ToUpper() : "?";
            }
        }
    }
}
