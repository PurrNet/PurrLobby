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

        // With Domain Reload disabled this static singleton outlives a play session with a dead
        // socket/client; drop them on each play-mode boundary so they rebuild (session POCO is kept).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnEnterPlayMode()
        {
            instance.DiscardConnection();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.playModeStateChanged -= instance.OnPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += instance.OnPlayModeStateChanged;
#endif
        }

#if UNITY_EDITOR
        private void OnPlayModeStateChanged(UnityEditor.PlayModeStateChange change)
        {
            if (change == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                DiscardConnection();
        }
#endif

        private void DiscardConnection()
        {
            if (socket != null)
            {
                socket.Closed -= OnSocketClosed;
                socket.Connected -= OnSocketConnected;
                try { _ = socket.CloseAsync(); } catch { /* already dead */ }
                socket = null;
            }
            client = null;
            _clientTarget = null;
        }

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

            if (socket is { IsConnected: true })
                return;

            // A stale socket can't be reliably reconnected; replace it or sends fail with TaskCanceled.
            if (socket != null)
            {
                socket.Closed -= OnSocketClosed;
                socket.Connected -= OnSocketConnected;
                try { await socket.CloseAsync(); } catch { /* already dead */ }
                socket = null;
            }

            socket = client.NewSocket(true);
            socket.Closed += OnSocketClosed;
            socket.Connected += OnSocketConnected;
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
