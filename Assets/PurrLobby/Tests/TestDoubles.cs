using System;
using System.Collections.Generic;

namespace PurrNet.Lobby.Tests
{
    /// <summary>Metadata double: records every PushChange and lets tests toggle write access.</summary>
    public class TestMetadata : LobbyMetadataBase
    {
        public bool canWrite = true;

        public readonly List<(string key, string value)> pushedChanges = new();

        protected override bool CanWrite => canWrite;

        protected override void PushChange(string key, string value)
        {
            pushedChanges.Add((key, value));
        }
    }

    public class TestPlayer : LobbyPlayerBase
    {
        private readonly TestMetadata _metadata = new();

        public override IMetadata userData => _metadata;

        public TestPlayer(string id, string displayName = null, bool isOwner = false)
            : base(id, displayName ?? id, isOwner)
        {
        }
    }

    public class TestChat : LobbyChatBase
    {
        public IPlayer localPlayerValue;
        public readonly List<byte[]> sentMessages = new();
        public Action<byte[]> onSend;

        protected override IPlayer localPlayer => localPlayerValue;

        public TestChat(IPlayer localPlayer = null)
        {
            localPlayerValue = localPlayer;
        }

        protected override void SendToProvider(byte[] data)
        {
            sentMessages.Add(data);
            onSend?.Invoke(data);
        }

        public void ReceiveFromProviderPublic(IPlayer sender, byte[] data) =>
            ReceiveFromProvider(sender, data);
    }

    /// <summary>Lobby double exposing the protected roster/owner API for tests.</summary>
    public class TestLobby : LobbyBase<TestPlayer>
    {
        private readonly TestMetadata _lobbyData = new();

        public override string id => "test-lobby";
        public override int maxPlayers => 4;
        public override IMetadata lobbyData => _lobbyData;
        public override bool isLobbyJoinable => true;
        public override ILobbyChat chat => null;

        public override void KickPlayer(IPlayer player) { }
        public override void SetIsLobbyJoinable(bool isJoinable) { }
        public override void LeaveLobby() { }

        public void AddPlayerPublic(TestPlayer player, bool isLocal = false) => AddPlayer(player, isLocal);
        public bool RemovePlayerPublic(string playerId, out TestPlayer removed) => RemovePlayer(playerId, out removed);
        public bool TryGetPlayerPublic(string playerId, out TestPlayer player) => TryGetPlayerInternal(playerId, out player);
        public void SetOwnerPublic(TestPlayer newOwner) => SetOwner(newOwner);
        public void RaisePlayerUpdatedPublic(TestPlayer player) => RaisePlayerUpdated(player);
        public void RaisePlayerMetadataUpdatedPublic(TestPlayer player) => RaisePlayerMetadataUpdated(player);
        public void RaiseLobbyDestroyedPublic() => RaiseLobbyDestroyed();
    }

    /// <summary>Matchmaking double exposing the protected ticket helpers for tests.</summary>
    public class TestMatchmakingProvider : MatchmakingProvider
    {
        public override System.Threading.Tasks.Task<MatchmakingTicketResponse> StartMatchmaking(MatchmakingRequest request) =>
            System.Threading.Tasks.Task.FromResult(MatchmakingTicketResponse.Failure("not implemented"));

        public override System.Threading.Tasks.Task<APIResponse> CancelMatchmaking(MatchmakingTicket ticket) =>
            System.Threading.Tasks.Task.FromResult(APIResponse.Failure("not implemented"));

        public MatchmakingTicket? activeTicketPublic => activeTicket;
        public MatchmakingTicket BeginTicketPublic(string externalId = null) => BeginTicket(externalId);
        public bool IsStalePublic(MatchmakingTicket ticket) => IsStale(ticket);
        public bool TryConsumeActiveTicketPublic(MatchmakingTicket ticket) => TryConsumeActiveTicket(ticket);
        public void CompleteMatchPublic(MatchmakingTicket ticket, MatchResult result) => CompleteMatch(ticket, result);
        public void FailMatchPublic(MatchmakingTicket ticket, string error) => FailMatch(ticket, error);
        public void CancelLocallyPublic(MatchmakingTicket ticket) => CancelLocally(ticket);
    }

    public class TestHostMigrationLobbyConnection : HostMigrationLobbyConnection
    {
        public bool hasActiveLobbyConnectionPublic => hasActiveLobbyConnection;

        protected override void ConfigureTransportForHost(ILobby lobby, IPlayer host) { }
    }
}
