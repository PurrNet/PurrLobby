#if NAKAMA
using System;
using System.Threading.Tasks;
using Nakama;
using UnityEngine;

namespace PurrNet.Lobby.Nakama
{
    /// <summary>
    /// Singleton holder for the shared Nakama client, session, and socket.
    /// All Nakama providers read from this instance so a single authenticated session is reused across providers.
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

        private string _clientTarget;

        private NakamaConnection() { }

        public void EnsureClient(NakamaConfig config)
        {
            if (config == null)
                throw new ArgumentNullException(nameof(config));

            var target = $"{config.scheme}://{config.host}:{config.port}/{config.serverKey}";

            if (client != null && _clientTarget == target)
                return;

            client = new Client(config.scheme, config.host, config.port, config.serverKey, UnityWebRequestAdapter.Instance, true);
            _clientTarget = target;
        }

        public async Task<ISession> AuthenticateDeviceAsync(string deviceId, string displayName)
        {
            if (client == null)
                throw new InvalidOperationException("EnsureClient must be called before AuthenticateDeviceAsync.");

            session = await client.AuthenticateDeviceAsync(deviceId, displayName, true);
            onSessionChanged?.Invoke();
            return session;
        }

        /// <summary>Restores a session from cached auth/refresh tokens. Returns false if expired or invalid.</summary>
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

            client = null;
            _clientTarget = null;
            session = null;
            onSessionChanged?.Invoke();
        }

        private void OnSocketConnected() => onSocketConnected?.Invoke();

        private void OnSocketClosed(string reason) => onSocketDisconnected?.Invoke();
    }
}
#endif
