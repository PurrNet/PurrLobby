# PurrLobby

A drop-in lobby and matchmaking front-end for [PurrNet](https://purrnet.dev/).

PurrLobby gives you the multiplayer menu flow most games need: create a lobby,
share a code, browse open lobbies when the selected backend supports it, chat,
ready up, matchmake, and move everyone into a game scene together. The UI is
already built. You choose a backend, assign your game scene, and customize the
prefabs as needed.

> **Status:** beta (`1.0.0-beta.3`). The API is close to stable but may still
> shift before 1.0. Pin a version if you depend on it.

## Requirements

- Unity `6000.0` or newer.
- [PurrNet](https://github.com/PurrNet/PurrNet) and
  [PurrUI](https://github.com/PurrNet/PurrUI).
- PurrServices when using the PurrNet Services lobby provider or Edgegap game
  allocator.
- Optional provider packages:
  - [Nakama Unity](https://github.com/heroiclabs/nakama-unity) for Nakama
    providers.
  - [Edgegap Unity plugin](https://github.com/edgegap/edgegap-unity-plugin) for
    Edgegap workflows.

## Installing

In Unity, open **Window > Package Manager**, choose **Add package from git
URL**, and paste:

```text
https://github.com/PurrNet/PurrLobby.git?path=/Assets/Tool#dev
```

For local development, clone the repository and open it with a supported Unity
version. The checked-in `Packages/manifest.json` points at the dependency
versions used by the sample project.

## What's Included

- Menu flow: main menu, create lobby, join by code, lobby browser, matchmaking,
  and an in-lobby view with a player list and chat.
- Ready-up and owner-driven game start.
- A scene handoff that loads the game scene, connects the network transport,
  and returns to the menu on leave, game over, or connection loss.
- Swappable provider interfaces for sessions, lobbies, matchmaking, and game
  allocation.
- Prefabs and sample scenes under `Assets/Tool`.

## Providers

| Provider | Lobbies | Lobby Browser | Matchmaking | Game Allocation |
|----------|---------|---------------|-------------|-----------------|
| PurrNet Services | yes | yes | via generic lobby matchmaker | PurrTransport |
| Nakama | create/join by id or code | no | yes | Nakama relayed match |
| Edgegap | no | no | yes | managed server assignment |

Providers advertise optional lobby actions through `LobbyCapabilities`. The
menu hides unsupported buttons automatically, so a backend without lobby
browsing will not show the browser entry point.

Edgegap matchmaking forms the match and returns ready-to-use connection info in
one step. Pair `EdgegapMatchmakingProvider` with `EdgegapGameAllocator` so the
matchmaker and allocator agree on transport and port selection.

## Getting Started

1. Open `Assets/Tool/Scenes/MenuScene.unity` for a working example.
2. Select the `LobbyManager` in the scene and assign a `GameOrchestrator`.
   Preset orchestrators live under `Assets/Tool/Providers/.../Preset`.
3. Choose the session, lobby, matchmaking, and game allocator providers for
   your backend.
4. Set the allocator's game scene to your gameplay scene.
5. In the gameplay scene, keep the `NetworkManager` auto-start flags disabled.
   The allocator starts the host or client after loading the scene.

To customize the UI, edit the prefabs under `Assets/Tool/Prefabs/Views` and
the smaller elements under `Assets/Tool/Prefabs/Elements`.

## Writing A Backend

Implement the provider base classes in `Assets/Tool/Runtime/Providers`:

- `SessionProvider`
- `LobbyProvider`
- `MatchmakingProvider`
- `GameAllocatorProvider`

Lobby objects should implement the contracts in `Assets/Tool/Runtime/Contracts`,
especially `ILobby`, `IPlayer`, `IMetadata`, and `ILobbyChat`. The PurrNet
provider under `Assets/Tool/Providers/PurrNet` is the smallest complete
example; Nakama is a fuller reference for relayed-match lobby state.

## License

See [`Assets/Tool/LICENSE.txt`](Assets/Tool/LICENSE.txt).
