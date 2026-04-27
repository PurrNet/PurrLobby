using System.Threading.Tasks;

namespace PurrNet.Services
{
    /// <summary>
    /// Manages Edgegap game server deployments through the PurrNet API.
    /// The PurrNet server holds the Edgegap API token and proxies all
    /// requests, so the token never touches the game client.
    ///
    /// Requires the player to be authenticated via <see cref="AuthService"/>
    /// the verified player session is used for per-user deployment limits.
    /// </summary>
    public class EdgegapService
    {
        readonly ServiceHttp _http;

        internal EdgegapService(ServiceHttp http)
        {
            _http = http;
        }

        /// <summary>
        /// Request a new Edgegap game server deployment.
        /// The server will use the project's configured Edgegap app and version.
        /// The authenticated player's session is sent automatically for
        /// per-user deployment limiting.
        /// </summary>
        /// <param name="playerIds">
        /// Player IDs of the players who will connect. The server resolves
        /// these to cached IP addresses for optimal region selection.
        /// Pass null or empty to let Edgegap choose a default region.
        /// </param>
        public async Task<DeployResult> DeployAsync(string[] playerIds = null)
        {
            var request = new EdgegapDeployRequest
            {
                playerIds = playerIds
            };

            var response = await _http.PostAsync<EdgegapDeployResponse>(
                "/api/edgegap/deploy",
                request
            );

            if (!response.success)
            {
                return new DeployResult
                {
                    success = false,
                    error = response.error
                };
            }

            return new DeployResult
            {
                success = true,
                requestId = response.data.requestId
            };
        }

        /// <summary>
        /// Poll the status of an active deployment.
        /// Check <see cref="DeploymentStatusResult.deployment"/>.<see cref="EdgegapStatusResponse.ready"/>
        /// to know when the server is ready to accept connections.
        /// </summary>
        public async Task<DeploymentStatusResult> GetStatusAsync(string requestId)
        {
            var response = await _http.GetAsync<EdgegapStatusResponse>(
                $"/api/edgegap/{requestId}"
            );

            if (!response.success)
            {
                return new DeploymentStatusResult
                {
                    success = false,
                    error = response.error
                };
            }

            return new DeploymentStatusResult
            {
                success = true,
                deployment = response.data
            };
        }

        /// <summary>
        /// Stop a running deployment.
        /// </summary>
        public async Task<DeploymentStopResult> StopAsync(string requestId)
        {
            var response = await _http.DeleteAsync<EdgegapStopResponse>(
                $"/api/edgegap/{requestId}"
            );

            if (!response.success)
            {
                return new DeploymentStopResult
                {
                    success = false,
                    error = response.error
                };
            }

            return new DeploymentStopResult
            {
                success = true,
                requestId = response.data.requestId
            };
        }

        /// <summary>
        /// Stop every active deployment owned by the authenticated player
        /// in the current project. Scoped to the calling player only —
        /// other players' deployments are never touched.
        ///
        /// Best-effort: if Edgegap fails on one deployment the others still
        /// proceed. Each deployment's local concurrency slot is freed
        /// regardless of Edgegap reachability, so a follow-up Deploy call
        /// will not hit the limit.
        /// </summary>
        public async Task<DeploymentStopAllResult> StopAllAsync()
        {
            var response = await _http.DeleteAsync<EdgegapStopAllResponse>(
                "/api/edgegap/deployments"
            );

            if (!response.success)
            {
                return new DeploymentStopAllResult
                {
                    success = false,
                    error = response.error
                };
            }

            return new DeploymentStopAllResult
            {
                success = true,
                count = response.data.count,
                stopped = response.data.stopped
            };
        }

        /// <summary>
        /// Deploy a server and wait until it is ready (or an error occurs).
        /// Polls every <paramref name="pollIntervalMs"/> milliseconds up to
        /// <paramref name="timeoutMs"/> total.
        /// </summary>
        /// <param name="playerIds">Player IDs for region selection (resolved to IPs server-side).</param>
        /// <param name="pollIntervalMs">Milliseconds between status polls (default 2000).</param>
        /// <param name="timeoutMs">Total timeout in milliseconds (default 300000 = 5 min).</param>
        /// <returns>The final status, or an error result if it timed out or failed.</returns>
        public async Task<DeploymentStatusResult> DeployAndWaitAsync(
            string[] playerIds = null,
            int pollIntervalMs = 2000,
            int timeoutMs = 300000)
        {
            var deployResult = await DeployAsync(playerIds);
            if (!deployResult.success)
            {
                return new DeploymentStatusResult
                {
                    success = false,
                    error = deployResult.error
                };
            }

            var requestId = deployResult.requestId;
            var elapsed = 0;

            while (elapsed < timeoutMs)
            {
                await Task.Delay(pollIntervalMs);
                elapsed += pollIntervalMs;

                var status = await GetStatusAsync(requestId);
                if (!status.success)
                {
                    return status;
                }

                if (status.deployment.error)
                {
                    return new DeploymentStatusResult
                    {
                        success = false,
                        deployment = status.deployment,
                        error = status.deployment.errorDetail ?? "Deployment failed"
                    };
                }

                if (status.deployment.ready)
                {
                    return status;
                }
            }

            return new DeploymentStatusResult
            {
                success = false,
                error = $"Deployment timed out after {timeoutMs / 1000}s (requestId: {requestId})"
            };
        }
    }
}
