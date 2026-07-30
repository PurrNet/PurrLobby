using System;
using System.Threading.Tasks;
#if PURR_SERVICES
using PurrNet.Services;
#endif
using PurrNet.Transports;
using UnityEngine;

namespace PurrNet.Lobby.Edgegap
{
    public enum EdgegapAllocatorTransport
    {
        UDP,
        Web,
    }

    [CreateAssetMenu(menuName = "PurrLobby/Edgegap/Game Allocator", fileName = "Edgegap Game Allocator", order = -199)]
    public class EdgegapGameAllocator : GameAllocatorProvider
    {
        [SerializeField, PurrScene] private string _gameScene;
        [SerializeField] private EdgegapAllocatorTransport _transport = EdgegapAllocatorTransport.UDP;

        [Tooltip("Optional Edgegap port name. If empty, the first port whose protocol matches the chosen transport is used.")]
        [SerializeField] private string _portName = "";

#if !PURR_SERVICES
#pragma warning disable CS0414 // Preserve serialized allocation settings while their service is unavailable.
#endif
        [Tooltip("Total deployment timeout in milliseconds.")]
        [SerializeField] private int _deploymentTimeoutMs = 300_000;

        [Tooltip("Polling interval in milliseconds while waiting for the deployment to become ready.")]
        [SerializeField] private int _pollIntervalMs = 2_000;
#if !PURR_SERVICES
#pragma warning restore CS0414
#endif

        /// <summary>
        /// Transport this allocator connects with. The single source of truth for
        /// the transport - an <see cref="EdgegapMatchmakingProvider"/> paired with
        /// this allocator reads it from here rather than holding its own copy.
        /// </summary>
        public EdgegapAllocatorTransport Transport => _transport;

        /// <summary>Optional named port to connect through; empty means pick by protocol.</summary>
        public string PortName => _portName;

#if PURR_SERVICES
        public override async Task<GameStartResponse> AllocateGame(ILobby lobby)
        {
            await PurrServices.instance.edgegap.StopAllAsync();

            var result = await PurrServices.instance.edgegap.DeployAndWaitAsync(
                playerIds: null,
                pollIntervalMs: _pollIntervalMs,
                timeoutMs: _deploymentTimeoutMs);

            if (!result.success)
                return GameStartResponse.Failure(result.error ?? "Edgegap deployment failed.");

            var deployment = result.deployment;

            if (!TryPickPort(deployment, out var port, out var portError))
                return GameStartResponse.Failure(portError);

            var address = !string.IsNullOrEmpty(deployment.fqdn) ? deployment.fqdn : deployment.publicIp;
            if (string.IsNullOrEmpty(address))
                return GameStartResponse.Failure("Edgegap deployment returned no fqdn or publicIp.");

            return GameStartResponse.Success(new ConnectionInfo
            {
                serverAddress = address,
                serverPort = port.external,
            });
        }

        private bool TryPickPort(EdgegapStatusResponse deployment, out EdgegapPortInfo port, out string error)
        {
            port = default;
            error = null;

            if (deployment.ports == null || deployment.ports.Count == 0)
            {
                error = "Edgegap deployment exposes no ports.";
                return false;
            }

            if (!string.IsNullOrEmpty(_portName))
            {
                if (deployment.ports.TryGetValue(_portName, out port))
                    return true;

                error = $"Port `{_portName}` not found on Edgegap deployment.";
                return false;
            }

            foreach (var kv in deployment.ports)
            {
                if (MatchesTransport(kv.Value.protocol))
                {
                    port = kv.Value;
                    return true;
                }
            }

            error = $"No Edgegap port found matching transport `{_transport}`. Set the port name explicitly on `{name}`.";
            return false;
        }
#else
        public override Task<GameStartResponse> AllocateGame(ILobby lobby)
        {
            return Task.FromResult(GameStartResponse.Failure(
                "PurrServices is not installed. Install it before using direct Edgegap allocation."));
        }
#endif

        private bool MatchesTransport(string protocol)
        {
            if (string.IsNullOrEmpty(protocol))
                return false;

            switch (_transport)
            {
                case EdgegapAllocatorTransport.UDP:
                    return protocol.Equals("UDP", StringComparison.OrdinalIgnoreCase);
                case EdgegapAllocatorTransport.Web:
                    return protocol.Equals("WS", StringComparison.OrdinalIgnoreCase)
                        || protocol.Equals("WEBSOCKET", StringComparison.OrdinalIgnoreCase)
                        || protocol.Equals("WSS", StringComparison.OrdinalIgnoreCase)
                        || protocol.Equals("HTTP", StringComparison.OrdinalIgnoreCase)
                        || protocol.Equals("HTTPS", StringComparison.OrdinalIgnoreCase)
                        || protocol.Equals("TCP", StringComparison.OrdinalIgnoreCase);
                default:
                    return false;
            }
        }

        public override Task LoadGame(ILobby lobby)
        {
            return LoadGameScene(_gameScene);
        }

        /// <summary>Edgegap deploys dedicated servers; clients never host.</summary>
        protected override bool supportsHosting => false;

        protected override bool ConfigureTransport(NetworkManager manager, ConnectionInfo connection, bool asHost)
        {
            switch (_transport)
            {
                case EdgegapAllocatorTransport.UDP:
                {
                    var udp = manager.transport as UDPTransport ?? GetOrAddComponent<UDPTransport>(manager.gameObject);
                    udp.address = connection.serverAddress;
                    udp.serverPort = (ushort)connection.serverPort;
                    manager.transport = udp;
                    return true;
                }
                case EdgegapAllocatorTransport.Web:
                {
                    var web = manager.transport as WebTransport ?? GetOrAddComponent<WebTransport>(manager.gameObject);
                    web.address = connection.serverAddress;
                    web.serverPort = (ushort)connection.serverPort;
                    manager.transport = web;
                    return true;
                }
                default:
                    return false;
            }
        }
    }
}
