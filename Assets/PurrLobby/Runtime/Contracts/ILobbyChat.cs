using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace PurrNet.Lobby
{
    public interface ILobbyChat
    {
        void SendMessage(byte[] data);

        event Action<IPlayer, byte[]> onMessageReceived;
    }

    /// <summary>
    /// Shared chat implementation with optimistic local loopback. Outbound messages
    /// are raised locally before being sent to the provider, while a matching
    /// provider echo from the local player is consumed so listeners see it once.
    /// </summary>
    public abstract class LobbyChatBase : ILobbyChat
    {
        private const int MaxPendingLocalMessages = 128;
        private const double PendingEchoLifetimeSeconds = 30d;

        private readonly object _pendingGate = new();
        private readonly List<PendingMessage> _pendingLocalMessages = new();

        /// <summary>The provider's current local player, once its roster is ready.</summary>
        protected abstract IPlayer localPlayer { get; }

        public event Action<IPlayer, byte[]> onMessageReceived;

        public void SendMessage(byte[] data)
        {
            if (data == null || data.Length == 0)
                return;

            // Own the payload so neither the caller nor a local event listener can
            // mutate the bytes that are sent to the provider or used for deduping.
            var outbound = (byte[])data.Clone();
            var localPlayer = this.localPlayer;

            if (localPlayer != null)
            {
                RememberPending(outbound);
                onMessageReceived?.Invoke(localPlayer, (byte[])outbound.Clone());
            }

            SendToProvider(outbound);
        }

        /// <summary>Sends a locally-looped message through the provider.</summary>
        protected abstract void SendToProvider(byte[] data);

        /// <summary>
        /// Dispatches a message received from the provider. Matching echoes from the
        /// local player are consumed; remote and unmatched local messages are raised.
        /// </summary>
        protected void ReceiveFromProvider(IPlayer sender, byte[] data)
        {
            if (sender == null || data == null || data.Length == 0)
                return;

            var localPlayer = this.localPlayer;
            if (IsSamePlayer(sender, localPlayer) && ConsumePending(data))
                return;

            onMessageReceived?.Invoke(sender, data);
        }

        private void RememberPending(byte[] data)
        {
            lock (_pendingGate)
            {
                PrunePending();

                if (_pendingLocalMessages.Count >= MaxPendingLocalMessages)
                    _pendingLocalMessages.RemoveAt(0);

                _pendingLocalMessages.Add(new PendingMessage(
                    (byte[])data.Clone(), Stopwatch.GetTimestamp()));
            }
        }

        private bool ConsumePending(byte[] data)
        {
            lock (_pendingGate)
            {
                PrunePending();

                for (int i = 0; i < _pendingLocalMessages.Count; i++)
                {
                    if (!BytesEqual(_pendingLocalMessages[i].data, data))
                        continue;

                    _pendingLocalMessages.RemoveAt(i);
                    return true;
                }

                return false;
            }
        }

        private void PrunePending()
        {
            var now = Stopwatch.GetTimestamp();
            var maxAge = PendingEchoLifetimeSeconds * Stopwatch.Frequency;

            for (int i = _pendingLocalMessages.Count - 1; i >= 0; i--)
            {
                if (now - _pendingLocalMessages[i].createdAt <= maxAge)
                    continue;

                _pendingLocalMessages.RemoveAt(i);
            }
        }

        private static bool IsSamePlayer(IPlayer first, IPlayer second)
        {
            if (first == null || second == null)
                return false;
            if (ReferenceEquals(first, second))
                return true;
            return !string.IsNullOrEmpty(first.id) && first.id == second.id;
        }

        private static bool BytesEqual(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
                return false;

            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] != second[i])
                    return false;
            }

            return true;
        }

        private readonly struct PendingMessage
        {
            public readonly byte[] data;
            public readonly long createdAt;

            public PendingMessage(byte[] data, long createdAt)
            {
                this.data = data;
                this.createdAt = createdAt;
            }
        }
    }
}
