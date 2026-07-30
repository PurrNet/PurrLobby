#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using Steamworks;
using UnityEngine;

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Wraps a Steam lobby as an <see cref="ILobby"/>. Steam owns the roster and
    /// auto-migrates ownership when the owner leaves; this class mirrors that into
    /// the shared lobby events. Lobby metadata rides Steam lobby data; player
    /// metadata rides a JSON blob in member data; chat rides lobby chat messages.
    /// </summary>
    public class SteamLobby : LobbyBase<SteamPlayer>, IDisposable
    {
        /// <summary>Member-data key holding each player's serialized metadata blob.</summary>
        internal const string MemberDataKey = "purr_meta";

        /// <summary>Reserved lobby-data key mirroring joinability (Steam has no getter for it).</summary>
        internal const string JoinableKey = "purr_joinable";

        /// <summary>Reserved lobby-data key carrying the id of a soft-kicked player.</summary>
        internal const string KickKey = "purr_kick";

        /// <summary>Reserved lobby-data key holding the display name of the lobby.</summary>
        internal const string NameKey = "purr_name";

        public override string id => _lobbyId.m_SteamID.ToString();
        public override int maxPlayers => SteamMatchmaking.GetLobbyMemberLimit(_lobbyId);
        public override IMetadata lobbyData => _lobbyMetadata;
        public override bool isLobbyJoinable => SteamMatchmaking.GetLobbyData(_lobbyId, JoinableKey) != "0";
        public override ILobbyChat chat => _chat;

        internal CSteamID steamLobbyId => _lobbyId;

        private readonly CSteamID _lobbyId;
        private readonly CSteamID _localId;
        private readonly SteamMetadata _lobbyMetadata;
        private readonly SteamChat _chat;

        private readonly Callback<LobbyChatUpdate_t> _chatUpdateCallback;
        private readonly Callback<LobbyDataUpdate_t> _dataUpdateCallback;
        private readonly Callback<LobbyChatMsg_t> _chatMsgCallback;
        private readonly Callback<AvatarImageLoaded_t> _avatarCallback;
        private readonly Callback<PersonaStateChange_t> _personaCallback;

        private byte[] _chatBuffer = new byte[4096];
        private bool _disposed;

        internal SteamLobby(CSteamID lobbyId)
        {
            _lobbyId = lobbyId;
            _localId = SteamRuntime.localSteamId;

            _lobbyMetadata = new SteamMetadata(this, isPlayerMetadata: false, isLocalPlayer: false);
            _chat = new SteamChat(this);

            _chatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            _dataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);
            _chatMsgCallback = Callback<LobbyChatMsg_t>.Create(OnLobbyChatMsg);
            _avatarCallback = Callback<AvatarImageLoaded_t>.Create(OnAvatarImageLoaded);
            _personaCallback = Callback<PersonaStateChange_t>.Create(OnPersonaStateChange);

            SeedRoster();
            SeedLobbyMetadata();
        }

        private void SeedRoster()
        {
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(_lobbyId);
            var ownerId = SteamMatchmaking.GetLobbyOwner(_lobbyId);

            for (int i = 0; i < memberCount; i++)
            {
                var memberId = SteamMatchmaking.GetLobbyMemberByIndex(_lobbyId, i);
                AddSteamPlayer(memberId, ownerId);
            }

            if (TryGetPlayerInternal(ownerId.m_SteamID.ToString(), out var owner))
                SetOwner(owner);
        }

        private void SeedLobbyMetadata()
        {
            var snapshot = ReadFullLobbyData();
            _lobbyMetadata.ReplaceFrom(snapshot);
        }

        private Dictionary<string, string> ReadFullLobbyData()
        {
            var result = new Dictionary<string, string>();
            int count = SteamMatchmaking.GetLobbyDataCount(_lobbyId);

            for (int i = 0; i < count; i++)
            {
                if (!SteamMatchmaking.GetLobbyDataByIndex(_lobbyId, i,
                        out var key, Constants.k_nMaxLobbyKeyLength,
                        out var value, Constants.k_cubChatMetadataMax))
                    continue;

                if (string.IsNullOrEmpty(key) || IsReservedKey(key))
                    continue;

                result[key] = value;
            }

            return result;
        }

        private static bool IsReservedKey(string key) =>
            key == JoinableKey || key == KickKey || key == NameKey;

        private SteamPlayer AddSteamPlayer(CSteamID memberId, CSteamID ownerId)
        {
            bool isLocal = memberId == _localId;
            var player = new SteamPlayer(this, memberId, isOwner: memberId == ownerId, isLocal: isLocal);

            var blob = SteamMatchmaking.GetLobbyMemberData(_lobbyId, memberId, MemberDataKey);
            if (!string.IsNullOrEmpty(blob))
                player.GetMetadata().ReplaceFrom(SteamMetadata.DeserializeBlob(blob));

            AddPlayer(player, isLocal);
            return player;
        }

        public override void KickPlayer(IPlayer player)
        {
            if (player == null)
                return;

            if (!isOwner)
            {
                Debug.LogWarning("[SteamLobby] Only the owner can kick players.");
                return;
            }

            // Steam has no hard kick for lobbies: publish the kicked id; the target
            // leaves on seeing it, and the transport authenticator refuses them.
            SteamMatchmaking.SetLobbyData(_lobbyId, KickKey, player.id);
        }

        public override void SetIsLobbyJoinable(bool isJoinable)
        {
            if (!isOwner)
            {
                Debug.LogWarning("[SteamLobby] Only the owner can change joinability.");
                return;
            }

            SteamMatchmaking.SetLobbyJoinable(_lobbyId, isJoinable);
            SteamMatchmaking.SetLobbyData(_lobbyId, JoinableKey, isJoinable ? "1" : "0");
        }

        public override void LeaveLobby()
        {
            if (_disposed)
                return;

            SteamMatchmaking.LeaveLobby(_lobbyId);
            Dispose();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;

            _chatUpdateCallback.Dispose();
            _dataUpdateCallback.Dispose();
            _chatMsgCallback.Dispose();
            _avatarCallback.Dispose();
            _personaCallback.Dispose();
        }

        private void OnLobbyChatUpdate(LobbyChatUpdate_t evt)
        {
            if (_disposed || evt.m_ulSteamIDLobby != _lobbyId.m_SteamID)
                return;

            var changedId = new CSteamID(evt.m_ulSteamIDUserChanged);
            var change = (EChatMemberStateChange)evt.m_rgfChatMemberStateChange;

            if ((change & EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
            {
                if (!TryGetPlayerInternal(changedId.m_SteamID.ToString(), out _))
                    AddSteamPlayer(changedId, SteamMatchmaking.GetLobbyOwner(_lobbyId));
            }
            else
            {
                var leftId = changedId.m_SteamID.ToString();
                RemovePlayer(leftId, out _);

                // The kick is complete once the target leaves; clear the key so it
                // doesn't linger as an accidental ban (or block a deliberate re-invite).
                if (isOwner && SteamMatchmaking.GetLobbyData(_lobbyId, KickKey) == leftId)
                    SteamMatchmaking.DeleteLobbyData(_lobbyId, KickKey);
            }

            RefreshOwner();
        }

        private void OnLobbyDataUpdate(LobbyDataUpdate_t evt)
        {
            if (_disposed || evt.m_ulSteamIDLobby != _lobbyId.m_SteamID)
                return;

            if (evt.m_ulSteamIDMember == evt.m_ulSteamIDLobby)
            {
                // Lobby data changed: re-read everything and diff.
                _lobbyMetadata.ReplaceFrom(ReadFullLobbyData());

                RefreshOwner();
                CheckKicked();
            }
            else
            {
                // A member's data changed: re-parse their blob.
                var memberId = new CSteamID(evt.m_ulSteamIDMember);
                if (!TryGetPlayerInternal(memberId.m_SteamID.ToString(), out var player))
                    return;

                var blob = SteamMatchmaking.GetLobbyMemberData(_lobbyId, memberId, MemberDataKey);
                player.GetMetadata().ReplaceFrom(SteamMetadata.DeserializeBlob(blob));
                RaisePlayerMetadataUpdated(player);
            }
        }

        private void RefreshOwner()
        {
            var ownerId = SteamMatchmaking.GetLobbyOwner(_lobbyId);
            TryGetPlayerInternal(ownerId.m_SteamID.ToString(), out var newOwner);

            if (newOwner != ownerInternal)
                SetOwner(newOwner);
        }

        private void CheckKicked()
        {
            var kicked = SteamMatchmaking.GetLobbyData(_lobbyId, KickKey);
            if (string.IsNullOrEmpty(kicked) || localPlayerInternal == null)
                return;

            if (kicked == localPlayerInternal.id)
            {
                RaiseLobbyDestroyed();
                LeaveLobby();
            }
        }

        private void OnLobbyChatMsg(LobbyChatMsg_t evt)
        {
            if (_disposed || evt.m_ulSteamIDLobby != _lobbyId.m_SteamID)
                return;

            int length = SteamMatchmaking.GetLobbyChatEntry(_lobbyId, (int)evt.m_iChatID,
                out var senderId, _chatBuffer, _chatBuffer.Length, out _);

            if (length <= 0)
                return;

            if (length >= _chatBuffer.Length)
            {
                // Message was truncated; grow the buffer and re-read.
                _chatBuffer = new byte[Mathf.NextPowerOfTwo(length + 1)];
                length = SteamMatchmaking.GetLobbyChatEntry(_lobbyId, (int)evt.m_iChatID,
                    out senderId, _chatBuffer, _chatBuffer.Length, out _);
                if (length <= 0)
                    return;
            }

            if (!TryGetPlayerInternal(senderId.m_SteamID.ToString(), out var sender))
                return;

            var data = new byte[length];
            Array.Copy(_chatBuffer, data, length);
            _chat.DispatchIncoming(sender, data);
        }

        private void OnAvatarImageLoaded(AvatarImageLoaded_t evt)
        {
            if (_disposed)
                return;

            if (TryGetPlayerInternal(evt.m_steamID.m_SteamID.ToString(), out var player))
                player.OnAvatarLoaded(evt.m_iImage);
        }

        private void OnPersonaStateChange(PersonaStateChange_t evt)
        {
            if (_disposed)
                return;

            if (TryGetPlayerInternal(evt.m_ulSteamID.ToString(), out var player))
                player.RefreshDisplayName();
        }
    }
}
#endif
