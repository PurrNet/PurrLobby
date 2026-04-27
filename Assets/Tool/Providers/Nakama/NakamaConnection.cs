#if NAKAMA
using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Singleton holder for the Nakama <see cref="IClient"/>, <see cref="ISession"/> and <see cref="ISocket"/>.
    /// Mirrors the role played by <c>PurrServices</c> in the PurrNet provider — every Nakama provider asset
    /// reads from a single shared connection so a session/socket established by the SessionProvider is
    /// reused by the LobbyProvider, MatchmakingProvider, etc. Persistence policy (PlayerPrefs caching of
    /// auth tokens) is owned by <see cref="NakamaSessionProvider"/>; this class only knows how to import
    /// and export raw token pairs.
    /// </summary>
    public sealed class NakamaConnection
    {
        public static NakamaConnection instance { get; } = new NakamaConnection();

        public IClient client { get; private set; }
        public ISession session { get; private set; }
        public ISocket socket { get; private set; }

        public bool isAuthenticated => session != null && !session.IsExpired;
        public bool isSocketConnected => socket != null && socket.IsConnected;

        public string userId => session?.UserId;
        public string username => session?.Username;

        public string authToken => session?.AuthToken;
        public string refreshToken => session?.RefreshToken;

        public event Action onSessionChanged;
        public event Action onSocketConnected;
        public event Action onSocketDisconnected;

        private NakamaConnection() { }

        public void EnsureClient(NakamaConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            if (client != null)
                return;

            client = new Client(config.scheme, config.host, config.port, config.serverKey, UnityWebRequestAdapter.Instance, true);
        }

        public async Task<ISession> AuthenticateDeviceAsync(string deviceId, string displayName)
        {
            if (client == null)
                throw new InvalidOperationException("EnsureClient must be called before AuthenticateDeviceAsync.");

            session = await client.AuthenticateDeviceAsync(deviceId, displayName, true);
            onSessionChanged?.Invoke();
            return session;
        }

        /// <summary>
        /// Adopts a previously-issued session given its auth + refresh tokens. Returns false if the tokens
        /// are missing, malformed, or the resulting session is already expired.
        /// </summary>
        public bool TryRestoreFromTokens(string auth, string refresh)
        {
            if (string.IsNullOrEmpty(auth))
                return false;
            try
            {
                var restored = Session.Restore(auth, refresh);
                if (restored == null || restored.IsExpired)
                    return false;
                session = restored;
                onSessionChanged?.Invoke();
                return true;
            }
            catch
            {
                return false;
            }
        }

        public async Task EnsureSocketAsync()
        {
            if (session == null)
                throw new InvalidOperationException("Authenticate before opening a socket.");

            switch (socket)
            {
                case { IsConnected: true }:
                    return;
                case null:
                    socket = client.NewSocket(true);
                    socket.Closed += OnSocketClosed;
                    socket.Connected += OnSocketConnected;
                    break;
            }

            await socket.ConnectAsync(session, true);
        }

        public async Task LogoutAsync()
        {
            if (socket != null)
            {
                try { await socket.CloseAsync(); }
                catch (Exception ex) { Debug.LogWarning($"[Nakama] Socket close failed: {ex.Message}"); }
                socket.Closed -= OnSocketClosed;
                socket.Connected -= OnSocketConnected;
                socket = null;
            }

            if (client != null && session != null)
            {
                try { await client.SessionLogoutAsync(session); }
                catch (Exception ex) { Debug.LogWarning($"[Nakama] Session logout failed: {ex.Message}"); }
            }

            session = null;
            onSessionChanged?.Invoke();
        }

        private void OnSocketConnected() => onSocketConnected?.Invoke();

        private void OnSocketClosed(string reason) => onSocketDisconnected?.Invoke();
    }
}
#endif
