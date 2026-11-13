using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using PurrNet.Logging;
using PurrNet.Packing;

namespace PurrNet.Lobby
{
    /// <summary>Server to one client: the nonce that client writes to its own member data.</summary>
    public struct LobbyPlayerChallenge : IPackedAuto
    {
        public string nonce;
    }

    /// <summary>Client to server: the lobby player this connection says it is. Checked, never trusted.</summary>
    public struct LobbyPlayerClaim : IPackedAuto
    {
        public string lobbyPlayerId;
    }

    /// <summary>
    /// On the game server, links game connections (<see cref="PlayerID"/>) to the lobby players they belong to,
    /// without taking the client's word for it.
    /// <para>
    /// Every lobby member can read every other member's id, so a claimed id proves nothing. What a member can't
    /// fake is another member's own member data: the lobby service only lets a member write its own. So the server
    /// sends each new connection a random nonce (<see cref="Challenge"/>); the client writes that nonce to its own
    /// member data under <see cref="ProofKey"/> and claims its lobby id (<see cref="Claim"/>). The connection is
    /// bound once the claimed member's data shows the nonce sent to that connection (<see cref="Update"/>).
    /// A nonce is only good for the connection it was sent to, so copying another member's nonce or replaying an
    /// old one binds nothing.
    /// </para>
    /// <see cref="GameSession"/> drives this over the game connection; use
    /// <see cref="GameSession.TryGetLobbyPlayer"/> and <see cref="GameSession.TryGetPlayerID"/> to read it.
    /// </summary>
    public sealed class LobbyPlayerBindings
    {
        /// <summary>The member-data key a client writes its connection's nonce to.</summary>
        public const string ProofKey = "PURRLOBBY_GAME_PROOF";

        /// <summary>Default time a connection has to prove its lobby player, from its challenge.</summary>
        public const float DefaultTimeout = 30f;

        private struct Pending
        {
            public string nonce;
            public string claimedId;
            public float deadline;
        }

        private readonly ILobby _lobby;
        private readonly float _timeout;
        private readonly Dictionary<PlayerID, Pending> _pending = new();
        private readonly Dictionary<PlayerID, IPlayer> _byConnection = new();
        private readonly Dictionary<string, PlayerID> _byLobbyId = new();
        private readonly List<PlayerID> _scratch = new();

        /// <summary>A connection was bound to a lobby player (also on a rebind after a reconnect).</summary>
        public event Action<PlayerID, IPlayer> onBound;

        /// <summary>A binding ended: the connection left, the member left the lobby, or the member reconnected.</summary>
        public event Action<PlayerID, IPlayer> onUnbound;

        /// <summary>A connection didn't prove a lobby player in time. The string is its claimed id, if any.</summary>
        public event Action<PlayerID, string> onBindingFailed;

        public ILobby lobby => _lobby;

        public LobbyPlayerBindings(ILobby lobby, float timeout = DefaultTimeout)
        {
            _lobby = lobby ?? throw new ArgumentNullException(nameof(lobby));
            _timeout = timeout;
        }

        /// <summary>Server: starts proving a new connection. Send the returned nonce to that connection only.</summary>
        public string Challenge(PlayerID player, float now)
        {
            Unbind(player);
            var nonce = NewNonce();
            _pending[player] = new Pending { nonce = nonce, deadline = now + _timeout };
            return nonce;
        }

        /// <summary>Server: the lobby id a connection claims. Only its first claim after a challenge counts.</summary>
        public void Claim(PlayerID player, string lobbyPlayerId)
        {
            if (string.IsNullOrEmpty(lobbyPlayerId) || !_pending.TryGetValue(player, out var pending) ||
                pending.claimedId != null)
                return;

            pending.claimedId = lobbyPlayerId;
            _pending[player] = pending;
        }

        /// <summary>Server: binds the listen host's own connection to the local lobby player.</summary>
        public void BindLocal(PlayerID player)
        {
            var local = _lobby.localPlayer;
            if (local == null || string.IsNullOrEmpty(local.id))
                return;

            _pending.Remove(player);
            Bind(player, local);
        }

        /// <summary>Server: the connection left the game.</summary>
        public void Remove(PlayerID player)
        {
            _pending.Remove(player);
            Unbind(player);
        }

        /// <summary>Server: a member left the lobby; its connection no longer belongs to a lobby player.</summary>
        public void OnLobbyPlayerLeft(IPlayer member)
        {
            if (member != null && !string.IsNullOrEmpty(member.id) && _byLobbyId.TryGetValue(member.id, out var player))
                Unbind(player);
        }

        /// <summary>Server: binds connections whose claimed member now shows their nonce, and expires the rest.</summary>
        public void Update(float now)
        {
            if (_pending.Count == 0)
                return;

            _scratch.Clear();
            foreach (var kvp in _pending)
                _scratch.Add(kvp.Key);

            for (int i = 0; i < _scratch.Count; i++)
            {
                var player = _scratch[i];
                var pending = _pending[player];

                if (pending.claimedId != null && IsProven(pending))
                {
                    _pending.Remove(player);
                    _lobby.TryGetPlayer(pending.claimedId, out var member);
                    Bind(player, member);
                    continue;
                }

                if (now <= pending.deadline)
                    continue;

                _pending.Remove(player);
                PurrLogger.LogWarning($"Game connection {player} did not prove its lobby player " +
                                      $"(claimed `{pending.claimedId ?? "nothing"}`) in time; it stays unbound.");
                onBindingFailed?.Invoke(player, pending.claimedId);
            }

            _scratch.Clear();
        }

        /// <summary>The lobby player a game connection belongs to, once proven.</summary>
        public bool TryGetLobbyPlayer(PlayerID player, out IPlayer lobbyPlayer) =>
            _byConnection.TryGetValue(player, out lobbyPlayer);

        /// <summary>The game connection of a lobby player, once proven.</summary>
        public bool TryGetPlayerID(string lobbyPlayerId, out PlayerID player)
        {
            if (!string.IsNullOrEmpty(lobbyPlayerId))
                return _byLobbyId.TryGetValue(lobbyPlayerId, out player);
            player = default;
            return false;
        }

        /// <summary>True while a connection has been challenged but not yet bound or expired.</summary>
        public bool IsPending(PlayerID player) => _pending.ContainsKey(player);

        /// <summary>Client: answers a challenge by writing the nonce to the local player's own member data.</summary>
        public static bool Answer(ILobby lobby, string nonce)
        {
            var data = lobby?.localPlayer?.userData;
            if (data == null || string.IsNullOrEmpty(nonce))
                return false;

            data.SetData(ProofKey, nonce);
            return true;
        }

        private bool IsProven(Pending pending)
        {
            // Never rebind the server's own member from a remote connection.
            var local = _lobby.localPlayer;
            if (local != null && local.id == pending.claimedId)
                return false;

            return _lobby.TryGetPlayer(pending.claimedId, out var member) && member?.userData != null &&
                   member.userData.TryGetData(ProofKey, out var shown) && shown == pending.nonce;
        }

        private void Bind(PlayerID player, IPlayer member)
        {
            if (member == null)
                return;

            Unbind(player);

            // The member proved itself on a new connection: that's where it is now.
            if (_byLobbyId.TryGetValue(member.id, out var previous))
                Unbind(previous);

            _byConnection[player] = member;
            _byLobbyId[member.id] = player;
            onBound?.Invoke(player, member);
        }

        private void Unbind(PlayerID player)
        {
            if (!_byConnection.Remove(player, out var member))
                return;

            if (_byLobbyId.TryGetValue(member.id, out var bound) && bound.Equals(player))
                _byLobbyId.Remove(member.id);

            onUnbound?.Invoke(player, member);
        }

        private static string NewNonce()
        {
            var bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return BitConverter.ToString(bytes).Replace("-", string.Empty);
        }
    }
}
