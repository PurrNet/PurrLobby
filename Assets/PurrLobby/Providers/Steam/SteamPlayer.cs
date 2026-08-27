#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using Steamworks;
using UnityEngine;

namespace PurrNet.Lobby.Steam
{
    public class SteamPlayer : LobbyPlayerBase
    {
        public override IMetadata userData => _metadata;

        /// <summary>Lazily fetched large Steam avatar; arrives via AvatarImageLoaded_t.</summary>
        public override Texture2D avatar
        {
            get
            {
                if (_avatar)
                    return _avatar;

                RequestAvatar();
                return _avatar;
            }
        }

        internal CSteamID steamId { get; }

        private readonly SteamMetadata _metadata;
        private Texture2D _avatar;
        private bool _avatarRequested;

        internal SteamPlayer(SteamLobby lobby, CSteamID steamId, bool isOwner, bool isLocal)
            : base(steamId.m_SteamID.ToString(), SteamFriends.GetFriendPersonaName(steamId), isOwner)
        {
            this.steamId = steamId;
            _metadata = new SteamMetadata(lobby, isPlayerMetadata: true, isLocalPlayer: isLocal);
        }

        internal SteamMetadata GetMetadata() => _metadata;

        internal void RefreshDisplayName()
        {
            var current = SteamFriends.GetFriendPersonaName(steamId);
            if (current == displayName)
                return;

            displayName = current;
            NotifyUpdated();
        }

        private void RequestAvatar()
        {
            if (_avatarRequested)
                return;

            _avatarRequested = true;

            var handle = SteamFriends.GetLargeFriendAvatar(steamId);
            if (handle > 0)
                ApplyAvatarImage(handle);
            // -1 means Steam is still loading it; SteamLobby forwards AvatarImageLoaded_t.
        }

        /// <summary>Called by SteamLobby when AvatarImageLoaded_t fires for this player.</summary>
        internal void OnAvatarLoaded(int imageHandle)
        {
            if (!_avatarRequested)
                return;
            ApplyAvatarImage(imageHandle);
        }

        private void ApplyAvatarImage(int imageHandle)
        {
            if (imageHandle <= 0)
                return;

            if (!SteamUtils.GetImageSize(imageHandle, out var width, out var height))
                return;

            var buffer = new byte[width * height * 4];
            if (!SteamUtils.GetImageRGBA(imageHandle, buffer, buffer.Length))
                return;

            // Steam delivers rows top-to-bottom; Texture2D expects bottom-to-top.
            var flipped = new byte[buffer.Length];
            int stride = (int)width * 4;
            for (int y = 0; y < height; y++)
            {
                System.Array.Copy(buffer, y * stride,
                    flipped, ((int)height - 1 - y) * stride, stride);
            }

            var texture = new Texture2D((int)width, (int)height, TextureFormat.RGBA32, false);
            texture.LoadRawTextureData(flipped);
            texture.Apply();

            _avatar = texture;
            NotifyUpdated();
        }
    }
}
#endif
