#if PURR_SERVICES_MATCHMAKING
using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PurrNet.Lobby.PurrNet;
using PurrNet.Services;
using PurrNet.Transports;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace PurrNet.Lobby.Tests
{
    /// <summary>
    /// Exercises the actual UDP/authentication wire path, including generated
    /// runtime serializers. The verifier is local and held pending deliberately.
    /// </summary>
    public class PurrServicesMatchTransportTests
    {
        const string MatchId = "92db3997-e783-43f4-b6db-d691a8fa51d7";
        const string PlayerId = "loopback-player";
        const string Credential = "local-test-only-admission";
        const string EnvironmentState = "PurrLobby.MatchTransportTests.CI";
        GameObject _root;
        NetworkManager _manager;
        NetworkRules _rules;
        UDPTransport _transport;
        PurrServicesMatchAuthenticator _admission;
        TaskCompletionSource<HttpResult<MatchmakingAdmission>> _verification;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            // PurrNet already disables telemetry in batch mode. Also keep this
            // test strictly local when run manually; SessionState survives the
            // Enter/ExitPlayMode domain reloads so the old process value is restored.
            if (!SessionState.GetBool(EnvironmentState + ".owned", false))
            {
                var previous = Environment.GetEnvironmentVariable("CI");
                SessionState.SetBool(EnvironmentState + ".owned", true);
                SessionState.SetBool(EnvironmentState + ".existed", previous != null);
                SessionState.SetString(EnvironmentState, previous ?? string.Empty);
                Environment.SetEnvironmentVariable("CI", "true");
            }
            yield return new EnterPlayMode();

            Assert.IsFalse(NetworkManager.main,
                "The loopback fixture requires the test runner's empty scene, without an existing NetworkManager.");
            _root = new GameObject("PurrLobby matchmaking loopback test");
            _root.SetActive(false);
            _transport = _root.AddComponent<UDPTransport>();
            _transport.address = IPAddress.Loopback.ToString();
            _transport.serverPort = GetUnusedLoopbackPort();
            var transportSettings = new SerializedObject(_transport);
            transportSettings.FindProperty("_useNativeSockets").boolValue = false;
            transportSettings.ApplyModifiedPropertiesWithoutUndo();

            _manager = _root.AddComponent<NetworkManager>();
            _manager.transport = _transport;
            var managerSettings = new SerializedObject(_manager);
            managerSettings.FindProperty("_startServerFlags").intValue = 0;
            managerSettings.FindProperty("_startClientFlags").intValue = 0;
            managerSettings.ApplyModifiedPropertiesWithoutUndo();

            _rules = ScriptableObject.CreateInstance<NetworkRules>();
            var ruleSettings = new SerializedObject(_rules);
            ruleSettings.FindProperty("_defaultSceneRules.sceneCleanupModeOnDisconnect").enumValueIndex = (int)SceneCleanupMode.Off;
            ruleSettings.ApplyModifiedPropertiesWithoutUndo();
            _manager.SetNetworkRules(_rules);
            _admission = _root.AddComponent<PurrServicesMatchAuthenticator>();
            _admission.timeout = 15;
            _root.SetActive(true);
            _transport.SetStatisticsEnabled(true);
            yield return null; // Run Start with automatic networking disabled.
        }

        [UnityTest]
        [Category("MatchmakingLoopback")]
        public IEnumerator UdpHandshake_DoesNotJoinGameplayUntilServiceAdmissionSucceeds()
        {
            _verification = new TaskCompletionSource<HttpResult<MatchmakingAdmission>>();
            int verifierCalls = 0, serverJoins = 0, clientJoins = 0;
            string receivedMatch = null, receivedPlayer = null, receivedCredential = null;
            CancellationToken verificationCancellation = default;
            _admission.Setup(_manager, MatchId, PlayerId, Credential, (match, player, token, cancellation) =>
            {
                verifierCalls++;
                receivedMatch = match;
                receivedPlayer = player;
                receivedCredential = token;
                verificationCancellation = cancellation;
                return _verification.Task;
            });
            _manager.onPlayerJoined += (_, _, asServer) =>
            {
                if (asServer) serverJoins++;
                else clientJoins++;
            };

            _manager.StartServer();
            yield return Until(() => _manager.serverState == ConnectionState.Connected,
                "The local UDP listener did not start.");
            _manager.StartClient();
            yield return Until(() => verifierCalls > 0,
                "The generated admission payload did not reach the server over UDP.");

            Assert.AreEqual(MatchId, receivedMatch);
            Assert.AreEqual(PlayerId, receivedPlayer);
            Assert.AreEqual(Credential, receivedCredential);
            Assert.AreEqual(ConnectionState.Connected, _manager.clientState,
                "A transport connection must exist before service admission.");
            Assert.IsFalse(verificationCancellation.IsCancellationRequested);
            // Give multiple network ticks a chance to run while the service is
            // deliberately pending. A socket connection is not a game player.
            var holdUntil = Time.realtimeSinceStartup + 0.2f;
            while (Time.realtimeSinceStartup < holdUntil)
            {
                Assert.AreEqual(0, _manager.playerCount);
                Assert.AreEqual(0, serverJoins);
                Assert.AreEqual(0, clientJoins);
                Assert.IsFalse(_manager.isLocalPlayerReady);
                Assert.IsFalse(_admission.TryGetConnection(PlayerId, out _));
                yield return null;
            }

            _verification.SetResult(new HttpResult<MatchmakingAdmission>
            {
                success = true,
                statusCode = 200,
                data = new MatchmakingAdmission { valid = true, playerId = PlayerId, team = 1 }
            });
            yield return Until(() => _manager.isLocalPlayerReady && serverJoins == 1 && clientJoins == 1,
                "A verified connection did not become a PurrNet game player.");

            Assert.AreEqual(1, verifierCalls);
            Assert.AreEqual(1, _manager.playerCount);
            Assert.IsTrue(_admission.TryGetConnection(PlayerId, out var connection));
            Assert.IsTrue(connection.isValid);
            Assert.IsTrue(_admission.TryGetPlayerID(connection, out var mappedPlayer));
            Assert.AreEqual(PlayerId, mappedPlayer);
            Assert.IsTrue(_admission.TryGetTeam(PlayerId, out var team));
            Assert.AreEqual(1, team);
            Assert.Greater(_transport.nativePacketsSent, 0L, "This must exercise UDP, not an in-process auth call.");
            Assert.Greater(_transport.nativePacketsReceived, 0L);

            _manager.StopClient();
            yield return Until(() => !_admission.TryGetConnection(PlayerId, out _) && _manager.playerCount == 0,
                "Disconnecting the admitted peer did not release its identity.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (Application.isPlaying)
            {
                // Unsubscribe network modules before releasing a pending fake
                // response, including when a preceding assertion failed.
                if (_manager)
                {
                    if (_manager.clientState != ConnectionState.Disconnected) _manager.StopClient();
                    if (_manager.serverState != ConnectionState.Disconnected) _manager.StopServer();
                }
                if (_admission) _admission.ResetContext();
                _verification?.TrySetCanceled();
                yield return null;
                yield return null;
                if (_root) Object.Destroy(_root);
                if (_rules) Object.Destroy(_rules);
                yield return null;
                yield return new ExitPlayMode();
            }
            if (SessionState.GetBool(EnvironmentState + ".owned", false))
            {
                var previous = SessionState.GetBool(EnvironmentState + ".existed", false)
                    ? SessionState.GetString(EnvironmentState, string.Empty) : null;
                Environment.SetEnvironmentVariable("CI", previous);
                SessionState.EraseBool(EnvironmentState + ".owned");
                SessionState.EraseBool(EnvironmentState + ".existed");
                SessionState.EraseString(EnvironmentState);
            }
        }

        static IEnumerator Until(Func<bool> condition, string failure)
        {
            float deadline = Time.realtimeSinceStartup + 10;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.IsTrue(condition(), failure);
        }

        static ushort GetUnusedLoopbackPort()
        {
            using var reservation = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            reservation.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            return (ushort)((IPEndPoint)reservation.LocalEndPoint).Port;
        }
    }
}
#endif
