# Service matchmaking

PurrLobby can use PurrServices queues for two-team competitive matchmaking,
while keeping its existing lobby menus. The service selects players and teams;
Unity handles explicit ready checks, starts or joins the game, and authenticates
each connection against the saved match roster.

## Set up the menu

1. Install PurrServices **1.3.0-beta.1 or newer**, PurrNet, and PurrUI. This development
   checkout pins the matching PurrServices Git commit in its package manifest.
   To develop both repositories together, replace that dependency with
   `file:../../PurrServices/Assets/Tool` while they are sibling directories.
2. Link the PurrServices project using its existing settings/setup workflow.
   Enable Matchmaking and create an enabled queue in the project dashboard.
3. Duplicate a preset from `Providers/PurrNet/Preset/Matchmaking` into your own
   assets and assign it to the menu's `LobbyManager`:
   - `Orchestrator.ServiceCasual`: device sign-in, queue `casual`, player-hosted
     games over PurrTransport relay/NAT.
   - `Orchestrator.ServiceRanked`: Steam sign-in, queue `ranked`, dedicated
     games over direct UDP. Requires Steamworks.NET and project Steam auth.
     A custom username/password session provider also works for ranked queues.
4. Set the matchmaker's **Queue Id** to your actual queue ID. Team sizes,
   regions, ranked policy, skill windows, and hosting are configured on the
   service, not in the menu. The presets do not create or enable queues.
5. Configure **Latency Probes** on the matchmaker: one region ID and HTTP GET
   endpoint per region, close to the servers that will host games. HTTPS is
   required except for loopback development; WebGL endpoints need CORS.
   Up to eight regions are sampled concurrently, twice each, with a three-second
   timeout per sample. Failed regions are omitted. Alternatively pass measured
   `request.latencies` from your own probe system. No guessed latency is sent.
6. Set the game scene and transport on the matchmaking game allocator. Include
   the scene in the build and put a `NetworkManager` in it. The normal menu
   scene loader suppresses auto-start while installing matchmaking admission.
   Custom loaders must disable network auto-start themselves.

If presets are missing, use **Tools > PurrNet > PurrLobby > Create Service
Matchmaking Presets**. Existing preset files are preserved.

The queue's hosting model and the allocator's transport are separate choices.
Both player-hosted and dedicated games can use relay or direct UDP; all clients
and the allocated server must agree on transport. Direct player hosts need a
reachable public address and port configured on the allocator. Direct UDP is
unavailable in WebGL. Ordinary lobby game starts continue using PurrTransport.

Relay hosts pin their listener to the service-selected region. **Relay Regions**
maps queue region IDs to relay region IDs (default `eu` to `eu-central`); otherwise
IDs must match exactly. Configure the same aliases on dedicated relay servers.
An unavailable region fails allocation instead of silently selecting another
region. Direct-server workers must claim matches in the region they serve.

## Search, parties, and ready checks

The normal Matchmake button uses the selected provider's Queue Id. The shipped
matchmaking view displays queue/allocation state and an explicit Accept/Decline
prompt with a deadline and ready count. It never accepts on a player's behalf.
It creates matching ready-check buttons from its existing Cancel button; custom
views can assign their own buttons in the Inspector.

Custom menus can supply a queue override, measured latencies, or party details:

```csharp
using System.Collections.Generic;
using PurrNet.Lobby;

var request = new MatchmakingRequest
{
    queueId = "ranked-doubles",
    partySize = 2,
    latencies = new Dictionary<string, int> { ["eu"] = measuredEuRtt }
};
// Open MatchmakingView with your existing PurrUI stack, then:
view.Setup(orchestrator, request);
```

For a solo search omit `partySize` or set it to 1. A party leader shares the
ticket ID and invitation code shown while assembling. Each member opens the
view with `partyTicketId` and `partyInviteCode`, using their own signed-in
account and latency measurements. The party joins the queue only when full.
`PurrServicesMatchmakingProvider.currentParty` and `onPartyChanged` expose
assembly progress for a custom party UI. This does not silently enroll an
existing lobby's members; each member explicitly joins the ticket.

Closing the search cancels its ticket; leaving a party cancels the whole ticket.
Every member must stay connected and polling. If a ready check fails, accepted
parties may return to the queue without losing their wait age. Decliners and
nonresponders get the service's cooldown. The provider handles session changes,
late create responses, transient failures, and Retry-After backoff.
Uncertain create/join responses receive at most two retries using the same
request ID and captured account, so a lost response does not create a second
party. Unresolved requests still rely on the service's expiry deadline.

The provider exposes `currentMatch` for the assigned roster and match ID.
Admission credentials in that snapshot belong only to the current player.
Never store them in lobby metadata, chat, analytics, or logs.

## Player-hosted games

The elected host loads the game during `allocating`. The allocator installs
`PurrServicesMatchAuthenticator`, starts the server listener, and publishes the
connection only after the listener reports ready. It then starts its own local
client. Other players join only after the match becomes `active`.

For relay hosting the service supplies a unique room name. For direct hosting,
configure the allocator's public host address and port. Host migration is not
implemented; a lost host ends the game according to your game's policy.

Only a casual host can report an outcome with the public player client:

```csharp
var response = await PurrNet.Services.PurrServices.instance.matchmaking
    .SubmitResultAsync(matchId, stableResultId, winningTeam); // 0, 1, or null (draw)
```

Call this from the game's authoritative outcome logic. Keep the same result ID
and winner for retries. Ranked player-hosted games require a separate trusted
backend to validate evidence and report the result; no project secret belongs
in a player build. The Unity integration does not infer scores or make a peer's
unverified result authoritative.

## Dedicated games

Run a dedicated Unity build or external allocator using the project's secret
key from its runtime environment. Provisioning machines/containers remains the
game's responsibility. `MatchmakingServerClient` exposes claims, lease renewal,
ready publication, admission verification, results, and cancellation.

For an already running dedicated Unity worker, place
`PurrServicesDedicatedMatchServer` beside its offline `NetworkManager`:

```csharp
using System;
using PurrNet.Services;
using PurrNet.Lobby.PurrNet;

var client = new MatchmakingServerClient(serviceUrl,
    Environment.GetEnvironmentVariable("PURR_MATCHMAKING_SECRET"),
    scope: "production", compatibility: gameBuild);
var response = await client.ClaimAsync(workerId, queueId: "ranked", region: "eu");
if (!response.success) throw new InvalidOperationException(response.error);
if (response.data.match != null)
{
    await dedicatedServer.StartClaimAsync(client, response.data, workerId,
        new MatchmakingConnection { serverAddress = publicAddress, serverPort = publicPort },
        PurrServicesMatchTransport.DirectUdp, bindPort: localBindPort);
}
```

Idle workers should back off rather than loop tightly; honor Retry-After on
errors. Start calls on Unity's main thread. The bootstrap renews its claim
before opening a listener, renews during startup, installs admission, and
publishes only while the claim is current. A failed/superseded process stops
locally and must not cancel a replacement worker's match. An external watchdog
decides when to cancel an abandoned allocation.

From authoritative game logic call `dedicatedServer.ReportResultAsync` with a
stable result ID and team 0/1 (or null for a draw). A successful report leaves
the game running so it can show game-over; call `StopLocal()` when finished.
Use `CancelMatchAsync` for an invalid/unavailable active match according to
your game's policy. Local shutdown alone does not erase a ranked outcome.

This bootstrap does not implement a fleet scheduler, game-specific forfeits,
or a trusted evidence verifier for ranked peer games. Those need the game's
runtime infrastructure and rules.

## Admission and gameplay identity

`PurrServicesMatchAuthenticator` runs before PurrNet admits a connection. It
verifies the match ID, player ID and private connection token through the host
or trusted-server API, rejects duplicate live players, and clears reservations
on raw transport disconnects. Verification fails closed on timeouts/errors.
Old asynchronous responses cannot admit a player into a replacement session.

Gameplay can obtain the service player ID with `TryGetPlayerID(connection, out
playerId)` and team with `TryGetTeam(playerId, out team)`. The authenticator also
implements `IProvideConnectionToPlayerID`. Use those verified values rather
than client-supplied names or team selections. Reconnects reuse the player's
credential until the match ends; the old connection must first disconnect.

## Verification

Unity Editor tests cover SDK contracts, provider lifecycle, ready-check UI,
admission rejection/timeouts/duplicates, and allocation lifecycle. Use Unity's
Test Runner to run `PurrServices.Matchmaking.Tests`, `PurrNet.Lobby.Tests`, and
`PurrNet.Lobby.PurrServices.Tests`. Before a release, run separate game processes
against your configured queues and server environment to validate transport
reachability, real authentication, reconnects, and authoritative game results.
