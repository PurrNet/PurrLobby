#if !(UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX)
#define DISABLESTEAMWORKS
#endif
#if STEAMWORKS && !DISABLESTEAMWORKS
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace PurrNet.Lobby.Steam
{
    /// <summary>
    /// Lobby provider backed by Steam lobbies. The lobby's SteamID64 doubles as
    /// its id and join code. Supports the full capability set: create, join by
    /// id/code, browse, and quick join.
    /// </summary>
    [CreateAssetMenu(menuName = "PurrLobby/Steam/Lobby Provider", fileName = "Steam Lobby Provider", order = -203)]
    public class SteamLobbyProvider : LobbyProvider
    {
        [Tooltip("Default max players for newly created lobbies.")]
        [SerializeField] private int _maxPlayers = 4;

        public override int maxPlayer => _maxPlayers;

        public override LobbyCapabilities capabilities => LobbyCapabilities.All;

        private Callback<GameLobbyJoinRequested_t> _overlayJoinCallback;

        public override Task Initialize()
        {
            if (SteamRuntime.EnsureInitialized())
            {
                // Recreate unconditionally: SteamAPI.Shutdown unregisters all callbacks,
                // and this ScriptableObject field survives play sessions when domain
                // reload is disabled — a stale instance would silently never fire.
                _overlayJoinCallback?.Dispose();
                _overlayJoinCallback = Callback<GameLobbyJoinRequested_t>.Create(evt =>
                    RaiseExternalJoinRequested(evt.m_steamIDLobby.m_SteamID.ToString()));
            }

            return Task.CompletedTask;
        }

        public override Task Logout()
        {
            _overlayJoinCallback?.Dispose();
            _overlayJoinCallback = null;
            return Task.CompletedTask;
        }

        public override async Task<LobbyResponse> CreateLobby(LobbySettings settings)
        {
            if (!SteamRuntime.EnsureInitialized())
                return LobbyResponse.Failure(SteamUnavailable);

            var lobbyType = settings.visibility == LobbyVisibility.Public
                ? ELobbyType.k_ELobbyTypePublic
                : ELobbyType.k_ELobbyTypeFriendsOnly; // Private still allows friend invites via the overlay.

            var maxPlayers = settings.maxPlayers > 0 ? settings.maxPlayers : _maxPlayers;

            try
            {
                var created = await SteamCallResults.ToTask<LobbyCreated_t>(
                    SteamMatchmaking.CreateLobby(lobbyType, maxPlayers));

                if (created.m_eResult != EResult.k_EResultOK)
                    return LobbyResponse.Failure($"Steam lobby creation failed: {created.m_eResult}");

                var lobbyId = new CSteamID(created.m_ulSteamIDLobby);

                if (!string.IsNullOrEmpty(settings.name))
                    SteamMatchmaking.SetLobbyData(lobbyId, SteamLobby.NameKey, settings.name);

                SteamMatchmaking.SetLobbyData(lobbyId, SteamLobby.JoinableKey, "1");

                var lobby = new SteamLobby(lobbyId);

                if (settings.metadata != null)
                {
                    foreach (var kvp in settings.metadata)
                        lobby.lobbyData.SetData(kvp.Key, kvp.Value);
                }

                return LobbyResponse.Success(lobby);
            }
            catch (Exception e)
            {
                return LobbyResponse.Failure($"Steam lobby creation failed: {e.Message}");
            }
        }

        public override async Task<LobbyResponse> JoinLobby(string lobbyId)
        {
            if (!SteamRuntime.EnsureInitialized())
                return LobbyResponse.Failure(SteamUnavailable);

            if (!ulong.TryParse(lobbyId, out var rawId))
                return LobbyResponse.Failure("Invalid Steam lobby id.");

            try
            {
                var steamLobbyId = new CSteamID(rawId);
                var entered = await SteamCallResults.ToTask<LobbyEnter_t>(
                    SteamMatchmaking.JoinLobby(steamLobbyId));

                var response = (EChatRoomEnterResponse)entered.m_EChatRoomEnterResponse;
                if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
                    return LobbyResponse.Failure(DescribeEnterResponse(response));

                return LobbyResponse.Success(new SteamLobby(steamLobbyId));
            }
            catch (Exception e)
            {
                return LobbyResponse.Failure($"Failed to join Steam lobby: {e.Message}");
            }
        }

        public override Task<LobbyResponse> JoinLobbyByCode(string code) => JoinLobby(code);

        public override async Task<LobbyResponse> JoinRandom(LobbyQuery query = null)
        {
            var list = await QueryLobbies(query);

            if (!list.success)
                return LobbyResponse.Failure(list.error);

            if (list.lobbies == null || list.lobbies.Count == 0)
                return LobbyResponse.Failure("No matching lobbies found.");

            return await JoinLobby(list.lobbies[0].id);
        }

        public override async Task<LobbyCollectionResponse> QueryLobbies(LobbyQuery query = null)
        {
            if (!SteamRuntime.EnsureInitialized())
                return LobbyCollectionResponse.Failure(SteamUnavailable);

            try
            {
                ApplyQueryFilters(query);

                var matchList = await SteamCallResults.ToTask<LobbyMatchList_t>(
                    SteamMatchmaking.RequestLobbyList());

                var lobbies = new List<LobbyInfo>();

                for (int i = 0; i < matchList.m_nLobbiesMatching; i++)
                {
                    var lobbyId = SteamMatchmaking.GetLobbyByIndex(i);
                    if (!lobbyId.IsValid())
                        continue;

                    // Filtered client-side rather than via a lobby-list filter: Steam's
                    // behavior for lobbies missing the key is undocumented, and lobbies
                    // created outside this provider won't carry it.
                    bool joinable = SteamMatchmaking.GetLobbyData(lobbyId, SteamLobby.JoinableKey) != "0";
                    if (!joinable)
                        continue;

                    var idString = lobbyId.m_SteamID.ToString();

                    lobbies.Add(new LobbyInfo
                    {
                        id = idString,
                        code = idString,
                        name = SteamMatchmaking.GetLobbyData(lobbyId, SteamLobby.NameKey),
                        playerCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId),
                        maxPlayers = SteamMatchmaking.GetLobbyMemberLimit(lobbyId),
                        joinable = true,
                        // Non-members can only read individual keys; expose the queried ones.
                        metadata = ReadQueriedMetadata(lobbyId, query)
                    });
                }

                return LobbyCollectionResponse.Success(lobbies);
            }
            catch (Exception e)
            {
                return LobbyCollectionResponse.Failure($"Steam lobby query failed: {e.Message}");
            }
        }

        private static void ApplyQueryFilters(LobbyQuery query)
        {
            if (query == null)
                return;

            if (query.stringFilters != null)
            {
                foreach (var f in query.stringFilters)
                    SteamMatchmaking.AddRequestLobbyListStringFilter(f.key, f.value, ToSteamComparison(f.op));
            }

            if (query.numericalFilters != null)
            {
                foreach (var f in query.numericalFilters)
                    SteamMatchmaking.AddRequestLobbyListNumericalFilter(f.key, f.value, ToSteamComparison(f.op));
            }

            if (query.nearValueSorts != null)
            {
                foreach (var s in query.nearValueSorts)
                    SteamMatchmaking.AddRequestLobbyListNearValueFilter(s.key, s.target);
            }

            if (query.slotsAvailable > 0)
                SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(query.slotsAvailable);
        }

        private static Dictionary<string, string> ReadQueriedMetadata(CSteamID lobbyId, LobbyQuery query)
        {
            var metadata = new Dictionary<string, string>();

            if (query?.stringFilters != null)
            {
                foreach (var f in query.stringFilters)
                    metadata[f.key] = SteamMatchmaking.GetLobbyData(lobbyId, f.key);
            }

            return metadata;
        }

        private static ELobbyComparison ToSteamComparison(FilterComparison op)
        {
            switch (op)
            {
                case FilterComparison.Equal: return ELobbyComparison.k_ELobbyComparisonEqual;
                case FilterComparison.NotEqual: return ELobbyComparison.k_ELobbyComparisonNotEqual;
                case FilterComparison.LessThan: return ELobbyComparison.k_ELobbyComparisonLessThan;
                case FilterComparison.GreaterThan: return ELobbyComparison.k_ELobbyComparisonGreaterThan;
                case FilterComparison.LessThanOrEqual: return ELobbyComparison.k_ELobbyComparisonEqualToOrLessThan;
                case FilterComparison.GreaterThanOrEqual: return ELobbyComparison.k_ELobbyComparisonEqualToOrGreaterThan;
                default: return ELobbyComparison.k_ELobbyComparisonEqual;
            }
        }

        private static string DescribeEnterResponse(EChatRoomEnterResponse response)
        {
            switch (response)
            {
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist:
                    return "That lobby no longer exists.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull:
                    return "The lobby is full.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseNotAllowed:
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned:
                    return "You are not allowed to join that lobby.";
                case EChatRoomEnterResponse.k_EChatRoomEnterResponseLimited:
                    return "Limited Steam accounts cannot join lobbies.";
                default:
                    return $"Failed to join the lobby ({response}).";
            }
        }

        private const string SteamUnavailable =
            "Steam is not running or SteamAPI failed to initialize.";
    }
}
#endif
