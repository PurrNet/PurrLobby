using PurrNet.UI;
using PurrNet.Utils;
using UnityEngine;

namespace PurrNet.Lobby
{
    /// <summary>UI helper rendering a player's avatar, or an initial on a deterministic color when absent.</summary>
    public static class PlayerAvatarUI
    {
        public static void SetupAvatar(IPlayer player, RectangleGraphic graphic, TMPro.TMP_Text letter)
        {
            if (player.avatar)
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
