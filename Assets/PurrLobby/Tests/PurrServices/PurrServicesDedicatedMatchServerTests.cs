#if PURR_SERVICES_MATCHMAKING
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PurrNet.Lobby.PurrNet;
using PurrNet.Services;
using PurrNet.Transports;
using UnityEngine;
using Object = UnityEngine.Object;

namespace PurrNet.Lobby.Tests
{
    public class PurrServicesDedicatedMatchServerTests
    {
        class PendingRenewals : ServiceHttp
        {
            public readonly Queue<TaskCompletionSource<HttpResult<MatchmakingClaim>>> responses = new();
            public int calls;
            public PendingRenewals() : base(() => "https://example.invalid", () => "sk_test", () => "test",
                () => "test", () => null, () => null) { }

            public override async Task<HttpResult<T>> SendAsync<T>(string method, string path, object body,
                CancellationToken cancellationToken = default)
            {
                Assert.AreEqual("/api/matchmaking/server/claims", path,
                    "A cancelled allocation must never publish a connection.");
                calls++;
                // Intentionally ignore cancellation to reproduce late HTTP.
                return (HttpResult<T>)(object)await responses.Dequeue().Task;
            }
        }

        static MatchmakingClaim Claim() => new()
        {
            match = new MatchmakingMatchData { id = Guid.NewGuid().ToString(), hosting = "dedicated", state = "allocating" },
            claimToken = "test-claim"
        };

        [Test]
        public async Task ReplacedClaim_LateRenewalCannotStartListenerOrPublish()
        {
            var gameObject = new GameObject("Dedicated claim lifecycle test");
            gameObject.SetActive(false);
            var manager = gameObject.AddComponent<NetworkManager>();
            var server = gameObject.AddComponent<PurrServicesDedicatedMatchServer>();
            var http = new PendingRenewals();
            var oldResponse = new TaskCompletionSource<HttpResult<MatchmakingClaim>>();
            var newResponse = new TaskCompletionSource<HttpResult<MatchmakingClaim>>();
            http.responses.Enqueue(oldResponse);
            http.responses.Enqueue(newResponse);
            var client = new MatchmakingServerClient(http);
            var oldClaim = Claim();
            var newClaim = Claim();
            var endpoint = new MatchmakingConnection { serverAddress = "192.0.2.1", serverPort = 7777 };
            try
            {
                var oldStartup = server.StartClaimAsync(client, oldClaim, "worker-old", endpoint);
                Assert.AreEqual(1, http.calls);
                server.StopLocal();
                var newStartup = server.StartClaimAsync(client, newClaim, "worker-new", endpoint);
                Assert.AreEqual(2, http.calls);
                oldResponse.SetResult(new HttpResult<MatchmakingClaim> { success = true, data = oldClaim });
                try { await oldStartup; Assert.Fail("Cancelled startup unexpectedly succeeded."); }
                catch (OperationCanceledException) { }
                Assert.AreEqual(newClaim.match.id, server.matchId);
                Assert.IsFalse(server.isActive);
                Assert.AreEqual(ConnectionState.Disconnected, manager.serverState);
                Assert.AreEqual(2, http.calls);
                server.StopLocal();
                newResponse.SetResult(new HttpResult<MatchmakingClaim> { success = true, data = newClaim });
                try { await newStartup; Assert.Fail("Cancelled startup unexpectedly succeeded."); }
                catch (OperationCanceledException) { }
            }
            finally
            {
                server.StopLocal();
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
#endif
